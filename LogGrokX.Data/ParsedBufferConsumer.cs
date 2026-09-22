using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogGrokX.Data.Index;
using LogGrokX.Data.IndexTree;
using LogGrokX.Data.Monikers;

namespace LogGrokX.Data
{
    /// <summary>
    /// Applies parsed line buffers to <see cref="LineIndex"/> and <see cref="Indexer"/>.
    /// <para>
    /// Indexing is split into an order-independent part (walking line meta
    /// information, hashing index keys, resolving them to key numbers and index
    /// trees, registering new component values), which several workers do in
    /// parallel, and an order-dependent part (assigning line numbers and appending
    /// to the per-key index trees), which a single merge thread does strictly in
    /// buffer order. Line numbering and index contents are therefore identical to
    /// the sequential implementation.
    /// </para>
    /// </summary>
    public sealed class ParsedBufferConsumer
    {
        private readonly BlockingCollection<Task<PreparedBuffer>> _queue;

        private readonly LineIndex _lineIndex;
        private readonly Indexer _indexer;
        private readonly LogMetaInformation _logMetaInformation;
        private readonly StringPool _stringPool;
        private readonly int _componentsCount;
        private readonly bool _isParallel;
        private long? _totalBytesRead;
        private readonly Task _mergeTask;

        /// <summary>
        /// Test hook: forces parallel or sequential preparation of index data.
        /// </summary>
        internal static bool? ParallelIndexingOverride;

        private const int MinProcessorCountForParallelIndexing = 3;

        /// <summary>
        /// <c>LOGGROKX_PARALLEL_INDEXING=0</c> disables parallel preparation,
        /// <c>=1</c> forces it on. Unset means "decide by core count".
        /// </summary>
        private static bool? EnvironmentSwitch =>
            Environment.GetEnvironmentVariable("LOGGROKX_PARALLEL_INDEXING") switch
            {
                "0" or "false" or "False" => false,
                "1" or "true" or "True" => true,
                _ => null
            };

        public ParsedBufferConsumer(
            LineIndex lineIndex,
            Indexer indexer,
            LogMetaInformation logMetaInformation,
            StringPool stringPool)
        {
            _lineIndex = lineIndex;
            _indexer = indexer;
            _logMetaInformation = logMetaInformation;
            _stringPool = stringPool;
            _componentsCount = logMetaInformation.IndexedFieldNumbers.Length;
            _isParallel = ParallelIndexingOverride
                          ?? EnvironmentSwitch
                          ?? Environment.ProcessorCount >= MinProcessorCountForParallelIndexing;

            _queue = new BlockingCollection<Task<PreparedBuffer>>(
                Math.Max(Environment.ProcessorCount, 4));

            _mergeTask = Task.Factory.StartNew(ConsumeBuffers, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void AddParsedBuffer(long bufferStartOffset, int lineCount, string parsedBuffer)
        {
            if (_isParallel)
            {
                _queue.Add(Task.Run(() => Prepare(bufferStartOffset, lineCount, parsedBuffer)));
            }
            else
            {
                _queue.Add(Task.FromResult(Prepare(bufferStartOffset, lineCount, parsedBuffer)));
            }
        }

        public void CompleteAdding(long totalBytesRead)
        {
            _totalBytesRead = totalBytesRead;
            _queue.CompleteAdding();
        }

        /// <summary>
        /// Order-independent part: walk the buffer, resolve index keys.
        /// Runs on worker threads; the parsed buffer is returned to the pool here,
        /// so nothing downstream references it.
        /// </summary>
        private unsafe PreparedBuffer Prepare(long bufferStartOffset, int lineCount, string buffer)
        {
            var prepared = PreparedBuffer.Rent(lineCount);
            var metaOffset = 0;

            fixed (char* start = buffer)
            {
                for (var idx = 0; idx < lineCount; idx++)
                {
                    var lineMetaInformation = LineMetaInformation.Get(start + metaOffset, _componentsCount);
                    prepared.LineOffsets[idx] = bufferStartOffset + lineMetaInformation.LineOffsetFromBufferStart;

                    var indexKey = new IndexKey(buffer, metaOffset, _componentsCount);
                    var (keyNumber, index) = _indexer.ResolveKey(indexKey, prepared.Notifications);
                    prepared.KeyNumbers[idx] = keyNumber;
                    prepared.Indices[idx] = index;

                    metaOffset += lineMetaInformation.TotalSizeWithPayloadCharsAligned;
                }
            }

            _stringPool.Return(buffer);
            return prepared;
        }

        /// <summary>
        /// Order-dependent part: assign line numbers and append to the indexes.
        /// </summary>
        private void ConsumeBuffers()
        {
            long lastLineOffset = 0;

            foreach (var preparedTask in _queue.GetConsumingEnumerable())
            {
                var prepared = preparedTask.GetAwaiter().GetResult();
                try
                {
                    var count = prepared.Count;
                    if (count == 0)
                        continue;

                    var firstLineNumber = _lineIndex.AddRange(prepared.LineOffsets.AsSpan(0, count));
                    for (var idx = 0; idx < count; idx++)
                        _indexer.Append(prepared.KeyNumbers[idx], prepared.Indices[idx], firstLineNumber + idx);

                    if (prepared.Notifications.Count > 0)
                        _indexer.RaiseComponentNotifications(prepared.Notifications);

                    lastLineOffset = prepared.LineOffsets[count - 1];
                }
                finally
                {
                    prepared.Dispose();
                }
            }

            if (_totalBytesRead is not { } fileSize)
            {
                throw new InvalidOperationException();
            }

            _lineIndex.Finish((int)(fileSize - lastLineOffset));
            _indexer.Finish();
        }

        private sealed class PreparedBuffer : IDisposable
        {
            private bool _isReturned;

            public long[] LineOffsets { get; private set; } = Array.Empty<long>();

            public IndexKeyNum[] KeyNumbers { get; private set; } = Array.Empty<IndexKeyNum>();

            public IndexTree<int, SimpleLeaf<int>>[] Indices { get; private set; } =
                Array.Empty<IndexTree<int, SimpleLeaf<int>>>();

            public List<(int componentNumber, IndexKey key)> Notifications { get; } = new();

            public int Count { get; private set; }

            public static PreparedBuffer Rent(int lineCount)
            {
                return new PreparedBuffer
                {
                    LineOffsets = ArrayPool<long>.Shared.Rent(lineCount),
                    KeyNumbers = ArrayPool<IndexKeyNum>.Shared.Rent(lineCount),
                    Indices = ArrayPool<IndexTree<int, SimpleLeaf<int>>>.Shared.Rent(lineCount),
                    Count = lineCount
                };
            }

            public void Dispose()
            {
                if (_isReturned)
                    return;

                _isReturned = true;
                ArrayPool<long>.Shared.Return(LineOffsets);
                ArrayPool<IndexKeyNum>.Shared.Return(KeyNumbers);
                // reference array: clear it so the trees are not kept alive by the pool
                ArrayPool<IndexTree<int, SimpleLeaf<int>>>.Shared.Return(Indices, clearArray: true);
                LineOffsets = Array.Empty<long>();
                KeyNumbers = Array.Empty<IndexKeyNum>();
                Indices = Array.Empty<IndexTree<int, SimpleLeaf<int>>>();
            }
        }
    }
}

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogGrokX.Data.Monikers;

namespace LogGrokX.Data
{
    /// <summary>
    /// Decodes and parses log lines.
    /// <para>
    /// Decoding plus regex parsing is by far the most expensive part of loading and
    /// it used to run on the loader thread, so loading could not use more than one
    /// core. Now the loader thread only slices the file into line-aligned raw
    /// chunks, several workers parse chunks in parallel and a single merge thread
    /// applies the results strictly in order - line numbering, the time index and
    /// every downstream index stay bit-identical to the sequential implementation.
    /// </para>
    /// <para>
    /// On machines with very few cores the parallel path cannot win (the indexing
    /// thread already saturates the second core), so there the lines are parsed
    /// inline, without the extra chunk copy.
    /// </para>
    /// </summary>
    public class LineProcessor : ILineDataConsumer, IDisposable
    {
        private const int InitialBufferSize = 64 * 1024;
        private const int RawChunkSizeBytes = 1024 * 1024;
        private const int MinProcessorCountForParallelParsing = 4;
        private const int ExactCharCountThreshold = 8 * 1024;

        private readonly StringPool _stringPool;
        private readonly Encoding _encoding;
        private readonly ILineParser _parser;
        private readonly int _componentCount;
        private readonly ParsedBufferConsumer _parsedBufferConsumer;
        private readonly TimeIndex _timeIndex;

        private readonly bool _isParallel;

        // parallel path
        private readonly BlockingCollection<Task<ParsedChunk>>? _parsedChunks;
        private readonly Task? _mergeTask;
        private RawChunk? _currentRawChunk;

        // inline path
        private readonly ParseState? _inlineState;
        private readonly DirectSink? _inlineSink;

        private bool _isCompleted;

        /// <summary>
        /// Test hook: forces the parallel or the inline parsing path regardless of
        /// the number of available cores.
        /// </summary>
        internal static bool? ParallelParsingOverride;

        public LineProcessor(LogFile logFile,
            LogMetaInformation metaInformation,
            ILineParser parser,
            ParsedBufferConsumer parsedBufferConsumer,
            StringPool stringPool,
            TimeIndex timeIndex)
        {
            _encoding = logFile.Encoding;
            _componentCount = metaInformation.IndexedFieldNumbers.Length;
            _parsedBufferConsumer = parsedBufferConsumer;
            _stringPool = stringPool;
            _timeIndex = timeIndex;
            _parser = parser;

            _isParallel = ParallelParsingOverride
                          ?? Environment.ProcessorCount >= MinProcessorCountForParallelParsing;

            if (_isParallel)
            {
                var inFlightChunks = Math.Max(Environment.ProcessorCount - 1, 2);
                _parsedChunks = new BlockingCollection<Task<ParsedChunk>>(inFlightChunks);
                _mergeTask = Task.Factory.StartNew(MergeParsedChunks, CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            else
            {
                _inlineState = CreateParseState();
                _inlineSink = new DirectSink(parsedBufferConsumer, timeIndex);
            }
        }

        public void AddLineData(long lineOffset, Span<byte> lineData)
        {
            if (!_isParallel)
            {
                _inlineState!.Process(lineOffset, lineData, _inlineSink!);
                return;
            }

            var chunk = _currentRawChunk;
            if (chunk == null)
            {
                chunk = RawChunk.Rent(RawChunkSizeBytes);
                _currentRawChunk = chunk;
            }

            if (!chunk.TryAdd(lineOffset, lineData))
            {
                DispatchCurrentChunk();
                chunk = RawChunk.Rent(Math.Max(RawChunkSizeBytes, lineData.Length));
                _currentRawChunk = chunk;
                if (!chunk.TryAdd(lineOffset, lineData))
                    throw new InvalidOperationException("Unable to store line data.");
            }

            if (chunk.Length >= RawChunkSizeBytes)
                DispatchCurrentChunk();
        }

        public void CompleteAdding(long totalBytesRead)
        {
            if (_isParallel)
            {
                DispatchCurrentChunk();
                _parsedChunks!.CompleteAdding();
                _mergeTask!.GetAwaiter().GetResult();
            }
            else
            {
                _inlineState!.Flush(_inlineSink!);
            }

            _isCompleted = true;
            _parsedBufferConsumer.CompleteAdding(totalBytesRead);
        }

        public void Dispose()
        {
            if (_isCompleted || !_isParallel)
                return;

            try
            {
                _currentRawChunk?.Dispose();
                _currentRawChunk = null;
                _parsedChunks!.CompleteAdding();
                _mergeTask!.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                // Disposing a cancelled load must not throw.
            }
        }

        private ParseState CreateParseState() =>
            new(_encoding, _parser, _stringPool, _componentCount);

        private void DispatchCurrentChunk()
        {
            var chunk = _currentRawChunk;
            _currentRawChunk = null;
            if (chunk == null || chunk.LineCount == 0)
            {
                chunk?.Dispose();
                return;
            }

            var task = Task.Run(() => ParseChunk(chunk));
            _parsedChunks!.Add(task);
        }

        private void MergeParsedChunks()
        {
            foreach (var chunkTask in _parsedChunks!.GetConsumingEnumerable())
            {
                var parsed = chunkTask.GetAwaiter().GetResult();
                foreach (var ticks in parsed.Ticks)
                    _timeIndex.Add(ticks);

                foreach (var (bufferOffset, lineCount, buffer) in parsed.Buffers)
                    _parsedBufferConsumer.AddParsedBuffer(bufferOffset, lineCount, buffer);
            }
        }

        private ParsedChunk ParseChunk(RawChunk chunk)
        {
            var sink = new ChunkSink();
            var state = CreateParseState();
            try
            {
                foreach (var (lineOffset, start, length) in chunk.Lines)
                    state.Process(lineOffset, chunk.GetLineData(start, length), sink);

                state.Flush(sink);
            }
            finally
            {
                chunk.Dispose();
            }

            return sink.Chunk;
        }

        private interface IParsedSink
        {
            void AddBuffer(long bufferOffset, int lineCount, string buffer);

            void AddTicks(long ticks);
        }

        private sealed class DirectSink : IParsedSink
        {
            private readonly ParsedBufferConsumer _consumer;
            private readonly TimeIndex _timeIndex;

            public DirectSink(ParsedBufferConsumer consumer, TimeIndex timeIndex)
            {
                _consumer = consumer;
                _timeIndex = timeIndex;
            }

            public void AddBuffer(long bufferOffset, int lineCount, string buffer) =>
                _consumer.AddParsedBuffer(bufferOffset, lineCount, buffer);

            public void AddTicks(long ticks) => _timeIndex.Add(ticks);
        }

        private sealed class ChunkSink : IParsedSink
        {
            public ParsedChunk Chunk { get; } = new();

            public void AddBuffer(long bufferOffset, int lineCount, string buffer) =>
                Chunk.Buffers.Add((bufferOffset, lineCount, buffer));

            public void AddTicks(long ticks) => Chunk.Ticks.Add(ticks);
        }

        private sealed class ParsedChunk
        {
            public List<(long bufferOffset, int lineCount, string buffer)> Buffers { get; } = new();

            public List<long> Ticks { get; } = new();
        }

        /// <summary>
        /// Decoding and parsing state for one independent stream of lines
        /// (the whole file on the inline path, one chunk on the parallel path).
        /// </summary>
        private sealed class ParseState
        {
            private readonly Encoding _encoding;
            private readonly ILineParser _parser;
            private readonly StringPool _stringPool;
            private readonly int _componentCount;
            private readonly int _metaSizeChars;

            private string? _currentString;
            private int _currentOffset;
            private int _currentBufferLineCount;
            private long _bufferOffset;

            public ParseState(Encoding encoding, ILineParser parser, StringPool stringPool, int componentCount)
            {
                _encoding = encoding;
                _parser = parser;
                _stringPool = stringPool;
                _componentCount = componentCount;
                _metaSizeChars = LineMetaInformation.GetSizeChars(componentCount);
            }

            public unsafe void Process(long lineOffset, ReadOnlySpan<byte> lineData, IParsedSink sink)
            {
                // GetMaxCharCount over-reserves (up to 3x for UTF-8), which is
                // irrelevant for ordinary lines but wastes real memory on huge ones,
                // so those are measured exactly.
                var necessarySpaceChars = _metaSizeChars +
                                          (lineData.Length <= ExactCharCountThreshold
                                              ? _encoding.GetMaxCharCount(lineData.Length)
                                              : _encoding.GetCharCount(lineData));

                if (_currentString == null)
                {
                    _currentString = SwitchToNewBuffer(necessarySpaceChars, lineOffset);
                }
                else if (_currentString.Length - _currentOffset < necessarySpaceChars)
                {
                    sink.AddBuffer(_bufferOffset, _currentBufferLineCount, _currentString);
                    _currentString = SwitchToNewBuffer(necessarySpaceChars, lineOffset);
                }

                fixed (char* stringPointer = _currentString.AsSpan(_currentOffset))
                {
                    var decodedStringSpan =
                        new Span<char>(stringPointer + _metaSizeChars, _currentString.Length - _currentOffset);
                    var stringLength = _encoding.GetChars(lineData, decodedStringSpan);
                    var stringFrom = _currentOffset + _metaSizeChars;

                    var lineMetaInformation =
                        LineMetaInformation.Get(stringPointer, _componentCount);

                    if (_parser.TryParse(_currentString, stringFrom, stringLength,
                            lineMetaInformation.ParsedLineComponents, out var timeTicks))
                    {
                        lineMetaInformation.LineOffsetFromBufferStart = (int)(lineOffset - _bufferOffset);
                        _currentOffset += lineMetaInformation.TotalSizeWithPayloadCharsAligned;
                        _currentBufferLineCount++;
                        sink.AddTicks(timeTicks);
                        return;
                    }

                    if (_currentOffset == 0)
                    {
                        _bufferOffset += lineData.Length;
                    }
                }
            }

            public void Flush(IParsedSink sink)
            {
                if (_currentString == null)
                    return;

                if (_currentBufferLineCount > 0)
                    sink.AddBuffer(_bufferOffset, _currentBufferLineCount, _currentString);
                else
                    _stringPool.Return(_currentString);

                _currentString = null;
                _currentOffset = 0;
                _currentBufferLineCount = 0;
            }

            private string SwitchToNewBuffer(int minimumBufferSizeChars, long currentLineOffset)
            {
                _currentOffset = 0;
                _bufferOffset = currentLineOffset;
                _currentBufferLineCount = 0;
                return _stringPool.Rent((minimumBufferSizeChars / InitialBufferSize + 1) * InitialBufferSize);
            }
        }

        private sealed class RawChunk : IDisposable
        {
            private byte[] _buffer = Array.Empty<byte>();
            private readonly List<(long lineOffset, int start, int length)> _lines = new(1024);
            private int _length;
            private bool _isReturned;

            public static RawChunk Rent(int capacity)
            {
                return new RawChunk
                {
                    _buffer = ArrayPool<byte>.Shared.Rent(capacity)
                };
            }

            public int Length => _length;

            public int LineCount => _lines.Count;

            public IReadOnlyList<(long lineOffset, int start, int length)> Lines => _lines;

            public bool TryAdd(long lineOffset, ReadOnlySpan<byte> lineData)
            {
                if (_length + lineData.Length > _buffer.Length)
                    return false;

                lineData.CopyTo(_buffer.AsSpan(_length));
                _lines.Add((lineOffset, _length, lineData.Length));
                _length += lineData.Length;
                return true;
            }

            public ReadOnlySpan<byte> GetLineData(int start, int length) => _buffer.AsSpan(start, length);

            public void Dispose()
            {
                if (_isReturned)
                    return;

                _isReturned = true;
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = Array.Empty<byte>();
                _lines.Clear();
            }
        }
    }
}

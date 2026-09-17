using System;
using System.IO;
using System.Text;
using System.Threading;
using BenchmarkDotNet.Attributes;
using LogGrokX.Data;

namespace LogGrokX.Benchmarks
{
    [MemoryDiagnoser]
    public class LoaderBenchmark
    {
        private const int LineCount = 200_000;
        private const int BufferSize = 64 * 1024;

        private const string Line =
            "2024-01-15 08:32:11.482 [INFO] Request completed in 42 ms for /api/orders/17\r\n";

        private MemoryStream _stream = null!;
        private LineCountingConsumer _consumer = null!;
        private LoaderImpl _loader = null!;
        private byte[] _cr = null!;
        private byte[] _lf = null!;

        [GlobalSetup]
        public void Setup()
        {
            var builder = new StringBuilder(LineCount * Line.Length);
            for (var i = 0; i < LineCount; i++)
                builder.Append(Line);

            var data = Encoding.UTF8.GetBytes(builder.ToString());
            _stream = new MemoryStream(data, writable: false);
            _consumer = new LineCountingConsumer();
            _loader = new LoaderImpl(BufferSize, _consumer);
            _cr = Encoding.UTF8.GetBytes("\r");
            _lf = Encoding.UTF8.GetBytes("\n");
        }

        [Benchmark]
        public long Load()
        {
            _consumer.LineCount = 0;
            _stream.Position = 0;
            _loader.Load(_stream, _cr.AsSpan(), _lf.AsSpan(), CancellationToken.None);
            return _consumer.LineCount;
        }

        private sealed class LineCountingConsumer : ILineDataConsumer
        {
            public long LineCount;

            public void AddLineData(long offset, Span<byte> lineData) => LineCount++;

            public void CompleteAdding(long totalBytesRead)
            {
            }
        }
    }
}

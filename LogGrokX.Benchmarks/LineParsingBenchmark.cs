using BenchmarkDotNet.Attributes;
using LogGrokX.Data;

namespace LogGrokX.Benchmarks
{
    [MemoryDiagnoser]
    public class LineParsingBenchmark
    {
        private const string SampleLine =
            "2024-01-15 08:32:11.482 [INFO] Request completed in 42 ms for /api/orders/17";

        private RegexBasedLineParser _fullParser = null!;
        private RegexBasedLineParser _indexedParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            var format = new LogFormat
            {
                Regex = @"^(?<Time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(?<Level>[A-Z]+)\] (?<Message>.*)$",
                IndexedFields = new[] { "Level" },
                TimeField = "Time",
                TimeFormat = "yyyy-MM-dd HH:mm:ss.fff"
            };

            var metaInformation = new LogMetaInformation(format);
            _fullParser = new RegexBasedLineParser(metaInformation);
            _indexedParser = new RegexBasedLineParser(metaInformation, onlyIndexed: true);
        }

        [Benchmark(Baseline = true)]
        public ParseResult ParseAllFields() => _fullParser.Parse(SampleLine);

        [Benchmark]
        public ParseResult ParseIndexedFieldsOnly() => _indexedParser.Parse(SampleLine);
    }
}

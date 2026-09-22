# Performance notes

Notes on the hot paths of LogGrokX and on what the core optimizations actually do.

## Loading pipeline

Loading is split into three stages:

1. `LoaderImpl` reads the file in 1 MB blocks and slices it into lines.
2. `LineProcessor` decodes and parses lines. By default this happens inline, on
   the loader thread. With `LOGGROKX_PARALLEL_PARSING=1` the lines are grouped
   into line-aligned raw chunks (1 MB) and parsed by thread-pool workers, and a
   dedicated merge thread applies parsed chunks **strictly in order**.
3. `ParsedBufferConsumer` applies parsed buffers to `LineIndex` and `Indexer`.

Invariants the parallel path must preserve (covered by
`OptimizationTests.LoadingKeepsLineOrderAndCount`, which runs both paths):

- line numbers follow file order;
- `TimeIndex` entries are added in line order (it is a parallel array to line
  numbers);
- `LineMetaInformation.LineOffsetFromBufferStart` is relative to the offset
  passed to `ParsedBufferConsumer.AddParsedBuffer`, so chunks must start at a
  line boundary.

`LineProcessor.ParallelParsingOverride` forces one of the two paths in tests, and
both paths are covered by the same test.

### Why parallel parsing is opt-in

Measured on 4 cores (AMD EPYC 7763, 2M lines / ~230 MB, best of three runs,
Server GC):

| | inline (default) | parallel |
| --- | --- | --- |
| load + index | 620 ms | 705 ms |
| peak working set | 115 MB | 248 MB |

Parsing is not the bottleneck at this core count: `ParsedBufferConsumer` applies
parsed lines to `LineIndex` and `Indexer` on a single thread, so making parsing
faster only moves the queue. The parallel path also pays for one extra copy of
the raw bytes and keeps a whole chunk of parsed buffers alive before merging.

To get an actual win, indexing has to be parallelized as well (per-chunk local
indexes merged in order), which is the natural follow-up. Until then the path is
kept behind the environment variable so it can be measured on machines with many
cores without affecting anyone.

## Indexing

- `IndexKey` hashes all of its components, so the hash is computed once at
  construction instead of on every dictionary probe.
- `Indexer` keeps a per-component registry (`value -> keys`), so
  `GetAllComponents` and `GetIndexCountForComponent` do not walk all keys. Both
  are called from the filter UI for every value.
- `CountIndex.Counts` caches the tail snapshot and invalidates it by version;
  previously every read rebuilt a list over all index keys.
- `Index` stores the number of valid items per chunk - arrays come from
  `ArrayPool` and may be larger than requested, so enumeration used to yield the
  rented tail.

## Search

`Pipeline` reads chunks of up to 4096 lines and searches them on
`ProcessorCount - 1` workers. Before decoding a line, `SearchPrefilter` looks for
a literal that any match must contain directly in the raw bytes:

- the literal is extracted from the pattern once (alternations, character
  classes, inline options and ignore-case disable the filter);
- the filter is conservative - it never rejects data the regex would match
  (`OptimizationTests.PrefilterNeverRejectsMatchingData`);
- it is only enabled for encodings where a substring maps to a contiguous byte
  sequence (single-byte code pages, UTF-8, UTF-16, UTF-32).

The chunk is checked first, so chunks without the literal are skipped without
decoding at all.

## Memory

- Parsed-line buffers are sized with `GetMaxCharCount` for ordinary lines and
  with the exact `GetCharCount` for lines above 8 KB, where the 3x
  over-reservation of UTF-8 becomes real memory.
- `StringPool` caps retention (32 MB worth of buffers per bucket, at most 1024
  buffers) and does not pool buffers above 8 MB at all - they live on the LOH.

## Benchmarks

`LogGrokX.Benchmarks` (BenchmarkDotNet, `--filter *`):

- `LoaderBenchmark` - line splitting only;
- `LineParsingBenchmark` - regex parsing of a single line;
- `IndexingPipelineBenchmark` - end-to-end load and index of 200k lines with
  `MemoryDiagnoser`;
- `SearchBenchmark` - search hot loop with and without the byte prefilter;
- `MergeBenchmark` - merged-view ordering.

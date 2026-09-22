# Performance notes

Notes on the hot paths of LogGrokX and on what the core optimizations actually do.

## Loading pipeline

Loading is split into three stages:

1. `LoaderImpl` reads the file in 1 MB blocks and slices it into lines.
2. `LineProcessor` decodes and parses lines. From 4 logical cores up the lines
   are grouped into line-aligned raw chunks (1 MB) and parsed by thread-pool
   workers, and a dedicated merge thread applies parsed chunks **strictly in
   order**; below that they are parsed inline, on the loader thread, without the
   extra copy. Override with `LOGGROKX_PARALLEL_PARSING=0|1`.
3. `ParsedBufferConsumer` applies parsed buffers to `LineIndex` and `Indexer`.
   It has two modes:
   - sequential (default): one thread walks each buffer and updates the indexes;
   - parallel (`LOGGROKX_PARALLEL_INDEXING=1`): workers do the order-independent
     part (walking line meta information, hashing keys, resolving key numbers and
     index trees, registering new component values) and a single merge thread
     assigns line numbers and appends to the per-key trees in buffer order.
     `Indexer.ResolveKey` / `Indexer.Append` is that split, and new-component
     notifications are deferred to the merge thread so subscribers still see them
     from one thread, in line order.

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

### Measurements

4 cores (AMD EPYC 7763, GitHub runner), 2M lines / ~230 MB, Server GC, best of
three runs after a warm-up run:

| configuration | load + index | peak working set |
| --- | --- | --- |
| `master` | 621 ms | 114 MB |
| branch, sequential parse + sequential index | 753 ms | 221 MB |
| branch, sequential parse + parallel index | 656 ms | 242 MB |
| branch, parallel parse + parallel index | 476 ms | 246 MB |
| **branch, parallel parse + sequential index (default)** | **494 ms** | 238 MB |

Search on the same file (best of three):

| pattern | `master` | branch |
| --- | --- | --- |
| rare literal (1 hit) | 123 ms | 160 ms |
| literal absent | 118 ms | 50 ms |
| regex, 400k hits | 165 ms | 103 ms |

Conclusions:

- parallel **parsing** pays off: 494 ms vs 621 ms (1.26x), so it is on by default
  from 4 cores up;
- parallel **indexing** does not: once parsing is parallel it changes nothing
  (476 ms vs 494 ms, within run-to-run noise) and it costs extra memory, so it
  stays opt-in. What is left on the merge thread is cheap (one array append, one
  `List.Add` per line); the remaining serial cost is the appends themselves,
  which would need per-key partitioning to spread;
- the sequential-parse configuration on the runner is slower than `master`
  (753 ms vs 621 ms) while being on par locally on 2 cores (732 ms vs 734 ms),
  so that gap is not yet explained and is worth re-measuring on real hardware;
- the search prefilter loses a little when the literal is present but rare
  (160 ms vs 123 ms: the chunk is scanned for the literal and then decoded
  anyway) and wins clearly otherwise.

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

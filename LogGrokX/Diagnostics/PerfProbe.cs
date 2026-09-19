using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LogGrokX.Diagnostics
{
    public static class PerfProbe
    {
        public static readonly bool Enabled =
            Environment.GetEnvironmentVariable("LOGGROKX_PERF") == "1";

        private static readonly Logger Log = Logger.Get("PerfProbe");

        private static long _measureTicks;
        private static long _arrangeTicks;
        private static long _renderTicks;
        private static long _textMeasureTicks;
        private static long _elementTicks;
        private static long _scrollTicks;
        private static long _scrollMaxTicks;
        private static long _elementAllocBytes;
        private static long _uiAllocBytes;
        private static readonly System.Collections.Generic.Dictionary<string, long> _allocSites = new();
        private static int _measureCount;
        private static int _arrangeCount;
        private static int _renderCount;
        private static int _textMeasureCount;
        private static int _elementCount;
        private static int _scrollCount;
        private static long _pingTotalTicks;
        private static long _pingMaxTicks;
        private static int _pingCount;
        private static int _gc0;
        private static int _gc1;
        private static int _gc2;
        private static long _allocatedBytes;
        private static TimeSpan _gcPause;
        private static long _frameTotalTicks;
        private static long _frameMaxTicks;
        private static int _frameCount;
        private static long _lastFrameTimestamp;
        private static Dispatcher? _dispatcher;
        private static DispatcherTimer? _timer;

        public static void Start()
        {
            if (!Enabled)
                return;

            var tier = RenderCapability.Tier >> 16;
            Log.Info("perf env: renderTier={0} renderMode={1} wheelLines={2}",
                tier, RenderOptions.ProcessRenderMode, SystemParameters.WheelScrollLines);

            _dispatcher = Dispatcher.CurrentDispatcher;
            _gc0 = GC.CollectionCount(0);
            _gc1 = GC.CollectionCount(1);
            _gc2 = GC.CollectionCount(2);
            _allocatedBytes = GC.GetTotalAllocatedBytes(false);
            _gcPause = GC.GetTotalPauseDuration();
            _uiAllocBytes = GC.GetAllocatedBytesForCurrentThread();

            CompositionTarget.Rendering += OnRendering;

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(
                    double.TryParse(Environment.GetEnvironmentVariable("LOGGROKX_PERF_INTERVAL"), out var sec) ? sec : 1)
            };
            _timer.Tick += (_, _) => System.Threading.Tasks.Task.Run(Report);
            _timer.Start();

            new Thread(PingLoop) { IsBackground = true, Name = "PerfProbePing" }.Start();
        }

        private static void PingLoop()
        {
            while (true)
            {
                Thread.Sleep(15);
                var dispatcher = _dispatcher;
                if (dispatcher == null)
                    continue;

                var start = Stopwatch.GetTimestamp();
                using var done = new ManualResetEventSlim(false);
                dispatcher.BeginInvoke(new Action(done.Set), DispatcherPriority.Send);
                if (!done.Wait(2000))
                    continue;

                var elapsed = Stopwatch.GetTimestamp() - start;
                Interlocked.Add(ref _pingTotalTicks, elapsed);
                Interlocked.Increment(ref _pingCount);

                long current;
                while (elapsed > (current = Interlocked.Read(ref _pingMaxTicks)))
                {
                    if (Interlocked.CompareExchange(ref _pingMaxTicks, elapsed, current) == current)
                        break;
                }
            }
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            var now = Stopwatch.GetTimestamp();
            if (_lastFrameTimestamp != 0)
            {
                var elapsed = now - _lastFrameTimestamp;
                _frameTotalTicks += elapsed;
                _frameCount++;

                long current;
                while (elapsed > (current = Interlocked.Read(ref _frameMaxTicks)))
                {
                    if (Interlocked.CompareExchange(ref _frameMaxTicks, elapsed, current) == current)
                        break;
                }

                if (Enabled && elapsed > Stopwatch.Frequency * 0.05)
                    Log.Info($"perf hitch {elapsed * 1000.0 / Stopwatch.Frequency:F0}ms at {DateTime.Now:HH:mm:ss.fff}");
            }

            _lastFrameTimestamp = now;
        }

        public static void RecordMeasure(long startTimestamp)
        {
            if (!Enabled) return;
            _measureTicks += Stopwatch.GetTimestamp() - startTimestamp;
            _measureCount++;
        }

        public static void RecordArrange(long startTimestamp)
        {
            if (!Enabled) return;
            _arrangeTicks += Stopwatch.GetTimestamp() - startTimestamp;
            _arrangeCount++;
        }

        public static void RecordRender(long startTimestamp)
        {
            if (!Enabled) return;
            _renderTicks += Stopwatch.GetTimestamp() - startTimestamp;
            _renderCount++;
        }

        public static void RecordTextMeasure(long startTimestamp)
        {
            if (!Enabled) return;
            _textMeasureTicks += Stopwatch.GetTimestamp() - startTimestamp;
            _textMeasureCount++;
        }

        public static void RecordElement(long startTimestamp)
        {
            if (!Enabled) return;
            _elementTicks += Stopwatch.GetTimestamp() - startTimestamp;
            _elementCount++;
        }

        public static void RecordElementAlloc(long bytes)
        {
            if (!Enabled) return;
            _elementAllocBytes += bytes;
        }

        public static void RecordAlloc(string site, long bytes)
        {
            if (!Enabled) return;
            lock (_allocSites)
            {
                _allocSites.TryGetValue(site, out var existing);
                _allocSites[site] = existing + bytes;
            }
        }

        public static long AllocMark() => Enabled ? GC.GetAllocatedBytesForCurrentThread() : 0;

        public static void RecordAlloc(string site, long start, long end) =>
            RecordAlloc(site, end - start);

        public static void RecordScroll(long startTimestamp)
        {
            if (!Enabled) return;
            var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            _scrollTicks += elapsed;
            _scrollCount++;

            long current;
            while (elapsed > (current = Interlocked.Read(ref _scrollMaxTicks)))
            {
                if (Interlocked.CompareExchange(ref _scrollMaxTicks, elapsed, current) == current)
                    break;
            }
        }

        private static void Report()
        {
            var pingAvg = _pingCount > 0 ? TicksToMs(_pingTotalTicks) / _pingCount : 0;
            var pingMax = TicksToMs(Interlocked.Read(ref _pingMaxTicks));

            var gc0 = GC.CollectionCount(0) - _gc0;
            var gc1 = GC.CollectionCount(1) - _gc1;
            var gc2 = GC.CollectionCount(2) - _gc2;
            var allocatedMb = (GC.GetTotalAllocatedBytes(false) - _allocatedBytes) / (1024.0 * 1024.0);
            var gcPauseMs = (GC.GetTotalPauseDuration() - _gcPause).TotalMilliseconds;
            var uiAllocMb = (GC.GetAllocatedBytesForCurrentThread() - _uiAllocBytes) / (1024.0 * 1024.0);
            var frameAvg = _frameCount > 0 ? TicksToMs(_frameTotalTicks) / _frameCount : 0;
            var frameMax = TicksToMs(Interlocked.Read(ref _frameMaxTicks));

            Log.Info(
                "perf frame={0:F1}ms avg/{1:F1}ms max/{2} n | ping={3:F2}ms avg/{4:F2}ms max/{5} n | scroll={6:F2}ms avg/{7:F2}ms max/{8} n | gc={9}/{10}/{11} pause={12:F1}ms alloc={13:F1}MB ui={14:F2}MB el={15:F2}MB | measure={16:F2}ms/{17} | arrange={18:F2}ms/{19} | render={20:F2}ms/{21} | textMeasure={22:F2}ms/{23} | element={24:F2}ms/{25}",
                frameAvg,
                frameMax,
                _frameCount,
                pingAvg,
                pingMax,
                _pingCount,
                _scrollCount > 0 ? TicksToMs(_scrollTicks) / _scrollCount : 0,
                TicksToMs(Interlocked.Read(ref _scrollMaxTicks)),
                _scrollCount,
                gc0, gc1, gc2,
                gcPauseMs,
                allocatedMb,
                uiAllocMb,
                _elementAllocBytes / (1024.0 * 1024.0),
                TicksToMs(_measureTicks),
                _measureCount,
                TicksToMs(_arrangeTicks),
                _arrangeCount,
                TicksToMs(_renderTicks),
                _renderCount,
                TicksToMs(_textMeasureTicks),
                _textMeasureCount,
                TicksToMs(_elementTicks),
                _elementCount);

            _measureTicks = _arrangeTicks = _renderTicks = _textMeasureTicks = _elementTicks = _scrollTicks = 0;
            _measureCount = _arrangeCount = _renderCount = _textMeasureCount = _elementCount = _scrollCount = 0;
            _elementAllocBytes = 0;
            _frameTotalTicks = 0;
            _frameMaxTicks = 0;
            _frameCount = 0;

            if (_allocSites.Count > 0)
            {
                var sites = new System.Text.StringBuilder("perf alloc sites: ");
                lock (_allocSites)
                {
                    foreach (var pair in _allocSites)
                        sites.Append(pair.Key).Append('=').Append(pair.Value / (1024.0 * 1024.0)).Append("MB ");
                    _allocSites.Clear();
                }

                Log.Info(sites.ToString());
            }
            _scrollMaxTicks = 0;
            _pingTotalTicks = 0;
            _pingMaxTicks = 0;
            _pingCount = 0;
            _gc0 = GC.CollectionCount(0);
            _gc1 = GC.CollectionCount(1);
            _gc2 = GC.CollectionCount(2);
            _allocatedBytes = GC.GetTotalAllocatedBytes(false);
            _gcPause = GC.GetTotalPauseDuration();
            _uiAllocBytes = GC.GetAllocatedBytesForCurrentThread();
        }

        private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}

namespace LogGrokX.Data
{
    public readonly struct MergeSource
    {
        public MergeSource(int lineCount, TimeIndex timeIndex)
        {
            LineCount = lineCount;
            TimeIndex = timeIndex;
            HasTime = timeIndex.HasTime && timeIndex.IsMonotonic && timeIndex.Count >= lineCount;
        }

        public int LineCount { get; }

        public TimeIndex TimeIndex { get; }

        public bool HasTime { get; }
    }
}
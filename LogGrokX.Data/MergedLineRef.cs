namespace LogGrokX.Data
{
    public readonly struct MergedLineRef
    {
        public MergedLineRef(int sourceIndex, int lineNumber, long ticks)
        {
            SourceIndex = sourceIndex;
            LineNumber = lineNumber;
            Ticks = ticks;
        }

        public int SourceIndex { get; }

        public int LineNumber { get; }

        public long Ticks { get; }
    }
}
using System.Windows.Media;

namespace LogGrokX.Controls
{
    public sealed class TimelineSegment
    {
        public TimelineSegment(int startLine, int endLine, Brush brush)
        {
            StartLine = startLine;
            EndLine = endLine;
            Brush = brush;
        }

        public int StartLine { get; }

        public int EndLine { get; }

        public Brush Brush { get; }
    }
}
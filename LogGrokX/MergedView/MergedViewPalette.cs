using System.Windows.Media;

namespace LogGrokX.MergedView
{
    internal static class MergedViewPalette
    {
        private const byte RowAlpha = 0x30;
        private const byte TimelineAlpha = 0x66;

        private static readonly Color[] Colors =
        {
            Color.FromRgb(0x4C, 0x8B, 0xF0),
            Color.FromRgb(0x3C, 0xB3, 0x7A),
            Color.FromRgb(0xE0, 0x8A, 0x3C),
            Color.FromRgb(0xB0, 0x6C, 0xD8),
            Color.FromRgb(0xD8, 0x5C, 0x6C),
            Color.FromRgb(0x3C, 0xA8, 0xC0),
            Color.FromRgb(0xC0, 0xA8, 0x3C),
            Color.FromRgb(0x8A, 0x8A, 0x8A)
        };

        private static readonly SolidColorBrush[] RowBrushes = CreateBrushes(RowAlpha);
        private static readonly SolidColorBrush[] TimelineBrushes = CreateBrushes(TimelineAlpha);

        public static int Count => Colors.Length;

        public static SolidColorBrush GetRowBrush(int index) => RowBrushes[Normalize(index)];

        public static SolidColorBrush GetTimelineBrush(int index) => TimelineBrushes[Normalize(index)];

        private static int Normalize(int index) => ((index % Colors.Length) + Colors.Length) % Colors.Length;

        private static SolidColorBrush[] CreateBrushes(byte alpha)
        {
            var brushes = new SolidColorBrush[Colors.Length];
            for (var i = 0; i < Colors.Length; i++)
            {
                var color = Colors[i];
                var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
                brush.Freeze();
                brushes[i] = brush;
            }

            return brushes;
        }
    }
}
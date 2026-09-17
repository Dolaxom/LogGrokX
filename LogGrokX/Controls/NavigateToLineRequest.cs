using System;

namespace LogGrokX.Controls
{
    public class NavigateToLineRequest
    {
        public event Action<int, bool>? Navigate;

        public void Raise(int lineNumber, bool center = false)
        {
            Navigate?.Invoke(lineNumber, center);
        }
    }
}

using System;
using System.Collections.Generic;
using LogGrokX.Data.Virtualization;

namespace LogGrokX.MergedView
{
    internal sealed class ListItemProvider<T> : IItemProvider<T>
    {
        private readonly IReadOnlyList<T> _items;

        public ListItemProvider(IReadOnlyList<T> items)
        {
            _items = items;
        }

        public int Count => _items.Count;

        public void Fetch(int start, Span<T> values)
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = _items[start + i];
        }
    }
}
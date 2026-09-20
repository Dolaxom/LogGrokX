using System;
using System.ComponentModel;

namespace LogGrokX.Search
{
    public interface ISearchDocument : INotifyPropertyChanged, IDisposable
    {
        SearchPattern SearchPattern { get; set; }

        string Title { get; }

        string MatchCounterText { get; }

        bool[] MatchBuckets { get; }

        int CurrentMatchLine { get; }

        Action<int>? NavigateToIndexRequested { get; set; }

        void FindNext();

        void FindPrevious();

        void FindNext(int anchorOriginalLine);

        void FindPrevious(int anchorOriginalLine);
    }
}
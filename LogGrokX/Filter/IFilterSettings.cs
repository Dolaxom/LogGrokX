using System;
using System.Collections.Generic;

namespace LogGrokX.Filter
{
    public interface IFilterSettings
    {
        bool HaveExclusions { get; }

        IReadOnlyDictionary<int, IEnumerable<string>> Exclusions { get; }

        event Action? ExclusionsChanged;

        bool this[(int component, string value) arg] { get; set; }

        void AddExclusions(int indexedComponent, IEnumerable<string> componentValuesToExclude);

        void ExcludeAllExcept(int indexedComponent, IEnumerable<string> componentValuesToInclude);

        void ClearAllExclusions();

        void SetExclusions(int indexedComponent, IEnumerable<string> componentValuesToExclude);
    }
}
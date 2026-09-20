using System;
using System.Collections.Generic;

namespace LogGrokX.Data.Index;

public interface IComponentIndexer
{
    IEnumerable<string> GetAllComponents(int componentNumber);

    int GetIndexCountForComponent(int componentIndex, string componentValue);

    event Action<int, string>? ComponentAdded;
}
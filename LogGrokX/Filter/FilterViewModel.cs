using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using LogGrokX.Data;
using LogGrokX.Data.Index;

namespace LogGrokX.Filter
{
    public class FilterViewModel : ViewModelBase
    {
        private readonly IFilterSettings _filterSettings;
        private readonly IComponentIndexer _indexer;
        private string? _textFilter;
        private readonly int _indexedFieldIndex;

        public FilterViewModel(
            string fieldName,
            IFilterSettings filterSettings,
            IComponentIndexer indexer,
            LogMetaInformation metaInformation)
        {
            
            _filterSettings = filterSettings;
            _filterSettings.ExclusionsChanged += () => InvokePropertyChanged(nameof(IsFilterApplied));
            
            _indexer = indexer;
            _indexedFieldIndex = metaInformation.GetIndexedFieldIndexByName(fieldName);

            var fieldValues =
                _indexer.GetAllComponents(_indexedFieldIndex);


            Elements = new ObservableCollection<ElementViewModel>(
                fieldValues.Select(CreateElementViewModel));

            _indexer.ComponentAdded += OnNewComponentAdded;
        }

        private readonly ConcurrentBag<(int componentNumber, string value)> _newComponentsQueue = new();
        private DispatcherOperation? _addComponentDispatcherOperation;

        private ElementViewModel CreateElementViewModel(string fieldValue)
        {
            var newElementViewModel =
                new ElementViewModel(
                    fieldValue,
                    _indexedFieldIndex,
                    _filterSettings,
                    () => _indexer.GetIndexCountForComponent(_indexedFieldIndex, fieldValue));

            return newElementViewModel;
        }

        private void OnNewComponentAdded(int componentIndex, string value)
        {
            if (componentIndex != _indexedFieldIndex)
            {
                return;
            }

            _newComponentsQueue.Add((componentIndex, value));

            void ProcessNewComponents()
            {
                while (_newComponentsQueue.TryTake(out var valueTuple))
                {
                    var (componentNumber, componentValue) = valueTuple;
                    var newElement = CreateElementViewModel(componentValue);
                    Elements.Add(newElement);
                }
            }

            _addComponentDispatcherOperation ??=
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ProcessNewComponents();
                    _addComponentDispatcherOperation = null;
                    ProcessNewComponents();
                });
        }

        public string? TextFilter
        {
            get => _textFilter;
            set => SetAndRaiseIfChanged(ref _textFilter, value);
        }

        public ObservableCollection<ElementViewModel> Elements { get; }

        public bool IsFilterApplied => 
            _filterSettings.Exclusions.TryGetValue(_indexedFieldIndex, out var exclusionList) 
                && exclusionList.Any();

        public DelegateCommand DeselectAllCommand =>
            new(() => _filterSettings.ExcludeAllExcept(_indexedFieldIndex,
                Enumerable.Empty<string>()));

        public DelegateCommand SelectAllCommand =>
            new(() => _filterSettings.SetExclusions(_indexedFieldIndex,
                Enumerable.Empty<string>()));

        public DelegateCommand SelectOnlySearchResultsCommand =>
            DelegateCommand.Create<IEnumerable>(SelectOnlySearchResults);

        private void SelectOnlySearchResults(IEnumerable items)
        {
            _filterSettings.ExcludeAllExcept(_indexedFieldIndex,
                items.Cast<ElementViewModel>().Select(item => item.Name));
        }
    }
}
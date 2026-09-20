using System.Windows;
using System.Windows.Controls;
using LogGrokX.MergedView;

namespace LogGrokX.Search
{
    public sealed class SearchResultsViewTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
        {
            var key = item switch
            {
                MergedSearchDocumentViewModel => "MergedSearchDocumentViewTemplate",
                SearchDocumentViewModel => "SearchDocumentViewTemplate",
                _ => null
            };

            if (key == null)
                return base.SelectTemplate(item, container);

            return (Application.Current?.MainWindow?.TryFindResource(key)
                    ?? Application.Current?.TryFindResource(key)) as DataTemplate;
        }
    }
}
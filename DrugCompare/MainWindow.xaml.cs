using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using DrugCompare.ViewModels;

namespace DrugCompare;

public partial class MainWindow : Window
{
    private bool _isNavigationCollapsed;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        MainTabs.SelectedIndex = 0;
        UpdateNavigationState();
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.Tag is null)
        {
            return;
        }

        if (!int.TryParse(element.Tag.ToString(), out var tabIndex))
        {
            return;
        }

        if (tabIndex >= 0 && tabIndex < MainTabs.Items.Count)
            MainTabs.SelectedIndex = tabIndex;
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, MainTabs)) UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        if (MainTabs is null || ExpandedNavigation is null || CollapsedNavigation is null) return;
        foreach (var button in NavigationButtons(ExpandedNavigation).Concat(NavigationButtons(CollapsedNavigation)))
            button.SetCurrentValue(Selector.IsSelectedProperty,
                int.TryParse(button.Tag?.ToString(), out var index) && index == MainTabs.SelectedIndex);
    }

    private static IEnumerable<Button> NavigationButtons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Button button) yield return button;
            foreach (var descendant in NavigationButtons(child)) yield return descendant;
        }
    }

    private void ToggleNavigation_Click(object sender, RoutedEventArgs e)
    {
        _isNavigationCollapsed = !_isNavigationCollapsed;
        NavigationToggle.Focus();
        NavigationToggle.ToolTip = _isNavigationCollapsed ? "Rozwiń menu" : "Zwiń menu";
        AutomationProperties.SetName(NavigationToggle, _isNavigationCollapsed ? "Rozwiń menu nawigacyjne" : "Zwiń menu nawigacyjne");
        NavigationNotice.Visibility = _isNavigationCollapsed ? Visibility.Collapsed : Visibility.Visible;

        NavigationColumn.Width = _isNavigationCollapsed
            ? new GridLength(64)
            : new GridLength(270);

        ExpandedNavigation.Visibility = _isNavigationCollapsed
            ? Visibility.Collapsed
            : Visibility.Visible;

        CollapsedNavigation.Visibility = _isNavigationCollapsed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}

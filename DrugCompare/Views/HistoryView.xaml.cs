using System.Windows.Controls;
using DrugCompare.ViewModels;

namespace DrugCompare.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => RefreshWhenVisible();
        DataContextChanged += (_, _) => RefreshWhenVisible();
    }

    private void RefreshWhenVisible()
    {
        if (IsVisible && DataContext is MainViewModel vm && vm.LoadHistoryCommand.CanExecute(null))
            vm.LoadHistoryCommand.Execute(null);
    }
}

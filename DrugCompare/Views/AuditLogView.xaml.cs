using System.Windows;
using System.Windows.Controls;
using DrugCompare.ViewModels;

namespace DrugCompare.Views;

public partial class AuditLogView : UserControl
{
    public AuditLogView()
    {
        InitializeComponent();
        IsVisibleChanged += AuditLogView_IsVisibleChanged;
        DataContextChanged += (_, _) => RefreshWhenVisible();
    }

    private void AuditLogView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshWhenVisible();
    }

    private void RefreshWhenVisible()
    {
        if (IsVisible && DataContext is MainViewModel vm && vm.LoadAuditLogsCommand.CanExecute(null))
            vm.LoadAuditLogsCommand.Execute(null);
    }
}

using DrugCompare.ViewModels;
using System.Windows;

namespace DrugCompare;

public partial class DatabaseStatsWindow : Window
{
    public DatabaseStatsWindow(DatabaseStatsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
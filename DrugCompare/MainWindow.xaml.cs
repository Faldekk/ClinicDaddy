using DrugCompare.ViewModels;
using System.Windows;

namespace DrugCompare;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
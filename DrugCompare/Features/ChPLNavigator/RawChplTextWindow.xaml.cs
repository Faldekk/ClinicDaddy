using System.Windows;

namespace DrugCompare.Features.ChPLNavigator;

public partial class RawChplTextWindow : Window
{
    public RawChplTextWindow(string rawText)
    {
        InitializeComponent();
        RawTextBox.Text = rawText;
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(RawTextBox.Text))
        {
            Clipboard.SetText(RawTextBox.Text);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
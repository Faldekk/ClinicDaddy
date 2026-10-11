using System.Windows.Controls;

namespace DrugCompare.Features.EvidenceAssistant;

public partial class EvidenceAssistantView : UserControl
{
    private bool _statusChecked;
    public EvidenceAssistantView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_statusChecked || DataContext is not EvidenceAssistantViewModel viewModel) return;
            _statusChecked = true;
            await viewModel.CheckOllamaStatusCommand.ExecuteAsync(null);
        };
    }
}

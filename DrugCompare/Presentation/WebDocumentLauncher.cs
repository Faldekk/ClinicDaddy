using System.Diagnostics;

namespace DrugCompare.Presentation;

/// <summary>Only web document URLs may cross the Windows shell boundary.</summary>
public sealed class WebDocumentLauncher
{
    private readonly Action<ProcessStartInfo> _launch;

    public WebDocumentLauncher() : this(info => { Process.Start(info); }) { }

    public WebDocumentLauncher(Action<ProcessStartInfo> launch) => _launch = launch;

    public void Open(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(char.IsControl)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Dokument musi mieć prawidłowy adres HTTP lub HTTPS.");

        _launch(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
    }
}

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DrugCompare.Application.Services.Contracts.Rag;
using Microsoft.Extensions.Configuration;

namespace DrugCompare.Application.Services.Implementations.Rag;

public sealed class OllamaStatusService(HttpClient client, IConfiguration configuration) : IOllamaStatusService
{
    public async Task<OllamaStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        var model = configuration["Rag:Ollama:Model"] ?? "qwen3:8b";
        if (bool.TryParse(configuration["Rag:Ollama:Enabled"], out var enabled) && !enabled)
            return new(OllamaState.Disabled, model, "Generowanie odpowiedzi jest wyłączone. Wyszukiwanie źródeł działa lokalnie.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var endpoint = new Uri(configuration["Rag:Ollama:Endpoint"] ?? "http://127.0.0.1:11434/");
            if (endpoint.Scheme is not ("http" or "https"))
                return new(OllamaState.Error, model, "Adres Ollama musi zaczynać się od http:// lub https://.");
            using var response = await client.GetAsync(new Uri(endpoint, "api/tags"), timeout.Token);
            if (!response.IsSuccessStatusCode)
                return new(OllamaState.Error, model, $"Ollama zwróciła HTTP {(int)response.StatusCode}. Sprawdź jej adres i uruchom ją ponownie.");
            var result = await response.Content.ReadFromJsonAsync<ModelList>(cancellationToken: timeout.Token);
            if (result?.Models is null)
                return new(OllamaState.Error, model, "Serwer zwrócił nieprawidłową listę modeli.");
            var configuredName = Normalize(model);
            var found = result.Models.Any(x => Normalize(x.Name ?? x.Model ?? "") == configuredName);
            return found
                ? new(OllamaState.Ready, model, $"Ollama dostępna • model {model} jest zainstalowany.")
                : new(OllamaState.ModelMissing, model, $"Ollama działa, ale brakuje modelu {model}. Pobierz go poleceniem: ollama pull {model}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(OllamaState.Unavailable, model, "Ollama nie odpowiedziała w ciągu 5 sekund. Uruchom Ollama i sprawdź połączenie ponownie.");
        }
        catch (HttpRequestException)
        {
            return new(OllamaState.Unavailable, model, "Brak połączenia z Ollama. Uruchom aplikację Ollama, a następnie sprawdź połączenie.");
        }
        catch (JsonException)
        {
            return new(OllamaState.Error, model, "Serwer nie zwrócił prawidłowej listy modeli Ollama. Sprawdź skonfigurowany adres.");
        }
        catch (UriFormatException)
        {
            return new(OllamaState.Error, model, "Adres Ollama w konfiguracji jest nieprawidłowy.");
        }
    }

    private static string Normalize(string name) => (name.Contains(':') ? name : name + ":latest").ToLowerInvariant();
    private sealed class ModelList { public List<ModelItem>? Models { get; set; } }
    private sealed class ModelItem { public string? Name { get; set; } public string? Model { get; set; } }
}

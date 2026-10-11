namespace DrugCompare.Application.Services.Contracts.Rag;

public enum OllamaState { Ready, Disabled, Unavailable, ModelMissing, Error }
public sealed record OllamaStatus(OllamaState State, string Model, string Message)
{
    public bool IsReady => State == OllamaState.Ready;
}

public interface IOllamaStatusService
{
    Task<OllamaStatus> CheckAsync(CancellationToken cancellationToken = default);
}

public enum RagFailureKind { Connection, Timeout, ModelMissing, Server, InvalidAnswer, Disabled }
public sealed class RagServiceException(RagFailureKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public RagFailureKind Kind { get; } = kind;
}

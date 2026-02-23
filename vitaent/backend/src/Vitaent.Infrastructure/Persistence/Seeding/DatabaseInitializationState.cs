namespace Vitaent.Infrastructure.Persistence.Seeding;

public sealed class DatabaseInitializationState
{
    public bool IsReady { get; private set; }
    public string? FailureMessage { get; private set; }

    public void MarkReady()
    {
        IsReady = true;
        FailureMessage = null;
    }

    public void MarkFailed(string message)
    {
        IsReady = false;
        FailureMessage = message;
    }
}

namespace AcademicRegistration.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public int BatchSize { get; set; } = 20;

    public int PollingIntervalSeconds { get; set; } = 5;

    public int MaxRetries { get; set; }
}

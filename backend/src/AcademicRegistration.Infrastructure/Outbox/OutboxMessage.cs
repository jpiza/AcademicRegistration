namespace AcademicRegistration.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
        Type = string.Empty;
        EventType = string.Empty;
        PartitionKey = string.Empty;
        Payload = string.Empty;
    }

    private OutboxMessage(
        Guid id,
        string type,
        string eventType,
        string partitionKey,
        string payload,
        DateTime occurredOnUtc)
    {
        Id = id;
        Type = type;
        EventType = eventType;
        PartitionKey = partitionKey;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public string EventType { get; private set; }

    public string PartitionKey { get; private set; }

    public string Payload { get; private set; }

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ProcessedOnUtc { get; private set; }

    public string? Error { get; private set; }

    public int RetryCount { get; private set; }

    public bool IsProcessed => ProcessedOnUtc.HasValue;

    public static OutboxMessage Create(
        Guid id,
        string type,
        string eventType,
        string partitionKey,
        string payload,
        DateTime occurredOnUtc)
    {
        return new OutboxMessage(id, type, eventType, partitionKey, payload, occurredOnUtc);
    }

    public void MarkAsProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        Error = null;
    }

    public void MarkAsFailed(string error)
    {
        RetryCount++;
        Error = error.Length <= 2000 ? error : error[..2000];
    }
}

namespace AcademicRegistration.Notifications.Worker.Configuration;

public sealed class SqsConsumerSettings
{
    public string QueueUrl { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public string? ServiceUrl { get; set; }

    public int MaxNumberOfMessages { get; set; } = 10;

    public int WaitTimeSeconds { get; set; } = 20;

    public int VisibilityTimeoutSeconds { get; set; } = 30;

    public int EmptyQueueDelaySeconds { get; set; } = 2;
}

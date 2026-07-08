namespace AcademicRegistration.Infrastructure.Messaging;

public sealed class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;
}

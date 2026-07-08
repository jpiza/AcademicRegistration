namespace AcademicRegistration.Notifications.Worker.Configuration;

public sealed class KafkaConsumerSettings
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string GroupId { get; set; } = "academic-registration-notifications";
}

namespace AcademicRegistration.Infrastructure.Messaging;

public sealed class EventBridgeSettings
{
    public string EventBusName { get; set; } = "academic-registration";

    public string Source { get; set; } = "academic-registration.api";

    public string Region { get; set; } = "us-east-1";

    public string? ServiceUrl { get; set; }
}

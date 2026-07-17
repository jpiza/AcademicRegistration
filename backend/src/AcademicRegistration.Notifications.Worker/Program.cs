using AcademicRegistration.Notifications.Worker.Configuration;
using AcademicRegistration.Notifications.Worker.Email;
using AcademicRegistration.Notifications.Worker.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<KafkaConsumerSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.BootstrapServers),
        "Kafka BootstrapServers is required")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Topic),
        "Kafka Topic is required")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.GroupId),
        "Kafka GroupId is required")
    .ValidateOnStart();

builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection("Email"))
    .Validate(
        settings => !settings.Enabled || settings.HasSmtpSettings(),
        "Email Host, Port and From are required when Email Enabled is true")
    .Validate(
        settings => !settings.Enabled || !settings.RequireAuthentication || settings.HasAuthenticationSettings(),
        "Email UserName and Password are required when Email Enabled and RequireAuthentication are true")
    .ValidateOnStart();

builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var settings = sp
        .GetRequiredService<IOptions<KafkaConsumerSettings>>()
        .Value;

    var config = new ConsumerConfig
    {
        BootstrapServers = settings.BootstrapServers,
        GroupId = settings.GroupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
        AllowAutoCreateTopics = true,
        ClientId = "academic-registration-notifications-worker"
    };

    ApplyKafkaSecurity(config, settings);

    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<StudentNotificationConsumer>();

await builder.Build().RunAsync();

static void ApplyKafkaSecurity(ClientConfig config, KafkaConsumerSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.SecurityProtocol))
    {
        return;
    }

    config.SecurityProtocol = ParseKafkaEnum<SecurityProtocol>(
        settings.SecurityProtocol,
        nameof(settings.SecurityProtocol));

    if (config.SecurityProtocol is not (SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext))
    {
        return;
    }

    if (string.IsNullOrWhiteSpace(settings.SaslMechanism))
    {
        throw new InvalidOperationException("Kafka SaslMechanism is required when SecurityProtocol uses SASL.");
    }

    if (string.IsNullOrWhiteSpace(settings.SaslUsername))
    {
        throw new InvalidOperationException("Kafka SaslUsername is required when SecurityProtocol uses SASL.");
    }

    if (string.IsNullOrWhiteSpace(settings.SaslPassword))
    {
        throw new InvalidOperationException("Kafka SaslPassword is required when SecurityProtocol uses SASL.");
    }

    config.SaslMechanism = ParseKafkaEnum<SaslMechanism>(
        settings.SaslMechanism,
        nameof(settings.SaslMechanism));
    config.SaslUsername = settings.SaslUsername;
    config.SaslPassword = settings.SaslPassword;
}

static TEnum ParseKafkaEnum<TEnum>(string value, string settingName)
    where TEnum : struct, Enum
{
    var normalized = value
        .Replace("-", string.Empty)
        .Replace("_", string.Empty)
        .Replace(".", string.Empty)
        .Replace(" ", string.Empty);

    if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed))
    {
        return parsed;
    }

    throw new InvalidOperationException($"Kafka {settingName} value '{value}' is not supported.");
}

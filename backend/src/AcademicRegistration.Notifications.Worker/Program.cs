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

    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<StudentNotificationConsumer>();

await builder.Build().RunAsync();

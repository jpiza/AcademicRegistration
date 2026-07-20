using AcademicRegistration.Notifications.Worker.Configuration;
using AcademicRegistration.Notifications.Worker.Email;
using AcademicRegistration.Notifications.Worker.Messaging;
using Amazon;
using Amazon.SQS;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<SqsConsumerSettings>()
    .Bind(builder.Configuration.GetSection("Sqs"))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.QueueUrl),
        "Sqs QueueUrl is required")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Region),
        "Sqs Region is required")
    .Validate(
        settings => settings.MaxNumberOfMessages is >= 1 and <= 10,
        "Sqs MaxNumberOfMessages must be between 1 and 10")
    .Validate(
        settings => settings.WaitTimeSeconds is >= 0 and <= 20,
        "Sqs WaitTimeSeconds must be between 0 and 20")
    .Validate(
        settings => settings.VisibilityTimeoutSeconds > 0,
        "Sqs VisibilityTimeoutSeconds must be greater than zero")
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

builder.Services.AddOptions<TracingSettings>()
    .Bind(builder.Configuration.GetSection("Tracing:XRay"));

if (builder.Configuration.GetValue("Tracing:XRay:Enabled", false))
{
    AWSXRayRecorder.InitializeInstance(builder.Configuration);
    AWSSDKHandler.RegisterXRayForAllServices();
}

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var settings = sp
        .GetRequiredService<IOptions<SqsConsumerSettings>>()
        .Value;

    var config = new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region)
    };

    if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
    {
        config.ServiceURL = settings.ServiceUrl;
        config.AuthenticationRegion = settings.Region;
    }

    return new AmazonSQSClient(config);
});

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<StudentNotificationConsumer>();

await builder.Build().RunAsync();

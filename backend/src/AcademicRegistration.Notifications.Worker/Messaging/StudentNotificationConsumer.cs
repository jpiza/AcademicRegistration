using System.Text.Json;
using AcademicRegistration.Application.IntegrationEvents.Students;
using AcademicRegistration.Notifications.Worker.Configuration;
using AcademicRegistration.Notifications.Worker.Email;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.XRay.Recorder.Core;
using Microsoft.Extensions.Options;
using SqsMessage = Amazon.SQS.Model.Message;

namespace AcademicRegistration.Notifications.Worker.Messaging;

public sealed class StudentNotificationConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAmazonSQS _sqs;
    private readonly IEmailSender _emailSender;
    private readonly SqsConsumerSettings _settings;
    private readonly TracingSettings _tracingSettings;
    private readonly ILogger<StudentNotificationConsumer> _logger;

    public StudentNotificationConsumer(
        IAmazonSQS sqs,
        IEmailSender emailSender,
        IOptions<SqsConsumerSettings> settings,
        IOptions<TracingSettings> tracingSettings,
        ILogger<StudentNotificationConsumer> logger)
    {
        _sqs = sqs;
        _emailSender = emailSender;
        _settings = settings.Value;
        _tracingSettings = tracingSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de notificaciones leyendo mensajes desde SQS: {QueueUrl}", _settings.QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqs.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = _settings.QueueUrl,
                        MaxNumberOfMessages = _settings.MaxNumberOfMessages,
                        WaitTimeSeconds = _settings.WaitTimeSeconds,
                        VisibilityTimeout = _settings.VisibilityTimeoutSeconds,
                        MessageSystemAttributeNames = ["All"],
                        MessageAttributeNames = ["All"]
                    },
                    stoppingToken);

                if (response.Messages.Count == 0)
                {
                    await DelayAfterEmptyReceiveAsync(stoppingToken);
                    continue;
                }

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recibiendo mensajes desde SQS");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(SqsMessage message, CancellationToken cancellationToken)
    {
        var traceSegmentStarted = TryBeginTraceSegment(message);

        try
        {
            if (string.IsNullOrWhiteSpace(message.Body))
            {
                await DeleteMessageAsync(message, cancellationToken);
                return;
            }

            var notification = DeserializeNotification(message.Body);

            if (notification is null)
            {
                _logger.LogWarning(
                    "Mensaje SQS {MessageId} sin contenido de notificacion.",
                    message.MessageId);

                await DeleteMessageAsync(message, cancellationToken);
                return;
            }

            var email = CreateEmail(notification);
            await _emailSender.SendAsync(email, cancellationToken);
            await DeleteMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Notificacion {EventType} enviada a {Email}. SqsMessageId: {MessageId}",
                notification.EventType,
                notification.Email,
                message.MessageId);
        }
        catch (JsonException ex)
        {
            AddTraceException(traceSegmentStarted, ex);
            _logger.LogError(
                ex,
                "Mensaje SQS {MessageId} invalido; se elimina para evitar reprocesamiento.",
                message.MessageId);

            await DeleteMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            AddTraceException(traceSegmentStarted, ex);
            _logger.LogError(
                ex,
                "Error procesando notificacion academica. SqsMessageId: {MessageId}",
                message.MessageId);
        }
        finally
        {
            EndTraceSegment(traceSegmentStarted);
        }
    }

    private StudentNotificationRequestedIntegrationEvent? DeserializeNotification(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("detail", out var detail))
        {
            return detail.Deserialize<StudentNotificationRequestedIntegrationEvent>(JsonOptions);
        }

        return root.Deserialize<StudentNotificationRequestedIntegrationEvent>(JsonOptions);
    }

    private Task DeleteMessageAsync(SqsMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.ReceiptHandle))
        {
            return Task.CompletedTask;
        }

        return _sqs.DeleteMessageAsync(
            _settings.QueueUrl,
            message.ReceiptHandle,
            cancellationToken);
    }

    private Task DelayAfterEmptyReceiveAsync(CancellationToken stoppingToken)
    {
        if (_settings.EmptyQueueDelaySeconds <= 0)
        {
            return Task.CompletedTask;
        }

        return Task.Delay(TimeSpan.FromSeconds(_settings.EmptyQueueDelaySeconds), stoppingToken);
    }

    private bool TryBeginTraceSegment(SqsMessage message)
    {
        if (!_tracingSettings.Enabled)
        {
            return false;
        }

        try
        {
            AWSXRayRecorder.Instance.BeginSegment("AcademicRegistration.Notifications.Worker");
            AWSXRayRecorder.Instance.AddAnnotation("sqsMessageId", message.MessageId ?? "unknown");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo iniciar el segmento X-Ray del worker.");
            return false;
        }
    }

    private static void AddTraceException(bool traceSegmentStarted, Exception exception)
    {
        if (traceSegmentStarted)
        {
            AWSXRayRecorder.Instance.AddException(exception);
        }
    }

    private void EndTraceSegment(bool traceSegmentStarted)
    {
        if (!traceSegmentStarted)
        {
            return;
        }

        try
        {
            AWSXRayRecorder.Instance.EndSegment();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo cerrar el segmento X-Ray del worker.");
        }
    }

    private static EmailMessage CreateEmail(StudentNotificationRequestedIntegrationEvent notification)
    {
        var subject = notification.EventType switch
        {
            StudentNotificationEventTypes.StudentRegistered => "Registro academico confirmado",
            StudentNotificationEventTypes.StudentEnrollmentChanged => "Actualizacion de matricula",
            _ => "Notificacion academica"
        };

        var action = notification.EventType switch
        {
            StudentNotificationEventTypes.StudentRegistered =>
                "Tu registro academico fue creado correctamente.",
            StudentNotificationEventTypes.StudentEnrollmentChanged =>
                "Tu seleccion de materias fue actualizada correctamente.",
            _ => "Se realizo una accion sobre tu registro academico."
        };

        var subjects = notification.Subjects.Count == 0
            ? "No se recibieron materias asociadas."
            : string.Join(
                Environment.NewLine,
                notification.Subjects.Select(subject => $"- {subject.Code} - {subject.Name}"));

        var body = $"""
            Hola {notification.Name},

            {action}

            Materias:
            {subjects}

            Fecha UTC: {notification.OccurredOnUtc:O}
            """;

        return new EmailMessage(notification.Email, subject, body);
    }
}

using System.Text.Json;
using AcademicRegistration.Application.IntegrationEvents.Students;
using AcademicRegistration.Notifications.Worker.Configuration;
using AcademicRegistration.Notifications.Worker.Email;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace AcademicRegistration.Notifications.Worker.Messaging;

public sealed class StudentNotificationConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConsumer<string, string> _consumer;
    private readonly IEmailSender _emailSender;
    private readonly KafkaConsumerSettings _settings;
    private readonly ILogger<StudentNotificationConsumer> _logger;

    public StudentNotificationConsumer(
        IConsumer<string, string> consumer,
        IEmailSender emailSender,
        IOptions<KafkaConsumerSettings> settings,
        ILogger<StudentNotificationConsumer> logger)
    {
        _consumer = consumer;
        _emailSender = emailSender;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_settings.Topic);
        _logger.LogInformation("Consumidor de notificaciones suscrito al topico {Topic}", _settings.Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? consumeResult = null;

                try
                {
                    consumeResult = _consumer.Consume(stoppingToken);

                    if (string.IsNullOrWhiteSpace(consumeResult.Message.Value))
                    {
                        _consumer.Commit(consumeResult);
                        continue;
                    }

                    var notification = JsonSerializer.Deserialize<StudentNotificationRequestedIntegrationEvent>(
                        consumeResult.Message.Value,
                        JsonOptions);

                    if (notification is null)
                    {
                        _logger.LogWarning(
                            "Mensaje Kafka sin contenido de notificacion. Topic: {Topic}, Offset: {Offset}",
                            consumeResult.Topic,
                            consumeResult.Offset.Value);

                        _consumer.Commit(consumeResult);
                        continue;
                    }

                    
                    var email = CreateEmail(notification);
                    await _emailSender.SendAsync(email, stoppingToken);
                    _consumer.Commit(consumeResult);

                    _logger.LogInformation(
                        "Notificacion {EventType} enviada a {Email}. Offset: {Offset}",
                        notification.EventType,
                        notification.Email,
                        consumeResult.Offset.Value);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Mensaje Kafka invalido; se confirma para evitar reprocesamiento.");

                    if (consumeResult is not null)
                    {
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consumiendo mensaje Kafka");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando notificacion academica");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _consumer.Close();
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

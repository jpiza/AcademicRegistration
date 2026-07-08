using System.Text;
using AcademicRegistration.Infrastructure.Outbox;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcademicRegistration.Infrastructure.Messaging;

public sealed class KafkaEventBus : IOutboxMessagePublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaEventBus> _logger;

    public KafkaEventBus(
        IProducer<string, string> producer,
        IOptions<KafkaSettings> settings,
        ILogger<KafkaEventBus> logger)
    {
        _producer = producer;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<KafkaEventMetadata> PublishAsync(
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = outboxMessage.PartitionKey,
                Value = outboxMessage.Payload,
                Headers =
                [
                    new Header("event-id", Encoding.UTF8.GetBytes(outboxMessage.Id.ToString())),
                    new Header("event-type", Encoding.UTF8.GetBytes(outboxMessage.EventType)),
                    new Header("occurred-on-utc", Encoding.UTF8.GetBytes(outboxMessage.OccurredOnUtc.ToString("O")))
                ]
            };

            var result = await _producer.ProduceAsync(_settings.Topic, message, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Evento publicado en Kafka. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, EventType: {EventType}",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    outboxMessage.EventType);
            }

            return new KafkaEventMetadata(
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Error publicando mensaje outbox {MessageId}", outboxMessage.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado publicando mensaje outbox {MessageId}", outboxMessage.Id);
            throw;
        }
    }
}

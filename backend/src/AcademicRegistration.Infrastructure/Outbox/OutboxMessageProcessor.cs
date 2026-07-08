using AcademicRegistration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcademicRegistration.Infrastructure.Outbox;

public sealed class OutboxMessageProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxMessageProcessor> _logger;

    public OutboxMessageProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxMessageProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando mensajes outbox.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AcademicRegistrationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxMessagePublisher>();

        var batchSize = Math.Max(1, _options.BatchSize);
        var messages = await dbContext.OutboxMessages
            .Where(message =>
                message.ProcessedOnUtc == null &&
                (_options.MaxRetries == 0 || message.RetryCount < _options.MaxRetries))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var result = await publisher.PublishAsync(message, cancellationToken);
                message.MarkAsProcessed();

                _logger.LogInformation(
                    "Mensaje outbox {MessageId} publicado en Kafka: {KafkaResult}",
                    message.Id,
                    result.Id);
            }
            catch (Exception ex)
            {
                message.MarkAsFailed(ex.Message);

                _logger.LogError(
                    ex,
                    "No se pudo publicar el mensaje outbox {MessageId}. Intento {RetryCount}",
                    message.Id,
                    message.RetryCount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

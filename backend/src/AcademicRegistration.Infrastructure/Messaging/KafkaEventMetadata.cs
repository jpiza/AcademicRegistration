using AcademicRegistration.Application.Abstractions.Events;

namespace AcademicRegistration.Infrastructure.Messaging;

public sealed record KafkaEventMetadata(string Topic, int Partition, long Offset)
    : EventPublishResult($"topico {Topic} | particion: {Partition} | offset: {Offset}");

using AcademicRegistration.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcademicRegistration.Infrastructure.Persistence.Configurations.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(message => message.Id);

        builder.Ignore(message => message.IsProcessed);

        builder.Property(message => message.Type)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(message => message.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.PartitionKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(message => message.Payload)
            .IsRequired();

        builder.Property(message => message.Error)
            .HasMaxLength(2000);

        builder.HasIndex(message => new { message.ProcessedOnUtc, message.CreatedAtUtc });
    }
}

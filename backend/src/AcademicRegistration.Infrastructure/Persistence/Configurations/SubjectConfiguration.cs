using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcademicRegistration.Infrastructure.Persistence.Configurations;

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");

        builder.HasKey(subject => subject.Id);

        builder.Property(subject => subject.Code)
            .HasConversion(code => code.Value, value => SubjectCode.Create(value))
            .HasMaxLength(12)
            .IsRequired();

        builder.HasIndex(subject => subject.Code)
            .IsUnique();

        builder.Property(subject => subject.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(subject => subject.Credits)
            .HasConversion(credits => credits.Value, value => AcademicCredits.Create(value))
            .IsRequired();

        builder.HasOne(subject => subject.Professor)
            .WithMany("Subjects")
            .HasForeignKey(subject => subject.ProfessorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

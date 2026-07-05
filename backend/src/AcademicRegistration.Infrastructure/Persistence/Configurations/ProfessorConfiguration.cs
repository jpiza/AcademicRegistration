using AcademicRegistration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcademicRegistration.Infrastructure.Persistence.Configurations;

internal sealed class ProfessorConfiguration : IEntityTypeConfiguration<Professor>
{
    public void Configure(EntityTypeBuilder<Professor> builder)
    {
        builder.ToTable("Professors");

        builder.HasKey(professor => professor.Id);

        builder.Property(professor => professor.FullName)
            .HasMaxLength(120)
            .IsRequired();
    }
}

using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcademicRegistration.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(student => student.Id);

        builder.Property(student => student.Name)
            .HasConversion(name => name.Value, value => StudentName.Create(value))
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(student => student.Email)
            .HasConversion(email => email.Value, value => EmailAddress.Create(value))
            .HasMaxLength(180)
            .IsRequired();

        builder.HasIndex(student => student.Email)
            .IsUnique();

        builder.Property(student => student.DocumentNumber)
            .HasConversion(document => document.Value, value => DocumentNumber.Create(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(student => student.DocumentNumber)
            .IsUnique();

        builder.Property(student => student.CreatedAtUtc)
            .IsRequired();

        builder.Property(student => student.UpdatedAtUtc);

        builder.HasMany(student => student.Enrollments)
            .WithOne()
            .HasForeignKey(enrollment => enrollment.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(student => student.Enrollments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

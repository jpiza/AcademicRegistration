using AcademicRegistration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcademicRegistration.Infrastructure.Persistence.Configurations;

internal sealed class StudentSubjectConfiguration : IEntityTypeConfiguration<StudentSubject>
{
    public void Configure(EntityTypeBuilder<StudentSubject> builder)
    {
        builder.ToTable("StudentSubjects");

        builder.HasKey(enrollment => new { enrollment.StudentId, enrollment.SubjectId });

        builder.Property(enrollment => enrollment.EnrolledAtUtc)
            .IsRequired();

        builder.HasOne(enrollment => enrollment.Subject)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

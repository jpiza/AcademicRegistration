namespace AcademicRegistration.Domain.Entities;

public sealed class StudentSubject
{
    private StudentSubject()
    {
    }

    private StudentSubject(Guid studentId, Guid subjectId)
    {
        StudentId = studentId;
        SubjectId = subjectId;
        EnrolledAtUtc = DateTime.UtcNow;
    }

    public Guid StudentId { get; private set; }

    public Guid SubjectId { get; private set; }

    public DateTime EnrolledAtUtc { get; private set; }

    public Subject? Subject { get; private set; }

    internal static StudentSubject Enroll(Guid studentId, Guid subjectId)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("El estudiante es requerido.", nameof(studentId));
        }

        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("La materia es requerida.", nameof(subjectId));
        }

        return new StudentSubject(studentId, subjectId);
    }
}

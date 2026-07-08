using AcademicRegistration.Domain.Events;
using AcademicRegistration.Domain.Primitives;
using AcademicRegistration.Domain.ValueObjects;

namespace AcademicRegistration.Domain.Entities;

public sealed class Student : AggregateRoot
{
    public const int RequiredSubjectCount = 3;

    private readonly List<StudentSubject> _enrollments = [];

    private Student()
    {
        Name = null!;
        Email = null!;
        DocumentNumber = null!;
    }

    private Student(Guid id, StudentName name, EmailAddress email, DocumentNumber documentNumber)
    {
        Id = id;
        Name = name;
        Email = email;
        DocumentNumber = documentNumber;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public StudentName Name { get; private set; }

    public EmailAddress Email { get; private set; }

    public DocumentNumber DocumentNumber { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<StudentSubject> Enrollments => _enrollments.AsReadOnly();

    public static Student Register(
        StudentName name,
        EmailAddress email,
        DocumentNumber documentNumber,
        IReadOnlyCollection<Subject> selectedSubjects)
    {
        var student = new Student(Guid.NewGuid(), name, email, documentNumber);

        student.ChangeEnrollment(selectedSubjects, raiseEvent: false);
        student.Raise(new StudentRegisteredEvent(
            student.Id,
            student.Name.Value,
            student.Email.Value,
            ToEnrollmentSubjects(selectedSubjects)));

        return student;
    }

    public void UpdateProfile(StudentName name, EmailAddress email, DocumentNumber documentNumber)
    {
        Name = name;
        Email = email;
        DocumentNumber = documentNumber;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangeEnrollment(IReadOnlyCollection<Subject> selectedSubjects)
    {
        ChangeEnrollment(selectedSubjects, raiseEvent: true);
    }

    private void ChangeEnrollment(IReadOnlyCollection<Subject> selectedSubjects, bool raiseEvent)
    {
        ValidateSelection(selectedSubjects);

        _enrollments.Clear();

        foreach (var subject in selectedSubjects)
        {
            _enrollments.Add(StudentSubject.Enroll(Id, subject.Id));
        }

        UpdatedAtUtc = DateTime.UtcNow;

        if (raiseEvent)
        {
            Raise(new StudentEnrollmentChangedEvent(
                Id,
                Name.Value,
                Email.Value,
                ToEnrollmentSubjects(selectedSubjects)));
        }
    }

    private static IReadOnlyCollection<StudentEnrollmentSubject> ToEnrollmentSubjects(
        IReadOnlyCollection<Subject> subjects)
    {
        return subjects
            .Select(subject => new StudentEnrollmentSubject(
                subject.Id,
                subject.Code.Value,
                subject.Name))
            .ToArray();
    }

    private static void ValidateSelection(IReadOnlyCollection<Subject> selectedSubjects)
    {
        if (selectedSubjects.Count != RequiredSubjectCount)
        {
            throw new DomainRuleException(
                "Students.SubjectCount",
                "El estudiante debe seleccionar exactamente 3 materias.");
        }

        if (selectedSubjects.Select(subject => subject.Id).Distinct().Count() != selectedSubjects.Count)
        {
            throw new DomainRuleException(
                "Students.DuplicateSubject",
                "El estudiante no puede seleccionar la misma materia mas de una vez.");
        }

        if (selectedSubjects.Any(subject => subject.Credits.Value != AcademicCredits.CreditsPerSubject))
        {
            throw new DomainRuleException(
                "Subjects.InvalidCredits",
                "Cada materia debe equivaler a 3 creditos.");
        }

        if (selectedSubjects.Select(subject => subject.ProfessorId).Distinct().Count() != selectedSubjects.Count)
        {
            throw new DomainRuleException(
                "Students.DuplicateProfessor",
                "El estudiante no podra tener clases con el mismo profesor.");
        }
    }
}

using AcademicRegistration.Domain.ValueObjects;

namespace AcademicRegistration.Domain.Entities;

public sealed class Subject
{
    private Subject()
    {
        Code = null!;
        Name = string.Empty;
        Credits = null!;
    }

    public Subject(Guid id, SubjectCode code, string name, Guid professorId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la materia es requerido.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la materia es requerido.", nameof(name));
        }

        if (professorId == Guid.Empty)
        {
            throw new ArgumentException("El profesor es requerido.", nameof(professorId));
        }

        Id = id;
        Code = code;
        Name = name.Trim();
        ProfessorId = professorId;
        Credits = AcademicCredits.Create(AcademicCredits.CreditsPerSubject);
    }

    public Guid Id { get; private set; }

    public SubjectCode Code { get; private set; }

    public string Name { get; private set; }

    public AcademicCredits Credits { get; private set; }

    public Guid ProfessorId { get; private set; }

    public Professor? Professor { get; private set; }
}

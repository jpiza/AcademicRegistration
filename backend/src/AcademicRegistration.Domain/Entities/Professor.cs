namespace AcademicRegistration.Domain.Entities;

public sealed class Professor
{
    private readonly List<Subject> _subjects = [];

    private Professor()
    {
        FullName = string.Empty;
    }

    public Professor(Guid id, string fullName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador del profesor es requerido.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("El nombre del profesor es requerido.", nameof(fullName));
        }

        Id = id;
        FullName = fullName.Trim();
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; }

    public IReadOnlyCollection<Subject> Subjects => _subjects.AsReadOnly();
}

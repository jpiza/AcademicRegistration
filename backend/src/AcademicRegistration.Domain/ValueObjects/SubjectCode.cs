namespace AcademicRegistration.Domain.ValueObjects;

public sealed record SubjectCode
{
    public string Value { get; }

    private SubjectCode(string value)
    {
        Value = value;
    }

    public static SubjectCode Create(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length is < 3 or > 12)
        {
            throw new ArgumentException("El codigo de la materia debe tener entre 3 y 12 caracteres.", nameof(value));
        }

        return new SubjectCode(normalized);
    }

    public override string ToString() => Value;
}

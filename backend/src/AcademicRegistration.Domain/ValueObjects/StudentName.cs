namespace AcademicRegistration.Domain.ValueObjects;

public sealed record StudentName
{
    public string Value { get; }

    private StudentName(string value)
    {
        Value = value;
    }

    public static StudentName Create(string value)
    {
        var normalized = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length is < 3 or > 120)
        {
            throw new ArgumentException("El nombre debe tener entre 3 y 120 caracteres.", nameof(value));
        }

        return new StudentName(normalized);
    }

    public override string ToString() => Value;
}

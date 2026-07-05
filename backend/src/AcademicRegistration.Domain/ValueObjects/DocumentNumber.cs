namespace AcademicRegistration.Domain.ValueObjects;

public sealed record DocumentNumber
{
    public string Value { get; }

    private DocumentNumber(string value)
    {
        Value = value;
    }

    public static DocumentNumber Create(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length is < 5 or > 20)
        {
            throw new ArgumentException("El documento debe tener entre 5 y 20 caracteres.", nameof(value));
        }

        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("El documento solo puede contener letras, numeros o guiones.", nameof(value));
        }

        return new DocumentNumber(normalized);
    }

    public override string ToString() => Value;
}

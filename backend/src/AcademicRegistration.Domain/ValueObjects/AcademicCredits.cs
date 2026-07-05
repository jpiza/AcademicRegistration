namespace AcademicRegistration.Domain.ValueObjects;

public sealed record AcademicCredits
{
    public const int CreditsPerSubject = 3;

    public int Value { get; }

    private AcademicCredits(int value)
    {
        Value = value;
    }

    public static AcademicCredits Create(int value)
    {
        if (value != CreditsPerSubject)
        {
            throw new ArgumentException("Cada materia debe equivaler a 3 creditos.", nameof(value));
        }

        return new AcademicCredits(value);
    }

    public override string ToString() => Value.ToString();
}

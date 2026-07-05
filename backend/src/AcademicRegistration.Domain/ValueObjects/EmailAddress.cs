using System.Net.Mail;

namespace AcademicRegistration.Domain.ValueObjects;

public sealed record EmailAddress
{
    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public static EmailAddress Create(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("El correo es requerido.", nameof(value));
        }

        try
        {
            var address = new MailAddress(normalized);

            if (address.Address != normalized)
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("El correo no tiene un formato valido.", nameof(value));
        }

        return new EmailAddress(normalized);
    }

    public override string ToString() => Value;
}

namespace AcademicRegistration.Domain.Primitives;

public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

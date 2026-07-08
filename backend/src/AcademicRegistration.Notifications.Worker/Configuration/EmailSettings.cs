namespace AcademicRegistration.Notifications.Worker.Configuration;

public sealed class EmailSettings
{
    public bool Enabled { get; set; }

    public string From { get; set; } = "no-reply@academic-registration.local";

    public string FromName { get; set; } = "Academic Registration";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;

    public bool RequireAuthentication { get; set; } = true;

    public bool HasSmtpSettings()
    {
        return !string.IsNullOrWhiteSpace(From)
               && !string.IsNullOrWhiteSpace(Host)
               && Port > 0;
    }

    public bool HasAuthenticationSettings()
    {
        return !string.IsNullOrWhiteSpace(UserName)
               && !string.IsNullOrWhiteSpace(Password);
    }
}

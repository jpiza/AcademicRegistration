namespace AcademicRegistration.Infrastructure.Persistence.Configurations;

public class Configuration
{
    public string Conexion { get; set; } = string.Empty;

    public CadenasConexion CadenasConexion { get; set; } = new();
}

public class CadenasConexion
{
    public string ConexionSQL { get; set; } = string.Empty;

    public string ConexionMySQL { get; set; } = string.Empty;
}

public static class TipoConexion
{
    public const string SQL = "SQL";
    public const string MySQL = "MySQL";
}

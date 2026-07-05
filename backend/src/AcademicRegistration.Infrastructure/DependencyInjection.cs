using AcademicRegistration.Application.Interfaces;
using AcademicRegistration.Domain.Primitives;
using AcademicRegistration.Domain.Repositories;
using AcademicRegistration.Infrastructure.Persistence;
using AcademicRegistration.Infrastructure.Persistence.Configurations;
using AcademicRegistration.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AcademicRegistration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("Configuracion").Get<Configuration>()
                     ?? throw new Exception("Configuracion section is missing");

        services.Configure<Configuration>(configuration.GetSection("Configuracion"));

        services.AddDbContext<AcademicRegistrationDbContext>(options =>
        {
            switch(config.Conexion)
            {
                case TipoConexion.SQL:
                    options.UseSqlServer(
                        config.CadenasConexion.ConexionSQL,
                        sqlOptions => sqlOptions.MigrationsAssembly(
                            "AcademicRegistration.Infrastructure.Migrations.SqlServer"));
                    break;
                case TipoConexion.MySQL:
                    options.UseMySQL(
                        config.CadenasConexion.ConexionMySQL,
                        mySqlOptions => mySqlOptions.MigrationsAssembly(
                            "AcademicRegistration.Infrastructure.Migrations.MySql"));
                    break;
                default:
                    throw new Exception($"Tipo de conexión no soportado: {config.Conexion}");
            }
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AcademicRegistrationDbContext>());

        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IProfessorRepository, ProfessorRepository>();
        services.AddScoped<IStudentReadRepository, StudentReadRepository>();
        services.AddScoped<ISubjectReadRepository, SubjectReadRepository>();

        return services;
    }
}

using AcademicRegistration.Application.Abstractions.Events;
using AcademicRegistration.Application.Interfaces;
using AcademicRegistration.Domain.Primitives;
using AcademicRegistration.Domain.Repositories;
using AcademicRegistration.Infrastructure.Messaging;
using AcademicRegistration.Infrastructure.Outbox;
using AcademicRegistration.Infrastructure.Persistence;
using AcademicRegistration.Infrastructure.Persistence.Configurations;
using AcademicRegistration.Infrastructure.Persistence.Repositories;
using Amazon;
using Amazon.EventBridge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AcademicRegistration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("Configuracion").Get<Configuration>()
                     ?? throw new Exception("Configuracion section is missing");

        services.Configure<Configuration>(configuration.GetSection("Configuracion"));
        services.AddEventBridge(configuration);
        services.AddOutbox(configuration);

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

    private static IServiceCollection AddEventBridge(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EventBridgeSettings>()
            .Bind(configuration.GetSection("EventBridge"))
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.EventBusName),
                "EventBridge EventBusName is required")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Source),
                "EventBridge Source is required")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Region),
                "EventBridge Region is required")
            .ValidateOnStart();

        services.AddSingleton<IAmazonEventBridge>(sp =>
        {
            var settings = sp
                .GetRequiredService<IOptions<EventBridgeSettings>>()
                .Value;

            var config = new AmazonEventBridgeConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region)
            };

            if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
            {
                config.ServiceURL = settings.ServiceUrl;
                config.AuthenticationRegion = settings.Region;
            }

            return new AmazonEventBridgeClient(config);
        });

        services.AddScoped<IEventBus, OutboxEventBus>();
        services.AddSingleton<IOutboxMessagePublisher, EventBridgeEventBus>();

        return services;
    }

    private static IServiceCollection AddOutbox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection("Outbox"))
            .Validate(
                options => options.BatchSize > 0,
                "Outbox BatchSize must be greater than zero")
            .Validate(
                options => options.PollingIntervalSeconds > 0,
                "Outbox PollingIntervalSeconds must be greater than zero")
            .Validate(
                options => options.MaxRetries >= 0,
                "Outbox MaxRetries must be zero or greater")
            .ValidateOnStart();

        services.AddHostedService<OutboxMessageProcessor>();

        return services;
    }
}

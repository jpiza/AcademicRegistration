using AcademicRegistration.Application.Abstractions.Events;
using AcademicRegistration.Application.Interfaces;
using AcademicRegistration.Domain.Primitives;
using AcademicRegistration.Domain.Repositories;
using AcademicRegistration.Infrastructure.Messaging;
using AcademicRegistration.Infrastructure.Outbox;
using AcademicRegistration.Infrastructure.Persistence;
using AcademicRegistration.Infrastructure.Persistence.Configurations;
using AcademicRegistration.Infrastructure.Persistence.Repositories;
using Confluent.Kafka;
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
        services.AddKafka(configuration);
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

    private static IServiceCollection AddKafka(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<KafkaSettings>()
            .Bind(configuration.GetSection("Kafka"))
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.BootstrapServers),
                "Kafka BootstrapServers is required")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Topic),
                "Kafka Topic is required")
            .ValidateOnStart();

        services.AddSingleton<IProducer<string, string>>(sp =>
        {
            var settings = sp
                .GetRequiredService<IOptions<KafkaSettings>>()
                .Value;

            var config = new ProducerConfig
            {
                BootstrapServers = settings.BootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 1000,
                CompressionType = CompressionType.Snappy,
                AllowAutoCreateTopics = true,
                ClientId = "academic-registration-api"
            };

            ApplyKafkaSecurity(config, settings);

            return new ProducerBuilder<string, string>(config).Build();
        });

        services.AddScoped<IEventBus, OutboxEventBus>();
        services.AddSingleton<IOutboxMessagePublisher, KafkaEventBus>();

        return services;
    }

    private static void ApplyKafkaSecurity(ClientConfig config, KafkaSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SecurityProtocol))
        {
            return;
        }

        config.SecurityProtocol = ParseKafkaEnum<SecurityProtocol>(
            settings.SecurityProtocol,
            nameof(settings.SecurityProtocol));

        if (config.SecurityProtocol is not (SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SaslMechanism))
        {
            throw new InvalidOperationException("Kafka SaslMechanism is required when SecurityProtocol uses SASL.");
        }

        if (string.IsNullOrWhiteSpace(settings.SaslUsername))
        {
            throw new InvalidOperationException("Kafka SaslUsername is required when SecurityProtocol uses SASL.");
        }

        if (string.IsNullOrWhiteSpace(settings.SaslPassword))
        {
            throw new InvalidOperationException("Kafka SaslPassword is required when SecurityProtocol uses SASL.");
        }

        config.SaslMechanism = ParseKafkaEnum<SaslMechanism>(
            settings.SaslMechanism,
            nameof(settings.SaslMechanism));
        config.SaslUsername = settings.SaslUsername;
        config.SaslPassword = settings.SaslPassword;
    }

    private static TEnum ParseKafkaEnum<TEnum>(string value, string settingName)
        where TEnum : struct, Enum
    {
        var normalized = value
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(".", string.Empty)
            .Replace(" ", string.Empty);

        if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Kafka {settingName} value '{value}' is not supported.");
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

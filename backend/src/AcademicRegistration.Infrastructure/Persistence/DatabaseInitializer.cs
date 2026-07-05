using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AcademicRegistration.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAcademicRegistrationDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AcademicRegistrationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AcademicRegistrationDbContext>>();

        if (!await dbContext.Professors.AnyAsync(cancellationToken))
        {
            dbContext.Professors.AddRange(CatalogSeedData.Professors);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Catalogo de profesores inicializado.");
        }

        if (!await dbContext.Subjects.AnyAsync(cancellationToken))
        {
            dbContext.Subjects.AddRange(CatalogSeedData.Subjects);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Catalogo de materias inicializado.");
        }
    }
}

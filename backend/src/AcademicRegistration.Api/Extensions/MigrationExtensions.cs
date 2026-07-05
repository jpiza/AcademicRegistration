using AcademicRegistration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcademicRegistration.Api.Extensions;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AcademicRegistrationDbContext>();

        dbContext.Database.Migrate();
    }
}

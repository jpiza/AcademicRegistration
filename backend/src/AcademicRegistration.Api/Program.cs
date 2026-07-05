using AcademicRegistration.Api.Extensions;
using AcademicRegistration.Api.Middlewares;
using AcademicRegistration.Application;
using AcademicRegistration.Infrastructure;
using AcademicRegistration.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddTransient<ExceptionHandlingMiddleware>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.ApplyMigrations();

    await app.Services.InitializeAcademicRegistrationDatabaseAsync();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "AcademicRegistration.Api" }));

app.Run();

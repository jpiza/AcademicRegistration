using Amazon.XRay.Recorder.Core;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration.GetValue("Tracing:XRay:Enabled", false))
{
    AWSXRayRecorder.InitializeInstance(builder.Configuration);
}

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

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

var app = builder.Build();

if (app.Configuration.GetValue("Tracing:XRay:Enabled", false))
{
    app.UseXRay("AcademicRegistration.Gateway");
}

app.UseCors("Frontend");

app.MapGet("/", () => Results.Ok(new { service = "AcademicRegistration.Gateway", proxy = "/api/{**catch-all}" }));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "AcademicRegistration.Gateway" }));
app.MapReverseProxy();

app.Run();

using kwikby_MP.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Local secrets (SQL password, JWT key) stay out of source control.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddApiInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseApiPipeline();

app.Run();

// WebApplicationFactory<Program> needs a public entry point type.
public partial class Program { }

using DataMap.Api.Endpoints;
using DataMap.Api.Infrastructure;
using DataMap.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddDataMapServices(builder.Configuration);

var app = builder.Build();

// Demo data is a local-development convenience. Seeding unconditionally would write it into
// whatever database the deployed environment is pointed at.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DataMap.Api.Seed.DemoDataSeeder>();
    await seeder.SeedAsync();
}

app.UseCors();

app.UseMiddleware<ErrorHandlerMiddleware>();
app.UseMiddleware<SessionAuthMiddleware>();

HealthEndpoints.Map(app);
InviteEndpoints.Map(app);
MetadataEndpoints.Map(app);
BusinessTermEndpoints.Map(app);

if (app.Environment.IsDevelopment())
    DevEndpoints.Map(app);

app.Run();

public partial class Program { }

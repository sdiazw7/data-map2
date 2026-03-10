using DataMap.Api.Endpoints;
using DataMap.Api.Infrastructure;
using DataMap.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddDataMapServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DataMap.Api.Seed.DemoDataSeeder>();
    await seeder.SeedAsync();
}

app.UseCors();

app.UseMiddleware<ErrorHandlerMiddleware>();
app.UseMiddleware<SessionAuthMiddleware>();

InviteEndpoints.Map(app);
MetadataEndpoints.Map(app);
BusinessTermEndpoints.Map(app);

app.Run();

public partial class Program { }

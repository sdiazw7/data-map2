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

// Explicit so endpoint metadata is resolved before SessionAuthMiddleware runs — it decides
// whether a route is public by reading that route's AllowAnonymous metadata.
app.UseRouting();

app.UseMiddleware<ErrorHandlerMiddleware>();

// Catches error statuses produced without an exception — an unmatched route, a rejected
// method, an unsupported content type — and gives them the same body as everything else.
// Skips any response that already has one, so the middlewares above still own their own.
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var (code, message) = ApiErrorWriter.DescribeStatus(response.StatusCode);
    await ApiErrorWriter.WriteAsync(context.HttpContext, response.StatusCode, code, message);
});

app.UseMiddleware<SessionAuthMiddleware>();

HealthEndpoints.Map(app);
InviteEndpoints.Map(app);
ColumnEndpoints.Map(app);
ImportEndpoints.Map(app);
BusinessTermEndpoints.Map(app);

if (app.Environment.IsDevelopment())
{
    DevEndpoints.Map(app);

    // The generated document describes the contract; it is not part of it, and it carries no
    // workspace data. Left protected it would 401 for every tool that tries to read it.
    app.MapOpenApi().AllowAnonymous();
}

app.Run();

public partial class Program { }

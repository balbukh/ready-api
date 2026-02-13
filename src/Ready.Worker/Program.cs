using Ready.Application;
using Ready.Infrastructure;
using Ready.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Ready.Worker;

var builder = Host.CreateApplicationBuilder(args);

var cs = builder.Configuration.GetConnectionString("ReadyDb")!;

builder.Services.AddReadyApplication(builder.Configuration);
builder.Services.AddReadyInfrastructure(cs);

// optional: auto-migrate on startup (dev only)
builder.Services.AddHostedService<DbMigrator>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();

public sealed class DbMigrator : IHostedService
{
    private readonly IServiceProvider _sp;
    public DbMigrator(IServiceProvider sp) => _sp = sp;

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadyDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
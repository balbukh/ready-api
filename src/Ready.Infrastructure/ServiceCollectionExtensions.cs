using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ready.Application.Abstractions;
using Ready.Application.Results;
using Ready.Infrastructure.Persistence;
using Ready.Infrastructure.Steps;

namespace Ready.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReadyInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReadyDbContext>(o => o.UseNpgsql(connectionString));

        services.AddScoped<IJobQueue, JobQueue>();
        services.AddScoped<IRunStore, RunStore>();
        services.AddScoped<IResultStore, DbResultStore>();
        services.AddScoped<IDocumentStore, DocumentStore>();

        services.AddSingleton<IFileStorage>(_ =>
            new Storage.LocalFileStorage(Path.Combine(AppContext.BaseDirectory, "data")));

        services.AddScoped<IWorkflowStep, PdfTextExtractStep>();

        // Read Stores
        services.AddScoped<IStatusReadStore, Persistence.StatusReadStore>();
        
        return services;
    }
}
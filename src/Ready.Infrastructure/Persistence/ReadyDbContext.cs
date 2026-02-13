using Microsoft.EntityFrameworkCore;

namespace Ready.Infrastructure.Persistence;

public sealed class ReadyDbContext : DbContext
{
    public ReadyDbContext(DbContextOptions<ReadyDbContext> options) : base(options) { }

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<RunEntity> Runs => Set<RunEntity>();
    public DbSet<StepRunEntity> StepRuns => Set<StepRunEntity>();
    public DbSet<ResultEntity> Results => Set<ResultEntity>();
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKeyEntity>(b =>
        {
            b.ToTable("api_keys");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).IsRequired();
            b.Property(x => x.CustomerId).IsRequired();
            b.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<DocumentEntity>(b =>
        {
            b.ToTable("documents");
            b.HasKey(x => x.Id);
            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.Source).IsRequired();
            b.Property(x => x.FileName).IsRequired();
            b.Property(x => x.ContentType).IsRequired();
            b.Property(x => x.StoragePath).IsRequired();
            b.Property(x => x.Sha256).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.CustomerId, x.CreatedAt });
            b.HasIndex(x => new { x.CustomerId, x.Sha256 }).IsUnique();
        });

        modelBuilder.Entity<RunEntity>(b =>
        {
            b.ToTable("runs");
            b.HasKey(x => x.Id);
            b.Property(x => x.DocumentId).IsRequired();
            b.Property(x => x.WorkflowName).IsRequired();
            b.Property(x => x.WorkflowVersion).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.StartedAt).IsRequired();
            b.HasIndex(x => x.DocumentId);
        });

        modelBuilder.Entity<StepRunEntity>(b =>
        {
            b.ToTable("step_runs");
            b.HasKey(x => x.Id);
            b.Property(x => x.RunId).IsRequired();
            b.Property(x => x.StepName).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.StartedAt).IsRequired();
            b.HasIndex(x => x.RunId);
        });

        modelBuilder.Entity<ResultEntity>(b =>
        {
            b.ToTable("results");
            b.HasKey(x => x.Id);
            b.Property(x => x.RunId).IsRequired();
            b.Property(x => x.ResultType).IsRequired();
            b.Property(x => x.Version).IsRequired();
            b.Property(x => x.PayloadJson).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.RunId);
            b.HasIndex(x => new { x.ResultType, x.Version });
        });

        modelBuilder.Entity<JobEntity>(b =>
        {
            b.ToTable("jobs");
            b.HasKey(x => x.Id);
            b.Property(x => x.DocumentId).IsRequired();
            b.Property(x => x.WorkflowName).IsRequired();
            b.Property(x => x.WorkflowVersion).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.Attempts).IsRequired();
            b.Property(x => x.ParamsJson).IsRequired(false);
            b.Property(x => x.NextRunAt).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.Status, x.NextRunAt });
        });
    }
}
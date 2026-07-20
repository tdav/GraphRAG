using Microsoft.EntityFrameworkCore;

namespace MyGraphRagV5.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RagProject> RagProjects => this.Set<RagProject>();

    public DbSet<IndexingRun> IndexingRuns => this.Set<IndexingRun>();

    public DbSet<ChatSession> ChatSessions => this.Set<ChatSession>();

    public DbSet<ChatMessage> ChatMessages => this.Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<RagProject>(entity =>
        {
            entity.HasIndex(p => p.Name).IsUnique();
            entity.HasIndex(p => p.GraphName).IsUnique();
        });

        modelBuilder.Entity<IndexingRun>(entity =>
        {
            entity.Property(r => r.Status).HasConversion<string>();
            entity.HasIndex(r => r.ProjectId);
            entity.HasOne<RagProject>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasIndex(s => s.ProjectId);
            entity.HasOne<RagProject>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.Property(m => m.SourcesJson).HasColumnType("jsonb");
            entity.HasIndex(m => m.SessionId);
            entity.HasOne<ChatSession>().WithMany().HasForeignKey(m => m.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

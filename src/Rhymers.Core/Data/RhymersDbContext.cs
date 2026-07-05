using Microsoft.EntityFrameworkCore;
using Rhymers.Core.Models;

namespace Rhymers.Core.Data;

/// <summary>
/// Entity Framework Core DbContext для VoteCounter
/// </summary>
public sealed class RhymersDbContext : DbContext
{
    public RhymersDbContext(DbContextOptions<RhymersDbContext> options)
        : base(options)
    {
    }

    // DbSets для основных моделей
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Contest> Contests { get; set; } = null!;
    public DbSet<WorkSubmission> Submissions { get; set; } = null!;
    public DbSet<ContestTopic> Topics { get; set; } = null!;
    public DbSet<TopicKind> TopicKinds { get; set; } = null!;
    public DbSet<ContestVote> ContestVotes { get; set; } = null!;
    public DbSet<VoterSetting> Voters { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User конфигурация
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.Role).IsRequired();
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Contest конфигурация
        modelBuilder.Entity<Contest>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Number).IsUnique();
            entity.Property(c => c.Number).HasMaxLength(10).IsRequired();
            entity.Property(c => c.Name).HasMaxLength(256).IsRequired();
            entity.Property(c => c.MaxTopicsCount).HasDefaultValue(0);
            entity.Property(c => c.AutoStageSwitchEnabled).HasDefaultValue(false);
            entity.Property(c => c.TopicReceptionSwitchTime).HasMaxLength(5);
            entity.Property(c => c.WorkReceptionSwitchTime).HasMaxLength(5);
            entity.Property(c => c.VotingOpenSwitchTime).HasMaxLength(5);
            entity.Property(c => c.VotingClosedSwitchTime).HasMaxLength(5);
            entity.Property(c => c.IsActive).HasDefaultValue(true);
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Игнорируем in-memory навигационные свойства
            entity.Ignore(c => c.Works);
            entity.Ignore(c => c.Topics);
            entity.Ignore(c => c.Voters);
        });

        // WorkSubmission конфигурация (с owned ContestWork)
        modelBuilder.Entity<WorkSubmission>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.ContestId);
            entity.Property(s => s.ContestId).HasMaxLength(256).IsRequired();
            entity.Property(s => s.Status).IsRequired();
            entity.Property(s => s.SubmittedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(s => s.ModeratorName).HasMaxLength(256);

            // ContestWork как owned entity (вложенный объект)
            entity.OwnsOne(s => s.Work, work =>
            {
                work.Property(w => w.Title).HasMaxLength(512);
                work.Property(w => w.Subtitle).HasMaxLength(512);
                work.Property(w => w.Author).HasMaxLength(256);
                work.Property(w => w.Topic).HasMaxLength(256);
                work.Property(w => w.Status).IsRequired();
            });
        });

        // ContestTopic конфигурация
        modelBuilder.Entity<ContestTopic>(entity =>
        {
            entity.ToTable("ContestTopics");
            entity.HasKey(t => new { t.ContestId, t.Number });
            entity.Property(t => t.ContestId).HasMaxLength(256).IsRequired();
            entity.Property(t => t.Title).HasMaxLength(512);
            entity.Property(t => t.TopicKindId);
            entity.Property(t => t.ProposedBy).HasMaxLength(256);
            entity.Property(t => t.IsWinnerTopic).HasDefaultValue(false);
            entity.Property(t => t.SubmittedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(t => t.ContestId);
        });

        modelBuilder.Entity<TopicKind>(entity =>
        {
            entity.ToTable("TopicKinds");
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Id).ValueGeneratedOnAdd();
            entity.Property(k => k.Name).HasMaxLength(128).IsRequired();
            entity.Property(k => k.SortNo).HasDefaultValue(0);
            entity.HasIndex(k => k.Name).IsUnique();
        });

        modelBuilder.Entity<ContestVote>(entity =>
        {
            entity.ToTable("ContestVotes");
            entity.HasKey(v => new { v.ContestId, v.SubmissionId, v.VoterUserId });
            entity.Property(v => v.ContestId).HasMaxLength(256).IsRequired();
            entity.Property(v => v.SubmissionId).HasMaxLength(256).IsRequired();
            entity.Property(v => v.VoterUserId).HasMaxLength(256).IsRequired();
            entity.Property(v => v.VoterUsername).HasMaxLength(256).IsRequired();
            entity.Property(v => v.Comment).HasMaxLength(512);
            entity.Property(v => v.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(v => v.ContestId);
        });

        // VoterSetting конфигурация
        modelBuilder.Entity<VoterSetting>(entity =>
        {
            entity.HasKey(v => v.Name);
            entity.Property(v => v.Name).HasMaxLength(256);
        });
    }
}

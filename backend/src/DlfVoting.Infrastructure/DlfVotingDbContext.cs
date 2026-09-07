using DlfVoting.Domain;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public class DlfVotingDbContext : DbContext
{
    public DlfVotingDbContext(DbContextOptions<DlfVotingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<VotingOption> VotingOptions => Set<VotingOption>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vote> Votes => Set<Vote>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
        modelBuilder.Entity<Administrator>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(a => a.Email).IsUnique();
            entity.Property(a => a.PasswordHash).IsRequired();
        });
        
        modelBuilder.Entity<VotingOption>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(v => v.Name).IsUnique();
        });
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
        });
        
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.UserId).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<VotingOption>().WithMany().HasForeignKey(v => v.VotingOptionId).OnDelete(DeleteBehavior.Cascade);
        });
        
    }
}
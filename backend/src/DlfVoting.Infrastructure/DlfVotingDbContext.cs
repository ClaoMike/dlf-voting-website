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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Administrator>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(a => a.Email).IsUnique();
            entity.Property(a => a.PasswordHash).IsRequired();
        });
    }
}
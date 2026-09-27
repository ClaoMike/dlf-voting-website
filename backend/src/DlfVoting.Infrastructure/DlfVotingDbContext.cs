using DlfVoting.Domain;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public class DlfVotingDbContext : DbContext
{   
    // ReSharper disable once ConvertToPrimaryConstructor
    public DlfVotingDbContext(DbContextOptions<DlfVotingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<VotingOption> VotingOptions => Set<VotingOption>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<VotingSettings> VotingSettings => Set<VotingSettings>();
    
    // ICU nondeterministic collation: equality and unique indexes ignore case ("JDoe" == "jdoe").
    public const string CaseInsensitiveCollation = "case_insensitive";

    // Postgres' built-in Danish ICU collation, for sorting people by name: Æ, Ø, Å after Z, case doesn't split the list.
    public const string DanishCollation = "da-x-icu";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCollation(CaseInsensitiveCollation, locale: "und-u-ks-level2", provider: "icu", deterministic: false);

        modelBuilder.Entity<Administrator>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Username).IsRequired().HasMaxLength(Administrator.UsernameMaxLength)
                .UseCollation(CaseInsensitiveCollation);
            entity.HasIndex(a => a.Username).IsUnique();
            entity.Property(a => a.Email).IsRequired().HasMaxLength(320)
                .UseCollation(CaseInsensitiveCollation);
            entity.HasIndex(a => a.Email).IsUnique();
            entity.Property(a => a.PasswordHash).IsRequired();
        });
        
        modelBuilder.Entity<VotingOption>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).IsRequired().HasMaxLength(VotingOption.NameMaxLength);
            entity.HasIndex(v => v.Name).IsUnique();
        });
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(User.UsernameMaxLength)
                .UseCollation(CaseInsensitiveCollation);
            entity.HasIndex(u => u.Username).IsUnique();
            // Case-insensitive like usernames: "A@x.dk" and "a@x.dk" are one person, and must not get two accounts (two votes).
            entity.Property(u => u.Email).HasMaxLength(320)
                .UseCollation(CaseInsensitiveCollation);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.EmployeeCode).HasMaxLength(User.EmployeeFieldMaxLength);
            entity.Property(u => u.FirstName).HasMaxLength(User.EmployeeFieldMaxLength);
            entity.Property(u => u.LastName).HasMaxLength(User.EmployeeFieldMaxLength);
            entity.Property(u => u.CompanyCode).HasMaxLength(User.EmployeeFieldMaxLength);
            entity.Property(u => u.Electability).HasMaxLength(User.EmployeeFieldMaxLength);
        });
        
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.UserId).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<VotingOption>().WithMany().HasForeignKey(v => v.VotingOptionId).OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<VotingSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
        });
        
    }
}
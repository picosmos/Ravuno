using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage.Models;

namespace Ravuno.DataStorage;

public class DataStorageContext : DbContext
{
    public DataStorageContext(DbContextOptions<DataStorageContext> options)
        : base(options) { }

    public DbSet<Item> Items { get; set; }
    public DbSet<FetchHistory> FetchHistories { get; set; }
    public DbSet<SendUpdateHistory> SendUpdateHistories { get; set; }
    public DbSet<Query> Queries { get; set; }
    public DbSet<EmailReceiver> EmailReceivers { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Group> Groups { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Source).IsRequired().HasConversion<string>();

            entity.Property(e => e.RetrievedAt).IsRequired();

            entity.Property(e => e.EventStartDateTime).IsRequired();

            entity.Property(e => e.Title).HasMaxLength(500);

            entity.Property(e => e.Location).HasMaxLength(500);

            entity.Property(e => e.Url).HasMaxLength(2000);

            entity.Property(e => e.Price).HasMaxLength(100);

            entity.HasIndex(e => new { e.Source, e.SourceId }).IsUnique();
        });

        modelBuilder.Entity<FetchHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Source).IsRequired().HasConversion<string>();

            entity.Property(e => e.ExecutionStartTime).IsRequired();

            entity.Property(e => e.ExecutionDuration).IsRequired();

            entity.Property(e => e.ItemsRetrieved).IsRequired();

            entity.Property(e => e.NewItems).IsRequired();

            entity.Property(e => e.UpdatedItems).IsRequired();
        });

        modelBuilder.Entity<SendUpdateHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.QueryTitle).IsRequired().HasMaxLength(500);

            entity.Property(e => e.EmailReceiverAddress).IsRequired().HasMaxLength(500);

            entity.Property(e => e.SentAt).IsRequired();

            entity.Property(e => e.NewItemsCount).IsRequired();

            entity.Property(e => e.UpdatedItemsCount).IsRequired();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);

            entity.Property(e => e.PasswordHash).IsRequired();

            entity.Property(e => e.Salt).IsRequired();

            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.Username).IsUnique();

            entity.HasOne(e => e.Role).WithMany(r => r.Users).HasForeignKey(e => e.RoleId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);

            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Query>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);

            entity.Property(e => e.SqlQuery).IsRequired();

            entity.HasOne(e => e.User).WithMany(u => u.Queries).HasForeignKey(e => e.UserId);
        });
    }
}

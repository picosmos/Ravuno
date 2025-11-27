using DataStorage.Models;
using Microsoft.EntityFrameworkCore;

namespace DataStorage;

public class DataStorageContext : DbContext
{
    public DataStorageContext(DbContextOptions<DataStorageContext> options)
        : base(options)
    {
    }

    public DbSet<Item> Items { get; set; }
    public DbSet<FetchHistory> FetchHistories { get; set; }
    public DbSet<SendUpdateHistory> SendUpdateHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => new { e.Source, e.RetrievedAt, e.Url });

            entity.Property(e => e.Source)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(e => e.RetrievedAt)
                .IsRequired();

            entity.Property(e => e.EventStartDateTime)
                .IsRequired();

            entity.Property(e => e.Title)
                .HasMaxLength(500);

            entity.Property(e => e.Location)
                .HasMaxLength(500);

            entity.Property(e => e.Url)
                .HasMaxLength(2000);

            entity.Property(e => e.Price)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<FetchHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Source)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(e => e.ExecutionStartTime)
                .IsRequired();

            entity.Property(e => e.ExecutionDuration)
                .IsRequired();

            entity.Property(e => e.ItemsRetrieved)
                .IsRequired();

            entity.Property(e => e.NewItems)
                .IsRequired();

            entity.Property(e => e.UpdatedItems)
                .IsRequired();
        });

        modelBuilder.Entity<SendUpdateHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.QueryTitle)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.EmailReceiverAddress)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.SentAt)
                .IsRequired();

            entity.Property(e => e.NewItemsCount)
                .IsRequired();

            entity.Property(e => e.UpdatedItemsCount)
                .IsRequired();
        });
    }
}
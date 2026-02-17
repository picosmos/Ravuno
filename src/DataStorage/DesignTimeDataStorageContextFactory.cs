using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ravuno.DataStorage;

public class DesignTimeDataStorageContextFactory : IDesignTimeDbContextFactory<DataStorageContext>
{
    public DataStorageContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DataStorageContext>();
        // Use SQLite for design-time migration, update as needed for your environment
        optionsBuilder.UseSqlite("Data Source=DataStorage.db");
        return new DataStorageContext(optionsBuilder.Options);
    }
}

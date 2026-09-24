using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RetailLab.Data;

public sealed class RetailLabDbContextFactory : IDesignTimeDbContextFactory<RetailLabDbContext>
{
    public RetailLabDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(AppContext.BaseDirectory, "retaillab-design.db");
        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new RetailLabDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MandiDost.Api.Data;

/// <summary>
/// Used only by `dotnet ef migrations add`. Generating a migration doesn't connect to the
/// database, so a placeholder connection string is enough. `dotnet ef database update`
/// should be given the real one via --connection.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("MANDIDOST_DB")
                 ?? @"Server=(localdb)\MSSQLLocalDB;Database=MandiDost;Trusted_Connection=True;TrustServerCertificate=True";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
    }
}

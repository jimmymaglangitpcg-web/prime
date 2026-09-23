using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Prime.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` / `dotnet ef database
/// update` work from the CLI without spinning up the full WebApi host.
/// Reads the connection string from the PRIME_DB_CONNECTION environment
/// variable — never a hard-coded local default with a real password, per
/// CLAUDE.md §67/§104. See docs/DATABASE.md §1.1 for which connection
/// (direct, not pooled) migrations must use against Supabase.
/// </summary>
public class PrimeDbContextFactory : IDesignTimeDbContextFactory<PrimeDbContext>
{
    public PrimeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PRIME_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "PRIME_DB_CONNECTION environment variable is not set. " +
                "Set it to a local PostgreSQL connection string (see .env.example) before running EF Core CLI commands.");

        var optionsBuilder = new DbContextOptionsBuilder<PrimeDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite());

        return new PrimeDbContext(optionsBuilder.Options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BusinessSuite.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BusinessSuiteDbContext>
{
    public BusinessSuiteDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "PostgreSql";
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=businesssuite;Username=postgres";
        var options = new DbContextOptionsBuilder<BusinessSuiteDbContext>();
        if (provider == "PostgreSql") options.UseNpgsql(connection);
        else throw new InvalidOperationException("Unsupported database provider.");
        return new(options.Options);
    }
}

using BusinessSuite.Application;
using BusinessSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessSuite.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessSuiteInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is required.");
        var provider = config["Database:Provider"] ?? "PostgreSql";
        if (provider != "PostgreSql") throw new InvalidOperationException("This release supplies PostgreSQL migrations only.");
        services.AddDbContext<BusinessSuiteDbContext>(options => options.UseNpgsql(connection));
        services.AddScoped<IPortfolioStore, PortfolioStore>();
        return services;
    }
}

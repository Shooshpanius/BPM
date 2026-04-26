using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoreBPM.Server.Infrastructure.Persistence;

/// <summary>
/// Фабрика для создания AppDbContext во время разработки (например, при генерации миграций).
/// Используется инструментами dotnet-ef без необходимости запуска приложения.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc/>
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Строка подключения только для генерации миграций (не используется в production)
        var connectionString = Environment.GetEnvironmentVariable("BPM_S_ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=corebpm;Username=postgres;Password=postgres";

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}

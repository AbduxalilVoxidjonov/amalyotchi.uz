using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Amaliyotchi.Infrastructure.Persistence;

/// <summary><c>dotnet ef migrations add / database update</c> uchun. API host'ini ko'tarmaydi (seed, JWT
/// tekshiruvi va h.k. ishga tushmaydi). Ulanish: <c>ConnectionStrings__Postgres</c> muhit o'zgaruvchisi,
/// bo'lmasa lokal dev bazasi (docker <c>amaliyotchi-postgres</c>).</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultDevConnection =
        "Host=127.0.0.1;Port=55432;Database=amaliyotchi;Username=amaliyotchi;Password=amaliyotchi";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = DefaultDevConnection;

        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureNpgsql(options, connectionString);
        return new AppDbContext(options.Options);
    }
}

using Testcontainers.PostgreSql;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Bitta PostGIS konteyneri — butun test to'plami uchun (ishga tushish ~5–10 s).
/// Image ishlab chiqarishdagi bilan bir xil (deploy/docker-compose.yml).</summary>
public sealed class PostgresFixture : IAsyncDisposable
{
    public const string Image = "postgis/postgis:16-3.4";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithDatabase("amaliyotchi_test")
        .WithUsername("amaliyotchi")
        .WithPassword("amaliyotchi")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task StartAsync() => _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

using Amaliyotchi.IntegrationTests.Infrastructure;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Alohida (bo'sh) baza — umumiy <see cref="ApiCollection"/> bazasida boshqa testlarning ma'lumoti bor,
/// "bo'sh bazada dashboard nol qaytaradi" tekshiruvi uchun o'z konteyneri kerak.</summary>
public sealed class AdminEmptyDbFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public ApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new ApiFactory(_postgres.ConnectionString);
        await Factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AdminEmptyDbCollection : ICollectionFixture<AdminEmptyDbFixture>
{
    public const string Name = "admin-empty-db";
}

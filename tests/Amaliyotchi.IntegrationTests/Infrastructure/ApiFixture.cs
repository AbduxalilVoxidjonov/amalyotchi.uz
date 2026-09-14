namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Collection fixture: konteyner + API bir marta ko'tariladi, barcha test klasslari bo'lishadi.
/// Testlar bir bazada ishlaydi — har test o'z foydalanuvchisini yaratadi (tasodifiy telefon/telegramId),
/// shuning uchun bir-biriga xalaqit bermaydi.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public ApiFactory Factory { get; private set; } = null!;

    /// <summary>API soati (<see cref="ApiFactory.Clock"/>): sukut bo'yicha haqiqiy vaqt.</summary>
    public MutableClock Clock => Factory.Clock;

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
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

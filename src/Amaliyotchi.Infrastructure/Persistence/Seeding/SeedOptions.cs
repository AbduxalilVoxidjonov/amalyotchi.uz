namespace Amaliyotchi.Infrastructure.Persistence.Seeding;

/// <summary><c>Seed</c> bo'limi. <c>Enabled</c> — startup'da migratsiya + seed;
/// <c>Demo</c> — frontend mock'lariga mos demo ma'lumot (default false; Development va Docker stendida true).</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public const string DevelopmentAdminPhone = "+998901234567";
    public const string DevelopmentAdminPassword = "admin12345";

    public bool Enabled { get; init; }
    public bool Demo { get; init; }
    public string AdminFullName { get; init; } = "Admin Adminov";
    public string? AdminPhone { get; init; }
    public string? AdminPassword { get; init; }
}

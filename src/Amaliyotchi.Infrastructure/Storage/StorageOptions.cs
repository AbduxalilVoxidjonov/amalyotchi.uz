namespace Amaliyotchi.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Lokal disk ildizi. Nisbiy bo'lsa — ishchi papkaga nisbatan.</summary>
    public string RootPath { get; init; } = "./data/files";
}

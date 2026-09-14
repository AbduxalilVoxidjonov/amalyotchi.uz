using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amaliyotchi.Api.Infrastructure;

/// <summary>API'ning yagona JSON kelishuvi (kontrakt §umumiy): camelCase property, enum — camelCase string,
/// null'lar chiqariladi (frontend <c>string | null</c> kutadi). Testlar ham shu sozlamani ishlatadi.</summary>
public static class JsonConventions
{
    public static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null; // ProblemDetails.errors kalitlari PascalCase (FluentValidation) qoladi
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    /// <summary>Mustaqil nusxa (testlar, fon ishlari) — DI'siz.</summary>
    public static JsonSerializerOptions Create() => Apply(new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

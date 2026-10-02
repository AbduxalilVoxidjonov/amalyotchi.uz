using System.Globalization;
using Amaliyotchi.Domain.Messaging;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary>Xabar tarixidagi auditoriya tavsifi (o'zbekcha, server yasaydi):
/// <list type="bullet">
/// <item><c>all</c> → "Barcha ulanganlar";</item>
/// <item><c>selected</c> → "Tanlangan: 3 ta talaba" (resolve qilingan — haqiqiy qabul qiluvchilar soni);</item>
/// <item><c>filter</c> → berilgan qismlar " · " bilan: fakultet nomi · yo'nalish · "3-kurs" · guruh · «qidiruv»
/// (masalan "Axborot texnologiyalari · 3-kurs · 412-22"); hech biri berilmasa — "Barcha ulanganlar".</item>
/// </list></summary>
public static class AudienceLabel
{
    public const string AllLinked = "Barcha ulanganlar";
    public const string Separator = " · ";

    public static string Build(
        BroadcastAudienceKind kind,
        int selectedCount = 0,
        string? facultyName = null,
        string? directionName = null,
        int? course = null,
        string? groupName = null,
        string? q = null)
    {
        switch (kind)
        {
            case BroadcastAudienceKind.All:
                return AllLinked;
            case BroadcastAudienceKind.Selected:
                return string.Create(CultureInfo.InvariantCulture, $"Tanlangan: {selectedCount} ta talaba");
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(facultyName))
            parts.Add(facultyName.Trim());
        if (!string.IsNullOrWhiteSpace(directionName))
            parts.Add(directionName.Trim());
        if (course is { } c)
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{c}-kurs"));
        if (!string.IsNullOrWhiteSpace(groupName))
            parts.Add(groupName.Trim());
        if (!string.IsNullOrWhiteSpace(q))
            parts.Add($"«{q.Trim()}»");

        return parts.Count == 0 ? AllLinked : string.Join(Separator, parts);
    }
}

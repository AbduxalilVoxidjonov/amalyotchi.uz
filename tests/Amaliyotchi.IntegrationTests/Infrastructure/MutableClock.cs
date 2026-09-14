using Amaliyotchi.Application.Common.Interfaces;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Test soati: sukut bo'yicha haqiqiy UTC vaqt; <see cref="Set"/> bilan muzlatiladi (vaqt yurmaydi),
/// <see cref="Reset"/> haqiqiy vaqtga qaytaradi. API'da <c>IClock</c> singleton — bitta nusxa butun
/// <see cref="ApiFactory"/> uchun, shuning uchun <c>Set</c> qilgan test <c>finally</c> da <c>Reset</c> qilishi shart
/// (kolleksiya testlari ketma-ket, lekin keyingi test toza soat kutadi).
/// JWT tekshiruvi (JwtBearer) o'z soatini ishlatadi — token muzlatishdan OLDIN olinsin.</summary>
public sealed class MutableClock : IClock
{
    private readonly Lock _gate = new();
    private DateTimeOffset? _frozenAt;

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
                return _frozenAt ?? DateTimeOffset.UtcNow;
        }
    }

    public bool IsFrozen
    {
        get
        {
            lock (_gate)
                return _frozenAt is not null;
        }
    }

    /// <summary>Soatni berilgan momentga muzlatadi (UTC'ga normalizatsiya qilinadi).</summary>
    public void Set(DateTimeOffset at)
    {
        lock (_gate)
            _frozenAt = at.ToUniversalTime();
    }

    public void Reset()
    {
        lock (_gate)
            _frozenAt = null;
    }
}

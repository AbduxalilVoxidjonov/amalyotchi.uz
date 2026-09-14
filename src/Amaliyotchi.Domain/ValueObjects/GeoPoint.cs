using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.ValueObjects;

/// <summary>Geografik nuqta (WGS 84). Domain NetTopologySuite'ga bog'lanmaydi —
/// Infrastructure buni PostGIS <c>geography(Point, 4326)</c> ga o'zi map qiladi.
/// Masofa hisoblari bazada (ST_Distance) bajariladi; <see cref="DistanceMetersTo"/> — faqat
/// unit test va ehtiyot uchun Haversine.</summary>
public readonly record struct GeoPoint
{
    /// <summary>Yer radiusi (m) — Haversine uchun o'rtacha qiymat.</summary>
    public const double EarthRadiusMeters = 6_371_000d;

    public GeoPoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
            throw new DomainException("Kenglik (latitude) -90 va 90 oralig'ida bo'lishi kerak.");
        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
            throw new DomainException("Uzunlik (longitude) -180 va 180 oralig'ida bo'lishi kerak.");

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }
    public double Longitude { get; }

    /// <summary>Ikki nuqta orasidagi masofa metrda (Haversine, ±0.5% aniqlik).</summary>
    public double DistanceMetersTo(GeoPoint other)
    {
        var lat1 = ToRadians(Latitude);
        var lat2 = ToRadians(other.Latitude);
        var dLat = ToRadians(other.Latitude - Latitude);
        var dLng = ToRadians(other.Longitude - Longitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public override string ToString() => FormattableString.Invariant($"{Latitude:F6},{Longitude:F6}");

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}

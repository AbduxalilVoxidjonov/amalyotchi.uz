using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    /// <summary>PostGIS ustuni (shadow): <c>geography(Point,4326)</c>, <c>latitude</c>/<c>longitude</c> dan
    /// bazada hisoblanadi (stored generated). So'rovlarda: <c>EF.Property&lt;Point&gt;(c, LocationGeog)</c>.</summary>
    public const string LocationGeog = "LocationGeog";

    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(Company.NameMaxLength).IsRequired();
        builder.Property(x => x.Tin).HasMaxLength(Tin.DigitCount).IsRequired();
        builder.Property(x => x.Activity).HasMaxLength(Company.ActivityMaxLength).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(Company.AddressMaxLength).IsRequired();
        builder.Property(x => x.SupervisorName).HasMaxLength(Company.NameMaxLength).IsRequired();
        builder.Property(x => x.SupervisorPhone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MentorName).HasMaxLength(Company.NameMaxLength);
        builder.Property(x => x.MentorPhone).HasMaxLength(20);

        // Domain GeoPoint (NTS'siz) → ikkita ustun. PostGIS geography — undan hisoblanadigan shadow ustun.
        builder.ComplexProperty(x => x.Location, location =>
        {
            location.Property(p => p.Latitude).HasColumnName("latitude").IsRequired();
            location.Property(p => p.Longitude).HasColumnName("longitude").IsRequired();
        });

        builder.Property<Point>(LocationGeog)
            .HasColumnName("location_geog")
            .HasColumnType("geography(Point,4326)")
            .HasComputedColumnSql("ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography", stored: true);

        builder.HasIndex(LocationGeog).HasMethod("GIST");
        builder.HasIndex(x => x.Tin).IsUnique().HasFilter("is_deleted = false");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

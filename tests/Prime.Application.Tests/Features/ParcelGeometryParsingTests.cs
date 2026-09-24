using NetTopologySuite.Geometries;
using Prime.Application.Features.Parcels;
using Prime.Domain.Common;
using Shouldly;
using Xunit;

namespace Prime.Application.Tests.Features;

public class ParcelGeometryParsingTests
{
    private const string Square = "POLYGON((121.0 14.5, 121.001 14.5, 121.001 14.501, 121.0 14.501, 121.0 14.5))";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankWkt_MeansNoGeometry(string? wkt)
    {
        var result = ParcelService.ParseParcelGeometry(wkt);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void Polygon_IsNormalizedToStorageSridMultiPolygon()
    {
        var result = ParcelService.ParseParcelGeometry(Square);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeOfType<MultiPolygon>();
        result.Value!.NumGeometries.ShouldBe(1);
        result.Value.SRID.ShouldBe(SpatialReference.StorageSrid);
    }

    [Fact]
    public void MultiPolygon_IsAcceptedAsIs()
    {
        var result = ParcelService.ParseParcelGeometry(
            "MULTIPOLYGON(((121.0 14.5, 121.001 14.5, 121.001 14.501, 121.0 14.5)),((121.002 14.5, 121.003 14.5, 121.003 14.501, 121.002 14.5)))");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.NumGeometries.ShouldBe(2);
    }

    [Theory]
    [InlineData("POINT(121.0 14.5)")]
    [InlineData("LINESTRING(121.0 14.5, 121.001 14.501)")]
    public void NonArealGeometry_IsRejected(string wkt)
    {
        var result = ParcelService.ParseParcelGeometry(wkt);

        result.IsFailure.ShouldBeTrue();
        result.Code.ShouldBe("INVALID_GEOMETRY_TYPE");
    }

    [Fact]
    public void SelfIntersectingPolygon_IsRejected()
    {
        // "Bow-tie": edges cross, so the ring is not a valid boundary.
        var result = ParcelService.ParseParcelGeometry("POLYGON((121.0 14.5, 121.001 14.501, 121.001 14.5, 121.0 14.501, 121.0 14.5))");

        result.IsFailure.ShouldBeTrue();
        result.Code.ShouldBe("INVALID_GEOMETRY");
    }

    [Fact]
    public void UnparseableWkt_IsRejected()
    {
        var result = ParcelService.ParseParcelGeometry("POLYGON((not coordinates))");

        result.IsFailure.ShouldBeTrue();
        result.Code.ShouldBe("INVALID_GEOMETRY");
    }
}

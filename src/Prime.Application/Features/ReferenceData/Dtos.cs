namespace Prime.Application.Features.ReferenceData;

/// <summary>Shape shared by every simple lookup (Zone, Classification, ActualUse, ...).</summary>
public sealed record LookupDto(Guid Id, string Code, string Name);

public sealed record ProvinceDto(Guid Id, string PsgcCode, string Name);
public sealed record MunicipalityDto(Guid Id, string PsgcCode, string Name, Guid ProvinceId, bool IsCity);
public sealed record BarangayDto(Guid Id, string PsgcCode, string Name, Guid MunicipalityId);

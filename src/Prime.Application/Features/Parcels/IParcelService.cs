using Prime.Application.Common;

namespace Prime.Application.Features.Parcels;

public interface IParcelService
{
    Task<Result<ParcelDto>> CreateAsync(CreateParcelRequest request, CancellationToken cancellationToken = default);
    Task<Result<ParcelDto>> GetByIdAsync(Guid parcelId, CancellationToken cancellationToken = default);
    Task<Result<ParcelDto>> SetGeometryAsync(Guid parcelId, SetParcelGeometryRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ParcelDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
}

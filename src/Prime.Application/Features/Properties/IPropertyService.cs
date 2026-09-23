using Prime.Application.Common;

namespace Prime.Application.Features.Properties;

public interface IPropertyService
{
    Task<Result<PropertyDto>> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default);
    Task<Result<PropertyProfileDto>> GetProfileAsync(Guid propertyId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<PropertyDto>>> SearchAsync(PropertySearchRequest request, CancellationToken cancellationToken = default);
}

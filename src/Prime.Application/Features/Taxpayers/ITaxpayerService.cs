using Prime.Application.Common;
using Prime.Application.Features.Properties;

namespace Prime.Application.Features.Taxpayers;

public interface ITaxpayerService
{
    Task<Result<TaxpayerDto>> CreateAsync(CreateTaxpayerRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxpayerDto>> GetByIdAsync(Guid taxpayerId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<TaxpayerDto>>> SearchAsync(TaxpayerSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an owner to a property. Adds alongside any existing current
    /// owners (supports co-ownership) rather than closing them out — a
    /// full ownership-transfer workflow (closing the previous owner,
    /// recording a PropertyTransaction) is a later-phase concern, not
    /// implemented here. See CLAUDE.md §35.
    /// </summary>
    Task<Result<PropertyOwnerDto>> AddOwnerAsync(AddPropertyOwnerRequest request, CancellationToken cancellationToken = default);

    Task<Result<PropertyOwnerDto>> EndPartyAsync(Guid propertyTaxpayerId, EndPropertyPartyRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PropertyOwnerDto>>> GetOwnershipHistoryAsync(Guid propertyId, CancellationToken cancellationToken = default);
}

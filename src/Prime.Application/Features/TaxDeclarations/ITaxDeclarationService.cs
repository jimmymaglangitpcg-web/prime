using Prime.Application.Common;

namespace Prime.Application.Features.TaxDeclarations;

public interface ITaxDeclarationService
{
    Task<Result<TaxDeclarationDto>> CreateAsync(CreateTaxDeclarationRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxDeclarationDto>> GetByIdAsync(Guid taxDeclarationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TaxDeclarationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
}

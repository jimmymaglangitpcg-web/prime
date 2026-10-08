using Prime.Application.Common;

namespace Prime.Application.Features.TaxDeclarations;

public interface ITaxDeclarationService
{
    Task<Result<TaxDeclarationDto>> CreateAsync(CreateTaxDeclarationRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxDeclarationDto>> GetByIdAsync(Guid taxDeclarationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TaxDeclarationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);

    // Lifecycle (docs/FORMS-REVISION-PLAN.md §5 A4)
    Task<Result<TaxDeclarationDto>> SubmitForReviewAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Signs the next approval step (or the maker-checker approval). On final approval, cancels the TD this one replaces.</summary>
    Task<Result<TaxDeclarationDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TaxDeclarationDto>> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    /// <summary>Cancels an approved TD outright (no successor), e.g. a duplicate declaration.</summary>
    /// <summary>Asks for an approved TD to be cancelled outright; it is cancelled when a second user approves (workflow-security.md Q19).</summary>
    Task<Result<TaxDeclarationDto>> RequestCancellationAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    /// <summary>Cancels the TD of an open request, by a known user other than the requester (CLAUDE.md §46).</summary>
    Task<Result<TaxDeclarationDto>> ApproveCancellationAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<Result<TaxDeclarationDto>> RejectCancellationAsync(Guid requestId, string reason, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TaxDeclarationCancellationRequestDto>>> ListCancellationRequestsAsync(Guid id, CancellationToken cancellationToken = default);

    // Annotations
    Task<Result<IReadOnlyList<TaxDeclarationAnnotationDto>>> ListAnnotationsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TaxDeclarationAnnotationDto>> AddAnnotationAsync(Guid id, AddTaxDeclarationAnnotationRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxDeclarationAnnotationDto>> LiftAnnotationAsync(Guid annotationId, LiftTaxDeclarationAnnotationRequest request, CancellationToken cancellationToken = default);
}

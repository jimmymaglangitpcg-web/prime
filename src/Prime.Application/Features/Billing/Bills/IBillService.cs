using Prime.Application.Common;

namespace Prime.Application.Features.Billing.Bills;

/// <summary>Tax bill generation and lifecycle (docs/BILLING.md §4, §6.1).</summary>
public interface IBillService
{
    Task<Result<TaxBillDto>> GenerateAsync(GenerateBillRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxBillDto>> PostAsync(Guid billId, CancellationToken cancellationToken = default);
    Task<Result<TaxBillDto>> CancelAsync(Guid billId, string reason, CancellationToken cancellationToken = default);
    Task<Result<TaxBillDto>> GetByIdAsync(Guid billId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TaxBillDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
    Task<Result<StatementOfAccountDto>> GetStatementOfAccountAsync(Guid propertyId, CancellationToken cancellationToken = default);
}

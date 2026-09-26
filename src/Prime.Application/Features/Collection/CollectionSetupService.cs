using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Collection;

namespace Prime.Application.Features.Collection;

public interface ICollectionSetupService
{
    Task<Result<IReadOnlyList<PaymentModeDto>>> ListPaymentModesAsync(CancellationToken cancellationToken = default);
    Task<Result<PaymentModeDto>> CreatePaymentModeAsync(CreatePaymentModeRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RevenueAccountMappingDto>>> ListAccountMappingsAsync(CancellationToken cancellationToken = default);
    Task<Result<RevenueAccountMappingDto>> CreateAccountMappingAsync(CreateRevenueAccountMappingRequest request, CancellationToken cancellationToken = default);
    Task<Result<RevenueAccountMappingDto>> ApproveAccountMappingAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// The collection configuration the LGU supplies (docs/analysis/collection.md §3):
/// accepted modes of payment, and the revenue account each collection line is
/// coded to. Mappings are effective-dated and approved by someone other than
/// their creator, like every other configuration (<see cref="ConfigurationApproval"/>).
/// </summary>
public sealed class CollectionSetupService(
    IApplicationDbContext db,
    IValidator<CreatePaymentModeRequest> modeValidator,
    IValidator<CreateRevenueAccountMappingRequest> mappingValidator,
    ICurrentUserService currentUser) : ICollectionSetupService
{
    public async Task<Result<IReadOnlyList<PaymentModeDto>>> ListPaymentModesAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<PaymentModeDto>>(await db.PaymentModes.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new PaymentModeDto(x.Id, x.Code, x.Name, x.Description, x.RequiresReference, x.AllowsChange, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken));

    public async Task<Result<PaymentModeDto>> CreatePaymentModeAsync(CreatePaymentModeRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await modeValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PaymentModeDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.PaymentModes.AnyAsync(x => x.Code == code, cancellationToken))
        {
            return Result.Failure<PaymentModeDto>("PAYMENT_MODE_DUPLICATE", $"A mode of payment with code {code} already exists.");
        }
        var mode = new PaymentMode
        {
            Code = code, Name = request.Name.Trim(), Description = request.Description, SortOrder = request.SortOrder,
            RequiresReference = request.RequiresReference, AllowsChange = request.AllowsChange,
        };
        db.PaymentModes.Add(mode);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new PaymentModeDto(mode.Id, mode.Code, mode.Name, mode.Description, mode.RequiresReference, mode.AllowsChange, mode.SortOrder, mode.IsActive));
    }

    public async Task<Result<IReadOnlyList<RevenueAccountMappingDto>>> ListAccountMappingsAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<RevenueAccountMappingDto>>((await db.RevenueAccountMappings.AsNoTracking().Include(x => x.TaxType)
                .OrderBy(x => x.TaxType!.SortOrder).ThenBy(x => x.TaxType!.Code).ThenBy(x => x.Component).ThenBy(x => x.YearCategory)
                .ThenByDescending(x => x.EffectiveDate)
                .ToListAsync(cancellationToken))
            .Select(ToDto).ToList());

    public async Task<Result<RevenueAccountMappingDto>> CreateAccountMappingAsync(CreateRevenueAccountMappingRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await mappingValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<RevenueAccountMappingDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        if (!await db.TaxTypes.AnyAsync(x => x.Id == request.TaxTypeId, cancellationToken))
        {
            return Result.Failure<RevenueAccountMappingDto>("TAX_TYPE_NOT_FOUND", "No tax type was found with the given id.");
        }
        var mapping = new RevenueAccountMapping
        {
            TaxTypeId = request.TaxTypeId, Component = request.Component, YearCategory = request.YearCategory,
            AccountCode = request.AccountCode.Trim(), AccountName = request.AccountName.Trim(),
            Fund = string.IsNullOrWhiteSpace(request.Fund) ? null : request.Fund.Trim(),
            LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
        };
        db.RevenueAccountMappings.Add(mapping);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(mapping.Id, cancellationToken));
    }

    public async Task<Result<RevenueAccountMappingDto>> ApproveAccountMappingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mapping = await db.RevenueAccountMappings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure<RevenueAccountMappingDto>("REVENUE_ACCOUNT_MAPPING_NOT_FOUND", "No revenue account mapping was found with the given id.");
        }
        var sameScope = db.RevenueAccountMappings.Where(x => x.TaxTypeId == mapping.TaxTypeId
            && x.Component == mapping.Component && x.YearCategory == mapping.YearCategory);
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, sameScope, mapping, "REVENUE_ACCOUNT_MAPPING", cancellationToken) is { } failure)
        {
            return Result.Failure<RevenueAccountMappingDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(await MapAsync(mapping.Id, cancellationToken));
    }

    private async Task<RevenueAccountMappingDto> MapAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.RevenueAccountMappings.AsNoTracking().Include(x => x.TaxType).SingleAsync(x => x.Id == id, ct));

    private static RevenueAccountMappingDto ToDto(RevenueAccountMapping x) => new(
        x.Id, x.TaxTypeId, x.TaxType!.Code, x.Component, x.YearCategory, x.AccountCode, x.AccountName, x.Fund, x.LegalBasis,
        x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);
}

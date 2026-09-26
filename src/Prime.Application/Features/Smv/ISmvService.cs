using Prime.Application.Common;

namespace Prime.Application.Features.Smv;

public interface ISmvService
{
    Task<Result<SmvDto>> CreateSmvAsync(CreateSmvRequest request, CancellationToken cancellationToken = default);
    Task<Result<SmvDto>> ApproveSmvAsync(Guid smvId, CancellationToken cancellationToken = default);
    Task<Result<SmvDto>> GetByIdAsync(Guid smvId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<SmvDto>>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<Result<SmvScheduleDto>> CreateScheduleAsync(Guid smvId, CreateSmvScheduleRequest request, CancellationToken cancellationToken = default);
    Task<Result<SmvScheduleDto>> ApproveScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SmvScheduleDto>>> ListSchedulesAsync(Guid smvId, CancellationToken cancellationToken = default);
}

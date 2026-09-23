namespace Prime.Domain.Enums;

/// <summary>
/// Execution lifecycle of a background job (e.g. <see cref="Entities.GeneralRevisionJob"/>)
/// — distinct from <see cref="WorkflowStatus"/>, which describes maker-checker
/// approval state, not whether the job itself finished running.
/// </summary>
public enum JobExecutionStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
}

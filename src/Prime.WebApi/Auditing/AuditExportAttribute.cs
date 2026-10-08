using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Audit;

namespace Prime.WebApi.Auditing;

/// <summary>
/// Writes an EXPORT audit row for each record a successful action returns: a form issued, a register run
/// (docs/analysis/workflow-security.md §4.3). The record's change is audited as CREATE by the save interceptor as
/// usual; this row records that a document left the system. The returned value (or each item of a returned list)
/// must have an <c>Id</c>; its <c>FormCode</c> and <c>DocumentNumber</c>, if any, are added to the description.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditExportAttribute(string tableName, string what) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null || executed.Result is not ObjectResult { Value: { } value } result
            || result.StatusCode is < 200 or >= 300)
        {
            return;
        }

        var events = context.HttpContext.RequestServices.GetRequiredService<ISecurityEventLog>();
        var items = value is IEnumerable list and not string ? list.Cast<object>() : [value];
        foreach (var item in items)
        {
            if (Read(item, "Id") is not Guid id)
            {
                continue;
            }
            var description = string.Join(" ", new[] { what, Read(item, "FormCode") as string, Read(item, "DocumentNumber") is string number ? $"No. {number}" : null }
                .Where(s => !string.IsNullOrEmpty(s)));
            await events.WriteExportAsync(new ExportEvent(AuditTrailService.ExportModule, tableName, id, description, ExportFormats.Issued),
                CancellationToken.None); // the document has left; its row is written even if the client went away
        }
    }

    private static object? Read(object item, string property) => item.GetType().GetProperty(property)?.GetValue(item);
}

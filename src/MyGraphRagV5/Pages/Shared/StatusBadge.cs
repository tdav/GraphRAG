using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyGraphRagV5.Data;

namespace MyGraphRagV5.Pages.Shared;

/// <summary>
/// Maps enum statuses to the Ember semantic role (CSS class + Russian text label). Status is
/// never conveyed by color alone -- callers always render both <see cref="CssClass"/> and
/// <see cref="Label"/> together.
/// </summary>
public static class StatusBadge
{
    public static (string CssClass, string Label) ForRunStatus(IndexingRunStatus status) => status switch
    {
        IndexingRunStatus.Pending => ("badge badge-muted", "Ожидание"),
        IndexingRunStatus.Running => ("badge badge-info", "Выполняется"),
        IndexingRunStatus.Succeeded => ("badge badge-success", "Завершён"),
        IndexingRunStatus.Failed => ("badge badge-danger", "Ошибка"),
        IndexingRunStatus.Cancelled => ("badge badge-muted", "Отменён"),
        _ => ("badge badge-muted", status.ToString()),
    };

    public static (string CssClass, string Label) ForHealthStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => ("badge badge-success", "Работает"),
        HealthStatus.Degraded => ("badge badge-warning", "Деградация"),
        HealthStatus.Unhealthy => ("badge badge-danger", "Недоступен"),
        _ => ("badge badge-muted", status.ToString()),
    };
}

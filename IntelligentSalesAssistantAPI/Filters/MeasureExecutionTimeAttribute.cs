using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace IntelligentSalesAssistantAPI.Filters
{
    // Action filter som mäter exekveringstiden för en action och loggar den
    public class MeasureExecutionTimeAttribute : ActionFilterAttribute
    {
        private Stopwatch? _stopwatch;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            _stopwatch = Stopwatch.StartNew();
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            _stopwatch?.Stop();
            var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<MeasureExecutionTimeAttribute>)) as ILogger;
            logger?.LogInformation($"[ExecutionTime] {context.ActionDescriptor.DisplayName}: {_stopwatch?.ElapsedMilliseconds} ms");
        }
    }
}
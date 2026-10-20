using Framework.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSuite.Api.Middleware;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var status = exception is DomainException domain ? domain.StatusCode : 500;
        if (status >= 500) logger.LogError(exception, "Request failed. TraceId: {TraceId}", http.TraceIdentifier);
        else if (status is 403 or 404) logger.LogWarning("Access or resource request denied. Status: {Status}, TraceId: {TraceId}", status, http.TraceIdentifier);
        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = status >= 500 ? "An unexpected error occurred." : exception.Message }
        });
    }
}

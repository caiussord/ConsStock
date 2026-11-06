using ConsStock.Application.Stock;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConsStock.Api;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException => (404, "Recurso não encontrado."),
            StockConflictException => (409, "Saldo alterado por outra operação."),
            InvalidOperationException { Message: "Saldo insuficiente." } => (409, "Saldo insuficiente."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (409, "Valor único já cadastrado."),
            ArgumentException or OverflowException => (400, "Dados inválidos."),
            _ => (0, "")
        };
        if (status == 0) return false;
        logger.LogWarning(exception, "Requisição rejeitada com status {Status}", status);
        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Instance = context.Request.Path
            }
        });
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SmartLog.Core.Helpers;

/// <summary>
/// Formata retorno de erros de validação de modelo
/// </summary>
public class ValidationProblemDetailsResult : IActionResult
{
    /// <summary>
    /// Apenas para personalizar a saida de erros de modelState
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value.Errors)
            .Select(e => e.ErrorMessage)
        .ToList();

        var objectResult = new ObjectResult(new { Erros = errors }) { StatusCode = StatusCodes.Status400BadRequest };

        await objectResult.ExecuteResultAsync(context);
    }
}
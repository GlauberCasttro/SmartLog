using System.ComponentModel.DataAnnotations;

namespace SmartLog.Core.Helpers;

/// <summary>
/// Validation
/// </summary>
internal class SerilogLevelValidAttribute : ValidationAttribute
{
    /// <summary>
    /// Apenas para validar o model de entrada
    /// </summary>
    /// <param name="value"></param>
    /// <param name="validationContext"></param>
    /// <returns></returns>
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var level = value as string;
        if (string.IsNullOrWhiteSpace(level))
            return new ValidationResult("O nível do Serilog no pode ser nulo ou vazio.");

        if (!HelpersService.TryGetSerilogLevel(level, out _, out var errorMessage))
            return new ValidationResult(errorMessage);

        return ValidationResult.Success;
    }
}

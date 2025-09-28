using FluentAssertions;
using SmartLog.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Testes.Controllers;

/// <summary>
/// Testes unitários para o modelo ChangeLogLevelRequest
/// </summary>
public class ChangeLogLevelRequestTests
{
    #region Testes de Validação Individuais

    [Fact]
    public void Validate_ComLevelNulo_DeveRetornarErroLevelObrigatorio()
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = null,
            TimeExpirationInMinute = 30
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(vr => vr.ErrorMessage.Contains("O nível do Serilog no pode ser nulo ou vazio."));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Validate_ComLevelVazioOuEspacos_DeveRetornarErroLevelObrigatorio(string level)
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = level,
            TimeExpirationInMinute = 30
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(vr => vr.ErrorMessage.Contains("O nível do Serilog no pode ser nulo ou vazio."));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_ComTimeExpirationInvalido_DeveRetornarErroTimeExpiration(int timeExpiration)
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = "Debug",
            TimeExpirationInMinute = timeExpiration
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(vr => 
            vr.ErrorMessage == "TimeExpiration deve ser > 0");
    }

    [Theory]
    [InlineData("InvalidLevel")]
    [InlineData("INVALID")]
    [InlineData("Debug123")]
    [InlineData("info")]
    [InlineData("warn")]
    public void Validate_ComLevelInvalido_DeveRetornarErroLevelInvalido(string invalidLevel)
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = invalidLevel,
            TimeExpirationInMinute = 30
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().NotBeEmpty();
            validationResults.First().ErrorMessage.Should().Contain("Log level não suportadado:");
        }
    [Theory]
    [InlineData("Error")]
    [InlineData("Fatal")]
    public void ValidateComNiveisValidosSerilogNaoDeveRetornarErros(string validLevel)
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = validLevel,
            TimeExpirationInMinute = 30
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().BeEmpty($"O nível '{validLevel}' deveria ser válido");
    }

    [Theory]
    [InlineData("verbose")]
    [InlineData("debug")]
    [InlineData("information")]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("fatal")]
    public void Validate_ComNiveisValidosMinusculas_NaoDeveRetornarErros(string validLevel)
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = validLevel,
            TimeExpirationInMinute = 30
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().BeEmpty($"O nível '{validLevel}' em minúsculas deveria ser aceito pelo HelpersService (case-insensitive)");
    }

    #endregion

    #region Testes de TimeExpiration Válidos

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(1440)] // 24 horas
    [InlineData(int.MaxValue)]
    public void Validate_ComTimeExpirationValido_NaoDeveRetornarErros(int validTimeExpiration)
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = "Debug",
            TimeExpirationInMinute = validTimeExpiration
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().BeEmpty($"TimeExpiration {validTimeExpiration} deveria ser válido");
    }

    #endregion

    #region Testes de Cenários Combinados

    [Fact]
    public void Validate_ComTodosOsErrosPossiveis_DeveRetornarTodosOsErros()
    {
        // Arrange - Pior cenário possível
        var request = new ChangeLogLevelRequest
        {
            Level = "", // Level vazio
            TimeExpirationInMinute = -1 // TimeExpiration inválido
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert - Fail-fast completo: todos os erros de uma vez
        validationResults.Should().HaveCount(2);
        validationResults.Should().Contain(vr => vr.ErrorMessage.Contains("O nível do Serilog no pode ser nulo ou vazio."));
        validationResults.Should().Contain(vr => vr.ErrorMessage == "TimeExpiration deve ser > 0");
    }

    [Fact]
    public void Validate_ComRequestValido_NaoDeveRetornarErros()
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = "Debug",
            TimeExpirationInMinute = 30
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().BeEmpty("Request válido não deveria ter erros de validação");
    }

    [Fact]
    public void Validate_ComLevelValidoMasTimeExpirationInvalido_DeveRetornarApenasErroTimeExpiration()
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = "Information",
            TimeExpirationInMinute = 0
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().HaveCount(1);
        validationResults.Should().Contain(vr => 
            vr.ErrorMessage == "TimeExpiration deve ser > 0" );
    }

    [Fact]
    public void Validate_ComTimeExpirationValidoMasLevelInvalido_DeveRetornarApenasErroLevel()
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = "InvalidLevel",
            TimeExpirationInMinute = 30
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        validationResults.Should().HaveCount(1);
        validationResults.First().ErrorMessage.Should().Contain("Log level não suportadado:");
    }

    #endregion

    #region Testes de Propriedades

    [Fact]
    public void Level_DevePermitirGetESet()
    {
        // Arrange
        var request = new ChangeLogLevelRequest();
        const string expectedLevel = "Debug";

        // Act
        request.Level = expectedLevel;

        // Assert
        request.Level.Should().Be(expectedLevel);
    }

    [Fact]
    public void TimeExpiration_DevePermitirGetESet()
    {
        // Arrange
        var request = new ChangeLogLevelRequest();
        const int expectedTime = 60;

        // Act
        request.TimeExpirationInMinute = expectedTime;

        // Assert
        request.TimeExpirationInMinute.Should().Be(expectedTime);
    }

    [Fact]
    public void Request_DeveTerValoresPadraoCorretos()
    {
        // Arrange & Act
        var request = new ChangeLogLevelRequest();

        // Assert
        request.Level.Should().BeNull("Level deveria ser null por padrão");
        request.TimeExpirationInMinute.Should().Be(0, "TimeExpiration deveria ser 0 por padrão");
    }

    #endregion

    #region Testes de Validação com Context

    [Fact]
    public void Validate_ComValidationContextNulo_NaoDeveLancarExcecao()
    {
        // Arrange
        var request = new ChangeLogLevelRequest
        {
            Level = "Debug",
            TimeExpirationInMinute = 30
        };

        // Act & Assert
        var act = () => {
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(request, null, validationResults, true);
        };

        act.Should().Throw<ArgumentNullException>("Validator exige ValidationContext não nulo");
    }

    #endregion
}

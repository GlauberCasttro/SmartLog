using FluentAssertions;
using Serilog.Events;
using SmartLog.Core.Enums;
using SmartLog.Core.Helpers;
using StackExchange.Redis;
using System.Globalization;

namespace SmartLog.Testes.Helpers;

public class HelpersServiceTests
{
    #region FormatLogLevelMessage Tests

    [Fact]
    public void FormatLogLevelMessage_ComParametrosValidos_DeveFormatarCorretamente()
    {
        // Arrange
        var level = LogEventLevel.Information;
        var changeType = LogChangeType.Manual;
        var duration = 30;
        var expectedDateTime = DateTime.UtcNow.AddMinutes(duration);

        // Act
        var result = HelpersService.FormatLogLevelMessage(level, changeType, duration);

        // Assert
        result.Should().StartWith("Information:Manual:");
        
        // Verificar se a data está no formato correto
        var parts = result.Split(':');
        parts.Should().HaveCount(5); // Level:Type:DD/MM/YYYY HH:MM:SS = 5 partes
        parts[0].Should().Be("Information");
        parts[1].Should().Be("Manual");
        
        // Reconstruir a data das partes 2, 3 e 4
        var dateString = $"{parts[2]}:{parts[3]}:{parts[4]}";
        var parsedDate = DateTime.ParseExact(dateString, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        parsedDate.Should().BeCloseTo(expectedDateTime, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(LogEventLevel.Debug, LogChangeType.Automatico, 15)]
    [InlineData(LogEventLevel.Warning, LogChangeType.Manual, 60)]
    [InlineData(LogEventLevel.Error, LogChangeType.Automatico, 0)]
    [InlineData(LogEventLevel.Fatal, LogChangeType.Manual, 120)]
    public void FormatLogLevelMessage_ComDiferentesParametros_DeveFormatarCorretamente(
        LogEventLevel level, LogChangeType changeType, int duration)
    {
        // Act
        var result = HelpersService.FormatLogLevelMessage(level, changeType, duration);

        // Assert
        result.Should().StartWith($"{level}:{changeType}:");
        
        var parts = result.Split(':');
        parts.Should().HaveCount(5); // Level:Type:DD/MM/YYYY HH:MM:SS = 5 partes
        parts[0].Should().Be(level.ToString());
        parts[1].Should().Be(changeType.ToString());
        
        // Reconstruir e verificar se a data é válida
        var dateString = $"{parts[2]}:{parts[3]}:{parts[4]}";
        var isValidDate = DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _);
        isValidDate.Should().BeTrue();
    }

    #endregion

    #region TryParseLogMessage Tests

    [Fact]
    public void TryParseLogMessage_ComMensagemValida_DeveRetornarTrueEValoresCorretos()
    {
        // Arrange
        var expectedLevel = LogEventLevel.Information;
        var expectedType = LogChangeType.Manual;
        var expectedExpiration = new DateTime(2025, 8, 20, 15, 30, 0);
        var dateString = expectedExpiration.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var message = $"{expectedLevel}:{expectedType}:{dateString}";
        RedisValue redisValue = message;

        // Act
        var success = redisValue.TryParseLogMessage(out var level, out var type, out var expiration);

        // Assert
        success.Should().BeTrue();
        level.Should().Be(expectedLevel);
        type.Should().Be(expectedType);
        expiration.Should().Be(expectedExpiration);
    }

    [Theory]
    [InlineData("Debug:Automatico:20/08/2025 10:30:00", LogEventLevel.Debug, LogChangeType.Automatico)]
    [InlineData("Warning:Manual:01/01/2026 23:59:59", LogEventLevel.Warning, LogChangeType.Manual)]
    [InlineData("Error:Automatico:15/12/2025 08:15:30", LogEventLevel.Error, LogChangeType.Automatico)]
    public void TryParseLogMessage_ComDiferentesMensagensValidas_DeveRetornarTrueEValoresCorretos(
        string message, LogEventLevel expectedLevel, LogChangeType expectedType)
    {
        // Arrange
        RedisValue redisValue = message;

        // Act
        var success = redisValue.TryParseLogMessage(out var level, out var type, out var expiration);

        // Assert
        success.Should().BeTrue();
        level.Should().Be(expectedLevel);
        type.Should().Be(expectedType);
        expiration.Should().BeAfter(DateTime.MinValue);
    }

    [Fact]
    public void TryParseLogMessage_ComRedisValueNulo_DeveRetornarFalse()
    {
        // Arrange
        RedisValue redisValue = RedisValue.Null;

        // Act
        var success = redisValue.TryParseLogMessage(out var level, out var type, out var expiration);

        // Assert
        success.Should().BeFalse();
        level.Should().Be(default);
        type.Should().Be(default);
        expiration.Should().Be(default);
    }

    [Fact]
    public void TryParseLogMessage_ComRedisValueVazio_DeveRetornarFalse()
    {
        // Arrange
        RedisValue redisValue = string.Empty;

        // Act
        var success = redisValue.TryParseLogMessage(out var level, out var type, out var expiration);

        // Assert
        success.Should().BeFalse();
        level.Should().Be(default);
        type.Should().Be(default);
        expiration.Should().Be(default);
    }

    [Theory]
    [InlineData("Information")] // Sem dois pontos
    [InlineData("Information:Manual")] // Apenas um dois pontos
    [InlineData("Information:Manual:InvalidDate")] // Data inválida
    [InlineData("InvalidLevel:Manual:20/08/2025 10:30:00")] // Nível inválido
    [InlineData("Information:InvalidType:20/08/2025 10:30:00")] // Tipo inválido
    [InlineData(":Manual:20/08/2025 10:30:00")] // Nível vazio
    [InlineData("Information::20/08/2025 10:30:00")] // Tipo vazio
    [InlineData("Information:Manual:")] // Data vazia
    public void TryParseLogMessage_ComMensagensInvalidas_DeveRetornarFalse(string invalidMessage)
    {
        // Arrange
        RedisValue redisValue = invalidMessage;

        // Act
        var success = redisValue.TryParseLogMessage(out var _, out var _, out var _);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void TryParseLogMessage_ComFormatoDataIncorreto_DeveRetornarFalse()
    {
        // Arrange
        var message = "Information:Manual:2025-08-20 15:30:00"; // Formato ISO ao invés de dd/MM/yyyy
        RedisValue redisValue = message;

        // Act
        var success = redisValue.TryParseLogMessage(out var _, out var _, out var _);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void TryParseLogMessage_ComCaseInsensitive_DeveRetornarTrue()
    {
        // Arrange
        var message = "information:manual:20/08/2025 10:30:00"; // Letras minúsculas
        RedisValue redisValue = message;

        // Act
        var success = redisValue.TryParseLogMessage(out var level, out var type, out var _);

        // Assert
        success.Should().BeTrue();
        level.Should().Be(LogEventLevel.Information);
        type.Should().Be(LogChangeType.Manual);
    }

    [Fact]
    public void TryParseLogMessage_ComEspacosExtras_DeveTratarCorretamente()
    {
        // Arrange
        var message = "Information:Manual: 20/08/2025 10:30:00 "; // Espaços extras na data
        RedisValue redisValue = message;

        // Act
        var success = redisValue.TryParseLogMessage(out var level, out var type, out var expiration);

        // Assert
        success.Should().BeTrue();
        level.Should().Be(LogEventLevel.Information);
        type.Should().Be(LogChangeType.Manual);
        expiration.Should().Be(new DateTime(2025, 8, 20, 10, 30, 0));
    }

    [Fact]
    public void TryParseLogMessage_ComMuitsosDosPontos_DeveUsarApenasOsPrimeiros()
    {
        // Arrange
        var message = "Information:Manual:Extra:20/08/2025 10:30:00"; // Dois pontos extras
        RedisValue redisValue = message;

        // Act
        var success = redisValue.TryParseLogMessage(out var _, out var _, out var _);

        // Assert
        success.Should().BeFalse(); // Deve falhar porque "Extra:20/08/2025 10:30:00" não é uma data válida
    }

    #endregion

    #region Testes de Integração

    [Fact]
    public void FormatLogLevelMessage_E_TryParseLogMessage_DevemSerConsistentes()
    {
        // Arrange
        var originalLevel = LogEventLevel.Warning;
        var originalType = LogChangeType.Automatico;
        var duration = 45;

        // Act - Formatar e depois fazer parse
        var formattedMessage = HelpersService.FormatLogLevelMessage(originalLevel, originalType, duration);
        RedisValue redisValue = formattedMessage;
        var parseSuccess = redisValue.TryParseLogMessage(out var parsedLevel, out var parsedType, out var parsedExpiration);

        // Assert
        parseSuccess.Should().BeTrue();
        parsedLevel.Should().Be(originalLevel);
        parsedType.Should().Be(originalType);
        
        // A data de expiração deve estar próxima ao esperado
        var expectedExpiration = DateTime.UtcNow.AddMinutes(duration);
        parsedExpiration.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    #endregion
}

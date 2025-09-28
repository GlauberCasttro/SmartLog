using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Serilog.Core;
using Serilog.Events;
using SmartLog.Core.Controllers;
using SmartLog.Core.Enums;
using SmartLog.Core.Helpers;
using SmartLog.Core.Interfaces;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Testes.Controllers
{
    /// <summary>
    /// Testes unitários para métodos específicos do SmartLogController
    /// </summary>
    public class SmartLogControllerTests
    {
        private readonly Mock<ISmartLogEconomyDetector> _mockDetector;
        private readonly Mock<ILogLevelSwitcherService> _mockSwitcher;
        private readonly MetricsRegistry _registry;
        private readonly LoggingLevelSwitch _levelSwitch;
        private readonly SmartLogOptions _options;
        private readonly SmartLogController _controller;

        public SmartLogControllerTests()
        {
            // Setup
            _options = new SmartLogOptions("test")
            {
                CircularBufferSize = 100,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
            };

            _registry = new MetricsRegistry(_options);
            _levelSwitch = new LoggingLevelSwitch(_options.EconomyLevel);
            _mockDetector = new Mock<ISmartLogEconomyDetector>();
            _mockSwitcher = new Mock<ILogLevelSwitcherService>();

            _controller = new SmartLogController(
                _mockDetector.Object,
                _registry,
                _levelSwitch,
                _options,
                _mockSwitcher.Object);
        }

        #region GetLogEventLevelNames Tests

        [Fact]
        public void GetLogEventLevelNames_QuandoChamado_DeveRetornarListaDeNiveis()
        {
            // Act
            var result = _controller.GetLogEventLevelNames();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;

            okResult.Value.Should().NotBeNull();
            var levelNames = okResult.Value as string[];

            levelNames.Should().NotBeNull()
                .And.NotBeEmpty()
                .And.Contain(["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"]);
        }

        [Fact]
        public void GetLogEventLevelNames_QuandoChamado_DeveRetornarNiveisValidos()
        {
            // Act
            var result = _controller.GetLogEventLevelNames();
            var okResult = result as OkObjectResult;
            var levelNames = okResult.Value as string[];

            // Assert - Verifica se todos os níveis retornados são válidos no Serilog
            foreach (var levelName in levelNames)
            {
                var isValid = HelpersService.TryGetSerilogLevel(levelName, out _, out _);
                isValid.Should().BeTrue($"O nível '{levelName}' deveria ser válido no Serilog");
            }
        }

        #endregion GetLogEventLevelNames Tests

        #region ChangeLevel Tests

        [Fact]
        public async Task ChangeLevel_ComRequestValido_DeveRetornarAccepted()
        {
            // Arrange
            var request = new ChangeLogLevelRequest
            {
                Level = "Debug",
                TimeExpirationInMinute = 30
            };

            // Act
            var result = await _controller.ChangeLevel(request);

            // Assert
            result.Should().BeOfType<AcceptedResult>();

            _mockSwitcher.Verify(s => s.SwitchLevelAsync(
                LogEventLevel.Debug,
                LogChangeType.Manual,
                30),
                Times.Once);
        }

        [Theory]
        [InlineData("Verbose")]
        [InlineData("Debug")]
        [InlineData("Information")]
        [InlineData("Warning")]
        [InlineData("Error")]
        [InlineData("Fatal")]
        public async Task ChangeLevel_ComTodosNiveisValidos_DeveAceitarTodos(string level)
        {
            // Arrange
            var request = new ChangeLogLevelRequest
            {
                Level = level,
                TimeExpirationInMinute = 15
            };

            // Act
            var result = await _controller.ChangeLevel(request);

            // Assert
            result.Should().BeOfType<AcceptedResult>($"O nível '{level}' deveria ser aceito");

            _mockSwitcher.Verify(s => s.SwitchLevelAsync(
                It.IsAny<LogEventLevel>(),
                LogChangeType.Manual,
                15),
                Times.Once);
        }

        [Fact]
        public void ChangeLevel_ComLevelNulo_DeveFalharValidacao()
        {
            // Arrange
            var request = new ChangeLogLevelRequest
            {
                Level = null,
                TimeExpirationInMinute = 30
            };

            // Simula validação do modelo
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            // Assert
            isValid.Should().BeFalse("Level nulo não deveria ser válido");
            validationResults.Should().Contain(vr =>
                vr.ErrorMessage == "O nível do Serilog no pode ser nulo ou vazio.");
        }

        [Fact]
        public void ChangeLevel_ComTimeExpirationZero_DeveFalharValidacao()
        {
            // Arrange
            var request = new ChangeLogLevelRequest
            {
                Level = "Debug",
                TimeExpirationInMinute = 0
            };

            // Simula validação do modelo
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            // Assert
            isValid.Should().BeFalse("TimeExpiration zero não deveria ser válido");
            validationResults.Should().NotBeEmpty();
            validationResults.Should().Contain(vr => vr.ErrorMessage == "TimeExpiration deve ser > 0");
        }

        [Fact]
        public void ChangeLevel_ComLevelInvalido_DeveFalharValidacao()
        {
            // Arrange
            var request = new ChangeLogLevelRequest
            {
                Level = "InvalidLevel",
                TimeExpirationInMinute = 30
            };

            // Simula validação do modelo
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            // Assert
            isValid.Should().BeFalse("Level inválido não deveria ser aceito");
            validationResults.Should().NotBeEmpty();
            validationResults.First().ErrorMessage.Should().Contain("Unsupported log level");
        }

        [Fact]
        public void ChangeLevel_ComTodosErros_DeveRetornarTodosErrosDeUmaVez()
        {
            // Arrange
            var request = new ChangeLogLevelRequest
            {
                Level = null, // Level nulo ou vazio
                TimeExpirationInMinute = -1 // TimeExpiration negativo
            };

            // Simula validação do modelo
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            // Assert
            isValid.Should().BeFalse("Deve falhar validação com múltiplos erros");
            validationResults.Should().HaveCount(2, "Devem ocorrer dois erros: TimeExpiration negativo e Level nulo");
            validationResults.Should().Contain(vr => vr.ErrorMessage == "TimeExpiration deve ser > 0")
                .And.Contain(vr => vr.ErrorMessage == "O nível do Serilog no pode ser nulo ou vazio.");
        }

        [Fact]
        public async Task ChangeLevel_QuandoSwitcherLancaExcecao_DevePropagarExcecao()
        {
            // Arrange
            var request = new ChangeLogLevelRequest
            {
                Level = "Debug",
                TimeExpirationInMinute = 30
            };

            _mockSwitcher.Setup(s => s.SwitchLevelAsync(
                It.IsAny<LogEventLevel>(),
                It.IsAny<LogChangeType>(),
                It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act & Assert
            await FluentActions.Invoking(() => _controller.ChangeLevel(request))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Test exception");
        }

        #endregion ChangeLevel Tests
    }
}
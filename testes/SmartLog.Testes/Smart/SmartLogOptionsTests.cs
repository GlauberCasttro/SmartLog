using FluentAssertions;
using Serilog.Events;
using SmartLog.Core.Models;
using Xunit.Abstractions;

namespace SmartLog.Testes.Smart
{
    /// <summary>
    /// Testes para validação e configuração da SmartLogOptions
    /// </summary>
    public class SmartLogOptionsTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;

        [Fact]
        public void DefaultValues_QuandoCriado_DeveSerValido()
        {
            // Act
            var options = new SmartLogOptions();

            // Assert
            options.CircularBufferSize.Should().Be(1000);
            options.DetectionInterval.Should().Be(TimeSpan.FromMinutes(120));
            options.EconomyLevel.Should().Be(LogEventLevel.Warning);
            options.HighVerbosityLevel.Should().Be(LogEventLevel.Information);
            options.AbsoluteErrorThreshold.Should().Be(30);
            options.LogWindowSeconds.Should().Be(60);
            options.MinimumHighVerbosityDurationInMinute.Should().Be(15);
            options.EnableRedisChannelListener.Should().BeTrue();

            _output.WriteLine("✅ Valores padrão estão corretos");
        }

        [Fact]
        public void WithLogLevels_QuandoChamado_DeveConfigurarCorretamente()
        {
            // Arrange
            var options = new SmartLogOptions();

            // Act
            var result = options.WithLogLevels(LogEventLevel.Error, LogEventLevel.Debug);

            // Assert
            result.Should().BeSameAs(options, "Deve retornar a mesma instância para fluent API");
            options.EconomyLevel.Should().Be(LogEventLevel.Error);
            options.HighVerbosityLevel.Should().Be(LogEventLevel.Debug);

            _output.WriteLine($"✅ Níveis configurados: Economy={options.EconomyLevel}, High={options.HighVerbosityLevel}");
        }

        [Fact]
        public void WithTimings_QuandoChamado_DeveConfigurarCorretamente()
        {
            // Arrange
            var options = new SmartLogOptions();
            var interval = TimeSpan.FromMinutes(5);

            // Act
            var result = options.WithTimings(interval, 120);

            // Assert
            result.Should().BeSameAs(options);
            options.DetectionInterval.Should().Be(interval);
            options.LogWindowSeconds.Should().Be(120);

            _output.WriteLine($"✅ Timings configurados: Interval={options.DetectionInterval}, Window={options.LogWindowSeconds}s");
        }

        [Fact]
        public void WithThresholds_QuandoChamado_DeveConfigurarCorretamente()
        {
            // Arrange
            var options = new SmartLogOptions();

            // Act
            var result = options.WithThresholds(50, 10);

            // Assert
            result.Should().BeSameAs(options);
            options.AbsoluteErrorThreshold.Should().Be(50);
            options.MinimumHighVerbosityDurationInMinute.Should().Be(10);

            _output.WriteLine($"✅ Thresholds configurados: Errors={options.AbsoluteErrorThreshold}, Duration={options.MinimumHighVerbosityDurationInMinute}min");
        }

        [Fact]
        public void ForDevelopment_QuandoChamado_DeveConfigurarPadroesDesenvolvimento()
        {
            // Arrange
            var options = new SmartLogOptions();

            // Act
            var result = options.ForDevelopment();

            // Assert
            result.Should().BeSameAs(options);
            options.EconomyLevel.Should().Be(LogEventLevel.Error);
            options.HighVerbosityLevel.Should().Be(LogEventLevel.Debug);
            options.DetectionInterval.Should().Be(TimeSpan.FromSeconds(30));
            options.LogWindowSeconds.Should().Be(60);
            options.AbsoluteErrorThreshold.Should().Be(5);
            options.MinimumHighVerbosityDurationInMinute.Should().Be(2);
            options.EnableRedisChannelListener.Should().BeFalse();

            _output.WriteLine("✅ Configuração de desenvolvimento aplicada");
        }

        [Fact]
        public void ForProduction_QuandoChamado_DeveConfigurarPadroesProducao()
        {
            // Arrange
            var options = new SmartLogOptions(); // AppName é obrigatório para validação

            // Act
            var result = options.ForProduction();

            // Assert
            result.Should().BeSameAs(options);
            options.EconomyLevel.Should().Be(LogEventLevel.Error);
            options.HighVerbosityLevel.Should().Be(LogEventLevel.Information);
            options.DetectionInterval.Should().Be(TimeSpan.FromMinutes(2));
            options.LogWindowSeconds.Should().Be(300);
            options.AbsoluteErrorThreshold.Should().Be(30);
            options.MinimumHighVerbosityDurationInMinute.Should().Be(15);
            options.EnableRedisChannelListener.Should().BeTrue();

            _output.WriteLine("✅ Configuração de produção aplicada");
        }

        [Theory]
        [InlineData(-1, 60, 30, 15)]  // DetectionInterval inválido
        [InlineData(60, -1, 30, 15)]  // LogWindowSeconds inválido
        [InlineData(60, 60, -1, 15)]  // AbsoluteErrorThreshold inválido
        [InlineData(60, 60, 30, -1)]  // MinimumHighVerbosityDurationIntMinute inválido
        public void Validate_ComValoresInvalidos_DeveLancarArgumentException(
            int detectionIntervalSeconds, int logWindowSeconds, int absoluteErrorThreshold,
            int minimumHighVerbosityMinutes)
        {
            // Arrange
            var options = new SmartLogOptions
            {
                DetectionInterval = TimeSpan.FromSeconds(detectionIntervalSeconds),
                LogWindowSeconds = logWindowSeconds,
                AbsoluteErrorThreshold = absoluteErrorThreshold,
                MinimumHighVerbosityDurationInMinute = minimumHighVerbosityMinutes,
            };

            // Act & Assert
            options.Invoking(o => o.Validate())
                   .Should().Throw<ArgumentException>()
                   .WithMessage("*deve ser maior que zero*", "Deveria validar valores positivos");

            _output.WriteLine($"✅ Validação rejeitou corretamente valores inválidos");
        }

        [Fact]
        public void Validate_ComAltaVerbosidadeMaiorQueEconomia_DeveLancarArgumentException()
        {
            // Arrange
            var options = new SmartLogOptions
            {
                EconomyLevel = LogEventLevel.Debug,        // Menor número = mais verboso
                HighVerbosityLevel = LogEventLevel.Error,  // Maior número = menos verboso
            };

            // Act & Assert
            options.Invoking(o => o.Validate())
                   .Should().Throw<ArgumentException>()
                   .WithMessage("*alta verbosidade deve ser menor*",
                                "HighVerbosity deve ter valor numérico menor que Economy");

            _output.WriteLine("✅ Validação rejeitou níveis de log inconsistentes");
        }

        [Fact]
        public void Validate_ComConfiguracaoValida_NaoDeveLancarExcecao()
        {
            // Arrange
            var options = new SmartLogOptions
            {
                DetectionInterval = TimeSpan.FromMinutes(2),
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 10,
                MinimumHighVerbosityDurationInMinute = 5,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
            };

            // Act & Assert
            options.Invoking(o => o.Validate()).Should().NotThrow();

            _output.WriteLine("✅ Configuração válida passou na validação");
        }

        [Fact]
        public void FluentConfiguration_QuandoUsado_DeveFuncionarCorretamente()
        {
            // Act
            var options = new SmartLogOptions()
                .WithLogLevels(LogEventLevel.Error, LogEventLevel.Debug)
                .WithTimings(TimeSpan.FromMinutes(3), 180)
                .WithThresholds(25, 8)
                .EnableRedis();

            // Assert
            options.EconomyLevel.Should().Be(LogEventLevel.Error);
            options.HighVerbosityLevel.Should().Be(LogEventLevel.Debug);
            options.DetectionInterval.Should().Be(TimeSpan.FromMinutes(3));
            options.LogWindowSeconds.Should().Be(180);
            options.AbsoluteErrorThreshold.Should().Be(25);
            options.MinimumHighVerbosityDurationInMinute.Should().Be(8);
            options.EnableRedisChannelListener.Should().BeTrue();

            _output.WriteLine("✅ API fluente funcionando corretamente");
        }
    }
}

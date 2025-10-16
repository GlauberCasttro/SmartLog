using FluentAssertions;
using Moq;
using Serilog.Core;
using Serilog.Events;
using SmartLog.Core.Enums;
using SmartLog.Core.Interfaces;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Xunit.Abstractions;

namespace SmartLog.Testes.Smart
{
    /// <summary>
    /// Testes para o detector de economia de logs
    /// </summary>
    public class SmartLogEconomyDetectorTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ILogLevelSwitcherService> _mockSwitcher;
        private readonly SmartLogOptions _options;
        private readonly MetricsRegistry _registry;
        private readonly LoggingLevelSwitch _levelSwitch;

        public SmartLogEconomyDetectorTests(ITestOutputHelper output)
        {
            _output = output;

            // Setup mocks
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockSwitcher = new Mock<ILogLevelSwitcherService>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                     .Returns(_mockDatabase.Object);

            // Setup test configuration
            _options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 2,
            };

            _registry = new MetricsRegistry(_options);
            _levelSwitch = new LoggingLevelSwitch(_options.EconomyLevel);
        }

        [Fact]
        public void Constructor_QuandoChamado_DeveInicializarComEstadoCorreto()
        {
            // Act
            var detector = new SmartLogEconomyDetector(
                _options, _registry, _levelSwitch, _mockRedis.Object, _mockSwitcher.Object, enableAutoDetection: false);

            // Assert
            detector.LastDecision.Should().NotBeNull();
            detector.LastDecision.RecommendedLevel.Should().Be(_options.EconomyLevel);
            detector.LastDecision.Reason.Should().Contain("SDK Initialized");
            detector.LastDecision.ShouldSwitchToHighVerbosity.Should().BeFalse();

            _output.WriteLine($"✅ Detector inicializado com nível: {detector.LastDecision.RecommendedLevel}");
        }

        [Fact]
        public async Task RunDetectionCycleAsync_ComPoucosErros_DeveManterModoEconomia()
        {
            // Arrange
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 2
            };

            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);

            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            // Limpa o buffer de eventos antes do teste
            var bufferField = typeof(MetricsRegistry).GetField("_logEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> buffer)
            {
                while (buffer.TryDequeue(out _)) { }
            }

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);

            // Adiciona poucos erros (abaixo do threshold)
            for (int i = 0; i < 3; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }

            // ✅ Pequeno delay para garantir consistência temporal
            await Task.Delay(50);


            // Act
            await detector.RunDetectionCycleAsync();

            // Assert
            detector.LastDecision.ShouldSwitchToHighVerbosity.Should().BeFalse();
            detector.LastDecision.RecommendedLevel.Should().Be(options.EconomyLevel);
            detector.LastDecision.Reason.Should().Contain("within acceptable range");

            _output.WriteLine($"✅ Poucos erros (3): {detector.LastDecision.Reason}");
        }

        private static void InitializeTestDependencies(SmartLogOptions options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher)
        {
            registry = new MetricsRegistry(options);
            levelSwitch = new LoggingLevelSwitch(options.EconomyLevel);
            mockRedis = new Mock<IConnectionMultiplexer>();
            mockDatabase = new Mock<IDatabase>();
            mockSwitcher = new Mock<ILogLevelSwitcherService>();
        }

        [Fact]
        public async Task RunDetectionCycleAsync_ComMuitosErros_DeveMudarParaAltaVerbosidade()
        {
            // Arrange
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 2
            };

            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            // Limpa o buffer de eventos antes do teste
            var bufferField = typeof(MetricsRegistry).GetField("_logEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> buffer)
            {
                while (buffer.TryDequeue(out _)) { }
            }

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);

            // Adiciona muitos erros (acima do threshold)
            for (int i = 0; i < 10; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }

            // Act
            await detector.RunDetectionCycleAsync();

            // Assert
            detector.LastDecision.ShouldSwitchToHighVerbosity.Should().BeTrue();
            detector.LastDecision.RecommendedLevel.Should().Be(options.HighVerbosityLevel);
            detector.LastDecision.Reason.Should().Contain("threshold exceeded");

            // Verifica se tentou alterar o nível
            mockSwitcher.Verify(s => s.SwitchLevelAsync(
                options.HighVerbosityLevel,
                LogChangeType.Automatico,
                options.MinimumHighVerbosityDurationInMinute),
                Times.Once);

            _output.WriteLine($"✅ Muitos erros (10): {detector.LastDecision.Reason}");
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(3, false)]
        [InlineData(5, true)]  // Exatamente no threshold
        [InlineData(7, true)]
        [InlineData(15, true)]
        public async Task RunDetectionCycleAsync_ComVariasQuantidadesErros_DeveComportarCorretamente(
            int errorCount, bool shouldSwitchToHighVerbosity)
        {
            // Arrange
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 2,
            };

            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);

            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            // Limpa o buffer de eventos antes do teste
            var bufferField = typeof(MetricsRegistry).GetField("_logEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> buffer)
            {
                while (buffer.TryDequeue(out _)) { }
            }

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);

            // Adiciona o número específico de erros
            for (int i = 0; i < errorCount; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }

            // ✅ Pequeno delay para garantir consistência temporal
            await Task.Delay(50);

            // Act
            await detector.RunDetectionCycleAsync();

            // Assert
            detector.LastDecision.ShouldSwitchToHighVerbosity.Should().Be(shouldSwitchToHighVerbosity);

            if (shouldSwitchToHighVerbosity)
            {
                detector.LastDecision.RecommendedLevel.Should().Be(options.HighVerbosityLevel);
                mockSwitcher.Verify(s => s.SwitchLevelAsync(It.IsAny<LogEventLevel>(), It.IsAny<LogChangeType>(), It.IsAny<int>()), Times.Once);
            }
            else
            {
                detector.LastDecision.RecommendedLevel.Should().Be(options.EconomyLevel);
                mockSwitcher.Verify(s => s.SwitchLevelAsync(It.IsAny<LogEventLevel>(), It.IsAny<LogChangeType>(), It.IsAny<int>()), Times.Never);
            }

            _output.WriteLine($"✅ {errorCount} erros → {(shouldSwitchToHighVerbosity ? "ALTA" : "ECONOMIA")} verbosidade");
        }

        [Fact]
        public async Task RunDetectionCycleAsync_ComChamadasConcorrentes_NaoDeveInterferir()
        {
            // Arrange
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 2,
            };

            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);

            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            // Limpa o buffer de eventos antes do teste
            var bufferField = typeof(MetricsRegistry).GetField("_logEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> buffer)
            {
                while (buffer.TryDequeue(out _)) { }
            }

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);

            // Adiciona erros para garantir detecção
            for (int i = 0; i < 10; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }

            // ✅ Pequeno delay para garantir consistência temporal
            await Task.Delay(50);

            // Act - Executa múltiplas detecções simultaneamente
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(detector.RunDetectionCycleAsync());
            }

            await Task.WhenAll(tasks);

            // Assert - Verifica que não houve execuções concorrentes descontroladas
            mockSwitcher.Verify(s => s.SwitchLevelAsync(It.IsAny<LogEventLevel>(), It.IsAny<LogChangeType>(), It.IsAny<int>()),
                Times.AtMost(5), "Deve ter no máximo 5 chamadas (uma por task, controlada pelo Interlocked)");

            _output.WriteLine($"✅ Concorrência controlada: {mockSwitcher.Invocations.Count} chamadas para 5 tasks simultâneas");
            _output.WriteLine("✅ Múltiplas execuções simultâneas tratadas corretamente");
        }

        [Fact]
        public void Dispose_QuandoChamado_DeveLimparRecursos()
        {
            // Arrange
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Warning,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 2,
            };

            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);

            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);

            // Act & Assert - Não deve lançar exceção
            detector.Dispose();

            _output.WriteLine("✅ Dispose executado sem exceções");
        }

        [Theory]
        [InlineData(LogEventLevel.Debug, "Mudança para Information: DIMINUI verbosidade (Debug→Info) - DEVE RESPEITAR tempo")]
        public async Task RunDetectionCycleAsync_DiminuindoVerbosidade_DeveRespeitarTempoMinimo(
            LogEventLevel nivelAtual,
            string cenario)
        {
            // Arrange - instâncias isoladas para cada execução
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Information,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 15,
            };

            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);

            levelSwitch.MinimumLevel = nivelAtual;
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            // Limpa o buffer de eventos antes do teste
            var bufferField = typeof(MetricsRegistry).GetField("_logEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> buffer)
            {
                while (buffer.TryDequeue(out _)) { }
            }

            // Simula timestamp MUITO recente (1 minuto atrás) para garantir que ainda não passou do tempo mínimo
            var timestampRecente = DateTime.UtcNow.AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture);
            mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                         .ReturnsAsync(new RedisValue(timestampRecente));

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);

            // NÃO adiciona erros - força sistema a sugerir EconomyLevel (Information)
            // Isso simula Debug → Information (diminuindo verbosidade)
            // ✅ Pequeno delay para garantir consistência temporal
            await Task.Delay(50);

            // Act
            await detector.RunDetectionCycleAsync();

            // Assert - Como diminui verbosidade e tempo é recente, NÃO deve mudar (respeita tempo mínimo)
            mockSwitcher.Verify(
                s => s.SwitchLevelAsync(It.IsAny<LogEventLevel>(), It.IsAny<LogChangeType>(), It.IsAny<int>()),
                Times.Never,
                $"Não deveria mudar devido ao tempo mínimo: {cenario}");

            _output.WriteLine($"🕐 TEMPO RESPEITADO: {cenario}");
        }

        [Theory]
        [InlineData(LogEventLevel.Information, "Mudança para Debug: AUMENTA verbosidade (Info→Debug) - MUDANÇA IMEDIATA")]
        [InlineData(LogEventLevel.Warning, "Mudança para Debug: AUMENTA verbosidade (Warning→Debug) - MUDANÇA IMEDIATA")]
        [InlineData(LogEventLevel.Error, "Mudança para Debug: AUMENTA verbosidade (Error→Debug) - MUDANÇA IMEDIATA")]
        public async Task RunDetectionCycleAsync_AumentandoVerbosidade_DeveMudarImediatamente(
            LogEventLevel nivelAtual,
            string cenario)
        {
            // Arrange - instâncias isoladas para cada execução
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Information,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 2,
            };

            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);

            levelSwitch.MinimumLevel = nivelAtual;
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            // Limpa o buffer de eventos antes do teste
            var bufferField = typeof(MetricsRegistry).GetField("_logEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> buffer)
            {
                while (buffer.TryDequeue(out _)) { }
            }

            // Simula timestamp de mudança recente (5 minutos atrás, menos que MinimumHighVerbosityDurationIntMinute = 15)
            var timestampRecente = DateTime.UtcNow.AddMinutes(-5).ToString("O", CultureInfo.InvariantCulture);
            mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                        .ReturnsAsync(new RedisValue(timestampRecente));
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                     .Returns(mockDatabase.Object);

            // Adiciona erros para forçar mudança para Debug (aumentando verbosidade)
            for (int i = 0; i < 8; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);
            
            // ✅ Pequeno delay para garantir consistência temporal
            await Task.Delay(50);

            // Act
            await detector.RunDetectionCycleAsync();

            // Assert - Como aumenta verbosidade, é situação crítica e deve mudar imediatamente
            mockSwitcher.Verify(
                s => s.SwitchLevelAsync(LogEventLevel.Debug, LogChangeType.Automatico, options.MinimumHighVerbosityDurationInMinute),
                Times.AtLeastOnce(),
                $"Deveria mudar imediatamente: {cenario}");

            // Garante que o buffer está limpo após o ciclo
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> bufferAfter)
            {
                bufferAfter.Count.Should().BeLessOrEqualTo(options.CircularBufferSize);
            }

            _output.WriteLine($"⚡ MUDANÇA IMEDIATA: {cenario}");
        }

        [Fact]
        public async Task RunDetectionCycleAsync_ComTempoMinimoExpirado_DeveMudarIndependenteDaDirecao()
        {
            // Arrange - instâncias isoladas para cada execução
            var options = new SmartLogOptions
            {
                CircularBufferSize = 100,
                LogWindowSeconds = 60,
                AbsoluteErrorThreshold = 5,
                EconomyLevel = LogEventLevel.Information,
                HighVerbosityLevel = LogEventLevel.Debug,
                MinimumHighVerbosityDurationInMinute = 15,
            };
            await Task.Delay(100); // Garante instâncias isoladas
            InitializeTestDependencies(options, out MetricsRegistry registry, out LoggingLevelSwitch levelSwitch, out Mock<IConnectionMultiplexer> mockRedis, out Mock<IDatabase> mockDatabase, out Mock<ILogLevelSwitcherService> mockSwitcher);

            levelSwitch.MinimumLevel = LogEventLevel.Information;
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            // Limpa o buffer de eventos antes do teste
            var bufferField = typeof(MetricsRegistry).GetField("_logEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField?.GetValue(registry) is ConcurrentQueue<(long Timestamp, LogEventLevel Level)> buffer)
            {
                while (buffer.TryDequeue(out _)) { }
            }

            // Simula timestamp antigo (30 minutos atrás, mais que MinimumHighVerbosityDurationIntMinute = 15)
            var timestampAntigo = DateTime.UtcNow.AddMinutes(-30).ToString("O", CultureInfo.InvariantCulture);
            mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                         .ReturnsAsync(new RedisValue(timestampAntigo));

            var detector = new SmartLogEconomyDetector(
                options, registry, levelSwitch, mockRedis.Object, mockSwitcher.Object, enableAutoDetection: false);

            // Adiciona erros suficientes para forçar mudança para Debug (diminuir verbosidade)
            for (int i = 0; i < 8; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }

            // ✅ Pequeno delay para garantir consistência temporal
            await Task.Delay(50);

            // Act
            await detector.RunDetectionCycleAsync();

            // Assert - Como o tempo expirou (30 min > 15 min), DEVE mudar mesmo sendo crítica
            mockSwitcher.Verify(
                s => s.SwitchLevelAsync(LogEventLevel.Debug, LogChangeType.Automatico, options.MinimumHighVerbosityDurationInMinute),
                Times.Once,
                "Deve mudar quando tempo mínimo expirou, independente da direção");

            _output.WriteLine("⏰ TEMPO EXPIRADO: Mudança permitida após aguardar tempo mínimo");
        }
    }
}

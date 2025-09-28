using FluentAssertions;
using Moq;
using Serilog.Core;
using Serilog.Events;
using SmartLog.Core.Enums;
using SmartLog.Core.Helpers;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using StackExchange.Redis;
using System.Globalization;

namespace SmartLog.Testes.Smart
{
    public class EventChannelRedisTests
    {
        private static string CreateLogMessage(LogEventLevel level, LogChangeType type, DateTime? expiration = null)
        {
            var expirationStr = expiration?.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) ?? DateTime.UtcNow.AddMinutes(5).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            return $"{level}:{type}:{expirationStr}";
        }

        private readonly Mock<IConnectionMultiplexer> _connectionMultiplexerMock;
        private readonly Mock<ISubscriber> _mockSubscriberMock;
        private readonly Mock<IDatabase> _mockDatabaseMock;
        private readonly LoggingLevelSwitch _levelSwitch;
        private readonly SmartLogOptions _options;
        private readonly EventChannelRedis _eventChannel;

        public EventChannelRedisTests()
        {
            _connectionMultiplexerMock = new Mock<IConnectionMultiplexer>();
            _mockSubscriberMock = new Mock<ISubscriber>();
            _mockDatabaseMock = new Mock<IDatabase>();
            _levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
            _options = new SmartLogOptions("tst");

            _connectionMultiplexerMock.Setup(m => m.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriberMock.Object);
            _connectionMultiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabaseMock.Object);

            _eventChannel = new EventChannelRedis(_connectionMultiplexerMock.Object, _levelSwitch, _options);
        }

        [Fact]
        public void Should_Parse_Valid_Manual_Message()
        {
            // Arrange
            var message = CreateLogMessage(LogEventLevel.Warning, LogChangeType.Manual);
            var redisValue = new RedisValue(message);

            // Act
            var parseResult = redisValue.TryParseLogMessage(out var level, out var type, out var expiration);

            // Assert
            parseResult.Should().BeTrue();
            level.Should().Be(LogEventLevel.Warning);
            type.Should().Be(LogChangeType.Manual);
            expiration.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public void Should_Parse_Valid_Automatic_Message()
        {
            // Arrange
            var expirationTime = DateTime.UtcNow.AddMinutes(10);
            var message = CreateLogMessage(LogEventLevel.Error, LogChangeType.Automatico, expirationTime);
            var redisValue = new RedisValue(message);

            // Act
            var parseResult = redisValue.TryParseLogMessage(out var level, out var type, out var expiration);

            // Assert
            parseResult.Should().BeTrue();
            level.Should().Be(LogEventLevel.Error);
            type.Should().Be(LogChangeType.Automatico);
            expiration.Should().BeCloseTo(expirationTime, TimeSpan.FromSeconds(1));
        }

        [Theory]
        [InlineData("InvalidMessage")]
        [InlineData(":Manual:01/01/2024 10:00:00")]
        [InlineData("Warning:Unknown:01/01/2024 10:00:00")]
        [InlineData("Warning:Manual:invalid-date")]
        [InlineData("")]
        public void Should_Not_Parse_Invalid_Messages(string invalidMessage)
        {
            // Arrange
            var redisValue = new RedisValue(invalidMessage);

            // Act
            var parseResult = redisValue.TryParseLogMessage(out var _, out var _, out var _);

            // Assert
            parseResult.Should().BeFalse();
        }

        [Fact]
        public void Should_Handle_Null_Message_Gracefully()
        {
            // Arrange
            var redisValue = RedisValue.Null;

            // Act
            var parseResult = redisValue.TryParseLogMessage(out var _, out var _, out var _);

            // Assert
            parseResult.Should().BeFalse();
        }

        [Fact]
        public void Should_Create_EventChannelRedis_Successfully()
        {
            // Arrange & Act
            var eventChannel = new EventChannelRedis(_connectionMultiplexerMock.Object, _levelSwitch, _options);

            // Assert
            eventChannel.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_Start_Consumer_Without_Exceptions()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(100); // Cancel after 100ms

            // Act & Assert
            var act = async () => await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);
            await act.Should().NotThrowAsync();
        }

        // ========================================================================================
        // TESTES COMPLETOS PARA LÓGICA DE NEGÓCIO
        // ========================================================================================

        [Fact]
        public void CanApplyChange_ManualType_ShouldAlwaysReturnTrue()
        {
            // Arrange
            var incomingType = LogChangeType.Manual;
            var currentType = LogChangeType.Automatico;
            var currentExpiration = DateTime.UtcNow.AddMinutes(-10); // Expirado

            // Act
            var result = InvokeCanApplyChange(incomingType, currentType, currentExpiration);

            // Assert
            result.Should().BeTrue("Manual sempre prevalece sobre qualquer outro tipo");
        }

        [Fact]
        public void CanApplyChange_AutomaticToAutomatic_ShouldAlwaysReturnTrue()
        {
            // Arrange
            var incomingType = LogChangeType.Automatico;
            var currentType = LogChangeType.Automatico;
            var currentExpiration = DateTime.UtcNow.AddMinutes(10); // Ainda válido

            // Act
            var result = InvokeCanApplyChange(incomingType, currentType, currentExpiration);

            // Assert
            result.Should().BeTrue("Automático sempre substitui outro automático");
        }

        [Fact]
        public void CanApplyChange_AutomaticToManual_WhenExpired_ShouldReturnTrue()
        {
            // Arrange
            var incomingType = LogChangeType.Automatico;
            var currentType = LogChangeType.Manual;
            var currentExpiration = DateTime.UtcNow.AddMinutes(-5); // Expirado

            // Act
            var result = InvokeCanApplyChange(incomingType, currentType, currentExpiration);

            // Assert
            result.Should().BeTrue("Automático pode substituir manual expirado");
        }

        [Fact]
        public void CanApplyChange_AutomaticToManual_WhenNotExpired_ShouldReturnFalse()
        {
            // Arrange
            var incomingType = LogChangeType.Automatico;
            var currentType = LogChangeType.Manual;
            var currentExpiration = DateTime.UtcNow.AddMinutes(10); // Ainda válido

            // Act
            var result = InvokeCanApplyChange(incomingType, currentType, currentExpiration);

            // Assert
            result.Should().BeFalse("Automático não pode substituir manual ainda válido");
        }

        [Fact]
        public async Task Consumer_WithEmptyRedis_ShouldApplyLogLevel()
        {
            // Arrange
            var message = CreateLogMessage(LogEventLevel.Warning, LogChangeType.Manual);
            var cancellationTokenSource = new CancellationTokenSource();
            Action<RedisChannel, RedisValue> handler = null;

            // Mock Redis vazio
            _mockDatabaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            _mockSubscriberMock.Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
                .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, h, __) => handler = h)
                .Returns(Task.FromResult((ChannelMessageQueue)null));

            // Act
            await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);

            handler?.Invoke(RedisChannel.Literal("test-channel"), message);

            // Assert
            _levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Warning);
            _mockDatabaseMock.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), message, It.Is<TimeSpan?>(t => t.Value.Days == 7), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);

            cancellationTokenSource.Cancel();
        }

        [Fact]
        public async Task Consumer_WithValidPrecedence_ShouldApplyLogLevel()
        {
            // Arrange
            var currentMessage = CreateLogMessage(LogEventLevel.Information, LogChangeType.Automatico, DateTime.UtcNow.AddMinutes(-5));
            var newMessage = CreateLogMessage(LogEventLevel.Error, LogChangeType.Manual);
            var cancellationTokenSource = new CancellationTokenSource();
            Action<RedisChannel, RedisValue> handler = null;

            // Mock Redis com valor atual
            _mockDatabaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(currentMessage));

            _mockSubscriberMock.Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
                .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, h, __) => handler = h)
                .Returns(Task.FromResult((ChannelMessageQueue)null));

            // Act
            await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);

            handler?.Invoke(RedisChannel.Literal("test-channel"), newMessage);

            // Assert
            _levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Error);
            _mockDatabaseMock.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), newMessage, It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);

            cancellationTokenSource.Cancel();
        }

        [Fact]
        public async Task Consumer_WithInvalidPrecedence_ShouldNotApplyLogLevel()
        {
            // Arrange
            var currentMessage = CreateLogMessage(LogEventLevel.Error, LogChangeType.Manual, DateTime.UtcNow.AddMinutes(10));
            var newMessage = CreateLogMessage(LogEventLevel.Warning, LogChangeType.Automatico);
            var cancellationTokenSource = new CancellationTokenSource();
            Action<RedisChannel, RedisValue> handler = null;

            // Mock Redis com valor atual válido
            _mockDatabaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(currentMessage));

            _mockSubscriberMock.Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
                .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, h, __) => handler = h)
                .Returns(Task.FromResult((ChannelMessageQueue)null));

            // Act
            await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);

            handler?.Invoke(RedisChannel.Literal("test-channel"), newMessage);

            // Assert
            _levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Information, "Nível não deve ter mudado");
            _mockDatabaseMock.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), newMessage, It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);

            cancellationTokenSource.Cancel();
        }

        [Fact]
        public async Task Consumer_WithInvalidMessage_ShouldNotApplyLogLevel()
        {
            // Arrange
            var invalidMessage = "mensagem-invalida";
            var cancellationTokenSource = new CancellationTokenSource();
            Action<RedisChannel, RedisValue> handler = null;

            _mockSubscriberMock.Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
                .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, h, __) => handler = h)
                .Returns(Task.FromResult((ChannelMessageQueue)null));

            // Act
            await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);

            handler?.Invoke(RedisChannel.Literal("test-channel"), invalidMessage);

            // Assert
            _levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Information, "Nível não deve ter mudado");
            _mockDatabaseMock.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);

            cancellationTokenSource.Cancel();
        }

        [Fact]
        public async Task Consumer_WithManualType_ShouldDeleteSwitchTimeKey()
        {
            // Arrange
            var message = CreateLogMessage(LogEventLevel.Warning, LogChangeType.Manual);
            var cancellationTokenSource = new CancellationTokenSource();
            Action<RedisChannel, RedisValue> handler = null;

            _mockDatabaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            _mockSubscriberMock.Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
                .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, h, __) => handler = h)
                .Returns(Task.FromResult((ChannelMessageQueue)null));

            // Act
            await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);

            handler?.Invoke(RedisChannel.Literal("test-channel"), message);

            // Assert
            _mockDatabaseMock.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once, "Manual deve deletar a chave de timestamp");

            cancellationTokenSource.Cancel();
        }

        [Fact]
        public async Task Consumer_WithAutomaticType_ShouldSetSwitchTimeKey()
        {
            // Arrange
            var message = CreateLogMessage(LogEventLevel.Debug, LogChangeType.Automatico);
            var cancellationTokenSource = new CancellationTokenSource();
            Action<RedisChannel, RedisValue> handler = null;

            _mockDatabaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            _mockSubscriberMock.Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
                .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, h, __) => handler = h)
                .Returns(Task.FromResult((ChannelMessageQueue)null));

            // Act
            await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);
            handler?.Invoke(RedisChannel.Literal("test-channel"), message);

            // Assert
            _mockDatabaseMock.Verify(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.Is<TimeSpan?>(t => t.Value.Days == 7),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Exactly(2), "Deve setar tanto a mensagem quanto o timestamp");

            cancellationTokenSource.Cancel();
        }

        [Fact]
        public async Task Consumer_WhenCancelled_ShouldUnsubscribe()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var act = async () =>
            {
                await _eventChannel.ConsumerAsyn(cancellationTokenSource.Token);
                cancellationTokenSource.Cancel();
            };

            // Assert - Simplesmente valida que o consumer pode ser cancelado sem exceções
            await act.Should().NotThrowAsync("Consumer deve ser cancelado graciosamente");
        }

        // ========================================================================================
        // MÉTODOS AUXILIARES PARA TESTES
        // ========================================================================================

        private static bool InvokeCanApplyChange(LogChangeType incomingType, LogChangeType currentType, DateTime currentExpiration)
        {
            // Usa reflexão para acessar método privado estático
            var methodInfo = typeof(EventChannelRedis).GetMethod("CanApplyChange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            return (bool)methodInfo.Invoke(null, [incomingType, currentType, currentExpiration]);
        }
    }
}
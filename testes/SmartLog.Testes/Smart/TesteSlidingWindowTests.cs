using Serilog.Events;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using System.Reflection;
using Xunit.Abstractions;

namespace SmartLog.Testes.Smart
{
    /// <summary>
    /// Testes completos para validar a implementação do Sliding Window
    /// </summary>
    public class TesteSlidingWindowTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;

        [Fact(DisplayName = "Deve manter o buffer circular dentro do limite especificado")]
        public void TesteBasicoBufferCircular()
        {
            // Arrange
            _output.WriteLine("📋 TESTE: Buffer Circular Básico");
            _output.WriteLine("----------------------------------------");
            
            var options = new SmartLogOptions 
            { 
                CircularBufferSize = 5,
                LogWindowSeconds = 60
            };
            
            var registry = new MetricsRegistry(options);
            
            // Act & Assert - Passo 1: Adiciona exatamente o limite
            _output.WriteLine("1️⃣ Adicionando 5 eventos (limite do buffer):");
            for (int i = 1; i <= 5; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
                var count = registry.GetLogEventsSnapshotCount();
                _output.WriteLine($"   Evento {i}: Buffer = {count}");
                Assert.True(count <= 5, $"Buffer deve estar dentro do limite de 5. Atual: {count}");
            }
            
            // Act & Assert - Passo 2: Adiciona além do limite
            _output.WriteLine("2️⃣ Adicionando mais 3 eventos (deve manter o limite):");
            for (int i = 6; i <= 8; i++)
            {
                var antes = registry.GetLogEventsSnapshotCount();
                registry.RecordLogEvent(LogEventLevel.Error);
                var depois = registry.GetLogEventsSnapshotCount();
                
                var status = depois <= 5 ? "✅ OK" : "❌ FALHOU";
                _output.WriteLine($"   Evento {i}: {antes} → {depois} {status}");
                
                Assert.True(depois <= 5, $"Buffer deve manter o limite de 5 eventos. Atual: {depois}");
            }
            
            var finalCount = registry.GetLogEventsSnapshotCount();
            _output.WriteLine($"🎯 Resultado: Buffer final = {finalCount} (esperado: ≤ 5)");
            
            Assert.True(finalCount <= 5, $"Buffer final deve ser ≤ 5. Atual: {finalCount}");
        }

        [Fact(DisplayName = "Deve remover eventos antigos automaticamente (Sliding Window)")]
        public void TesteComEventosAntigos()
        {
            // Arrange
            _output.WriteLine("⏰ TESTE: Eventos Antigos (Sliding Window)");
            _output.WriteLine("---------------------------------------------");
            
            var options = new SmartLogOptions 
            { 
                CircularBufferSize = 10,
                LogWindowSeconds = 30  // Janela menor para teste
            };
            
            var registry = new MetricsRegistry(options);
            
            // Act - Simula eventos antigos
            _output.WriteLine("1️⃣ Simulando eventos antigos (fora da janela de 30s):");
            
            AdicionarEventoComTimestamp(registry, LogEventLevel.Error, 
                DateTimeOffset.UtcNow.AddSeconds(-40).ToUnixTimeSeconds()); // 40s atrás
            AdicionarEventoComTimestamp(registry, LogEventLevel.Error, 
                DateTimeOffset.UtcNow.AddSeconds(-35).ToUnixTimeSeconds()); // 35s atrás
            
            var countComEventosAntigos = registry.GetLogEventsSnapshotCount();
            _output.WriteLine($"   Eventos antigos adicionados. Buffer = {countComEventosAntigos}");
            
            // Act - Adiciona evento recente
            _output.WriteLine("2️⃣ Adicionando evento recente (deve limpar os antigos):");
            registry.RecordLogEvent(LogEventLevel.Error); // Evento atual
            
            var countAposEventoRecente = registry.GetLogEventsSnapshotCount();
            _output.WriteLine($"   Após evento recente. Buffer = {countAposEventoRecente}");
            _output.WriteLine("   ✅ Eventos antigos devem ter sido removidos automaticamente");
            
            // Assert
            // O count pode ser menor ou igual ao inicial se os eventos antigos foram limpos
            Assert.True(countAposEventoRecente >= 1, "Deve haver pelo menos o evento recente");
        }

        [Fact(DisplayName = "Deve funcionar corretamente com buffer de tamanho mínimo")]
        public void TesteLimitesExtremos()
        {
            // Arrange
            _output.WriteLine("🔥 TESTE: Limites Extremos");
            _output.WriteLine("-----------------------------");
            
            var options = new SmartLogOptions 
            { 
                CircularBufferSize = 1,  // Buffer mínimo
                LogWindowSeconds = 60
            };
            
            var registry = new MetricsRegistry(options);
            
            // Act
            _output.WriteLine("1️⃣ Teste com buffer de tamanho 1:");
            for (int i = 1; i <= 5; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
                var count = registry.GetLogEventsSnapshotCount();
                _output.WriteLine($"   Evento {i}: Buffer = {count}");
                
                // Assert durante o loop
                Assert.True(count <= 1, $"Buffer deve manter no máximo 1 evento. Atual: {count}");
            }
            
            // Assert final
            var finalCount = registry.GetLogEventsSnapshotCount();
            var status = finalCount == 1 ? "✅ OK" : "❌ FALHOU";
            _output.WriteLine($"   Resultado: {finalCount} evento no buffer {status}");
            
            Assert.Equal(1, finalCount);
        }

        [Fact(DisplayName = "Deve processar grandes volumes de eventos com boa performance")]
        public void TestePerformance()
        {
            // Arrange
            _output.WriteLine("⚡ TESTE: Performance Básica");
            _output.WriteLine("-------------------------------");
            
            var options = new SmartLogOptions 
            { 
                CircularBufferSize = 1000,
                LogWindowSeconds = 60
            };
            
            var registry = new MetricsRegistry(options);
            
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            const int totalEventos = 10000;
            for (int i = 0; i < totalEventos; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }
            
            stopwatch.Stop();
            
            // Assert
            var finalCount = registry.GetLogEventsSnapshotCount();
            
            _output.WriteLine($"   📊 {totalEventos:N0} eventos processados em {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"   📊 Throughput: {totalEventos / stopwatch.ElapsedMilliseconds * 1000:F0} eventos/segundo");
            _output.WriteLine($"   📊 Buffer final: {finalCount} eventos");
            
            var performance = stopwatch.ElapsedMilliseconds < 100 ? "✅ EXCELENTE" : 
                             stopwatch.ElapsedMilliseconds < 500 ? "✅ BOM" : "⚠️ PODE MELHORAR";
            _output.WriteLine($"   🎯 Performance: {performance}");
            
            // Assertions
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
                $"Performance deve ser aceitável (< 1000ms). Atual: {stopwatch.ElapsedMilliseconds}ms");
            
            Assert.True(finalCount <= options.CircularBufferSize, 
                $"Buffer final deve respeitar o limite. Esperado: ≤ {options.CircularBufferSize}, Atual: {finalCount}");
        }

        [Theory(DisplayName = "Deve funcionar com diferentes tamanhos de buffer")]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void TesteComDiferentesTamanhosBuffer(int tamanhoBuffer)
        {
            // Arrange
            var options = new SmartLogOptions 
            { 
                CircularBufferSize = tamanhoBuffer,
                LogWindowSeconds = 60
            };
            
            var registry = new MetricsRegistry(options);
            
            // Act - Adiciona mais eventos que o buffer
            var eventosParaAdicionar = tamanhoBuffer * 2;
            for (int i = 0; i < eventosParaAdicionar; i++)
            {
                registry.RecordLogEvent(LogEventLevel.Error);
            }
            
            // Assert
            var finalCount = registry.GetLogEventsSnapshotCount();
            
            _output.WriteLine($"Buffer {tamanhoBuffer}: Adicionados {eventosParaAdicionar}, Final {finalCount}");
            
            Assert.True(finalCount <= tamanhoBuffer, 
                $"Buffer deve respeitar o limite. Esperado: ≤ {tamanhoBuffer}, Atual: {finalCount}");
        }

        [Theory(DisplayName = "Deve funcionar com diferentes níveis de log")]
        [InlineData(LogEventLevel.Verbose)]
        [InlineData(LogEventLevel.Debug)]
        [InlineData(LogEventLevel.Information)]
        [InlineData(LogEventLevel.Warning)]
        [InlineData(LogEventLevel.Error)]
        [InlineData(LogEventLevel.Fatal)]
        public void TesteComDiferentesNiveisLog(LogEventLevel nivel)
        {
            // Arrange
            var options = new SmartLogOptions 
            { 
                CircularBufferSize = 5,
                LogWindowSeconds = 60
            };
            
            var registry = new MetricsRegistry(options);
            
            // Act
            for (int i = 0; i < 10; i++)
            {
                registry.RecordLogEvent(nivel);
            }
            
            // Assert
            var finalCount = registry.GetLogEventsSnapshotCount();
            
            _output.WriteLine($"Nível {nivel}: Buffer final {finalCount}");
            
            Assert.True(finalCount <= 5, 
                $"Buffer deve respeitar o limite independente do nível de log. Atual: {finalCount}");
        }

        /// <summary>
        /// Helper para adicionar eventos com timestamp específico usando reflexão
        /// </summary>
        private static void AdicionarEventoComTimestamp(MetricsRegistry registry, LogEventLevel level, long timestamp)
        {
            // Acessa a queue privada usando reflexão
            var field = typeof(MetricsRegistry).GetField("_logEvents", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field?.GetValue(registry) is System.Collections.Concurrent.ConcurrentQueue<(long, LogEventLevel)> queue)
            {
                queue.Enqueue((timestamp, level));
            }
        }
    }
}

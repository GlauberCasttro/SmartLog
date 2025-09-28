using FluentAssertions;
using Serilog.Events;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using SmartLog.Testes.Helpers;
using System.Diagnostics;
using Xunit.Abstractions;

namespace SmartLog.Testes.Smart;

/// <summary>
/// Testes críticos para validar a correção do memory leak no MetricsRegistry
/// </summary>
public class MetricsRegistryTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Formata valores de memória em formato legível (MB/GB)
    /// </summary>
    private static string FormatMemoryForOutput(long bytes)
    {
        if (bytes >= 1_073_741_824) // 1 GB
            return $"{bytes / 1_073_741_824.0:F2} GB";
        if (bytes >= 1_048_576) // 1 MB
            return $"{bytes / 1_048_576.0:F2} MB";
        if (bytes >= 1_024) // 1 KB
            return $"{bytes / 1_024.0:F2} KB";
        return $"{bytes} bytes";
    }

    [Fact]
    public void RecordLogEvent_QuandoBufferCheio_DeveNaoBloqueiarNovosEventos()
    {
        // Arrange
        var options = new SmartLogOptions("tst")
        {
            CircularBufferSize = 5,
            LogWindowSeconds = 60
        };

        var registry = new MetricsRegistry(options);

        // Act - Preenche o buffer completamente
        for (int i = 1; i <= 5; i++)
        {
            registry.RecordLogEvent(LogEventLevel.Error);
        }

        // Verifica que buffer está cheio
        registry.GetLogEventsSnapshotCount().Should().Be(5);

        // Act - Adiciona mais eventos (AQUI ESTAVA O BUG!)
        for (int i = 6; i <= 10; i++)
        {
            registry.RecordLogEvent(LogEventLevel.Error);
        }

        // Assert - Buffer deve manter o limite e continuar funcionando
        registry.GetLogEventsSnapshotCount().Should().BeLessOrEqualTo(5, "Buffer deve manter o limite máximo e não travar");

        _output.WriteLine($"✅ Buffer final: {registry.GetLogEventsSnapshotCount()} eventos (limite: 5)");
    }

    [Fact]
    public void RecordLogEvent_SempreQueChamado_DeveAceitarNovosEventos()
    {
        // Arrange
        var options = new SmartLogOptions
        {
            CircularBufferSize = 3,  // Buffer pequeno para teste
            LogWindowSeconds = 60
        };

        var registry = new MetricsRegistry(options);

        // Act & Assert - Adiciona muitos eventos e verifica que todos são aceitos
        for (int i = 1; i <= 20; i++)
        {
            _ = registry.GetLogEventsSnapshotCount();
            registry.RecordLogEvent(LogEventLevel.Error);
            var countAfter = registry.GetLogEventsSnapshotCount();

            // O buffer pode não crescer se já está no limite, mas nunca deve recusar eventos

            // Replace the problematic line with the correct FluentAssertions method
            countAfter.Should().BeLessThanOrEqualTo(3, $"Evento {i}: Buffer não deve exceder limite");
            countAfter.Should().BeLessOrEqualTo(3, $"Evento {i}: Buffer não deve exceder limite");

            if (i <= 3)
            {
                countAfter.Should().Be(i, $"Evento {i}: Buffer deve crescer até o limite");
            }
        }

        _output.WriteLine($"✅ Processados 20 eventos. Buffer final: {registry.GetLogEventsSnapshotCount()}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    [InlineData(1000)]
    public void RecordLogEvent_ComDiferentesTamanhosBuffer_DeveManterLimite(int bufferSize)
    {
        // Arrange
        var options = new SmartLogOptions
        {
            CircularBufferSize = bufferSize,
            LogWindowSeconds = 60
        };

        var registry = new MetricsRegistry(options);

        // Act - Adiciona 3x mais eventos que o limite
        int eventsToAdd = bufferSize * 3;
        for (int i = 0; i < eventsToAdd; i++)
        {
            registry.RecordLogEvent(LogEventLevel.Error);
        }

        // Assert
        registry.GetLogEventsSnapshotCount().Should().BeLessOrEqualTo(bufferSize,
            $"Buffer de tamanho {bufferSize} não deve ser excedido");

        _output.WriteLine($"✅ Buffer {bufferSize}: {eventsToAdd} eventos → {registry.GetLogEventsSnapshotCount()} no buffer");
    }

    [Fact]
    public void GetBufferHealthStats_QuandoChamado_DeveForneceMetricasPrecisas()
    {
        // Arrange
        var options = new SmartLogOptions
        {
            CircularBufferSize = 10,
            LogWindowSeconds = 60
        };
        var registry = new MetricsRegistry(options);

        // Act - Adiciona diferentes tipos de eventos
        registry.RecordLogEvent(LogEventLevel.Error);
        registry.RecordLogEvent(LogEventLevel.Error);
        registry.RecordLogEvent(LogEventLevel.Warning);
        registry.RecordLogEvent(LogEventLevel.Warning);
        registry.RecordLogEvent(LogEventLevel.Information); // Não deve contar (< Warning)

        var stats = registry.GetBufferHealthStats();

        // Assert
        stats.TotalLogEvents.Should().Be(4, "Deve contar apenas Warning e acima");
        stats.ErrorsInWindow.Should().Be(2, "Deve contar 2 erros");
        stats.WarningsInWindow.Should().Be(2, "Deve contar 2 warnings");
        stats.BufferSizeLimit.Should().Be(10);
        stats.BufferUtilization.Should().Be(0.4, "4 eventos / 10 = 40%");
        stats.IsHealthy.Should().BeTrue("40% está abaixo do limite de 80%");
        stats.HealthStatus.Should().Be("Healthy");

        _output.WriteLine($"✅ Stats: {stats.TotalLogEvents} eventos, {stats.BufferUtilization:P} utilização");
    }

    [Fact]
    public void RecordLogEvent_ComNivelAbaixoWarning_DeveSerIgnorado()
    {
        // Arrange
        var options = new SmartLogOptions { CircularBufferSize = 10, LogWindowSeconds = 60 };
        var registry = new MetricsRegistry(options);

        // Act
        registry.RecordLogEvent(LogEventLevel.Verbose);
        registry.RecordLogEvent(LogEventLevel.Debug);
        registry.RecordLogEvent(LogEventLevel.Information);

        // Assert
        registry.GetLogEventsSnapshotCount().Should().Be(0,
            "Eventos abaixo de Warning não devem ser registrados");
    }

    [Fact]
    public void AddRequestMetric_QuandoChamado_DeveManterLimiteBuffer()
    {
        // Arrange
        var options = new SmartLogOptions { CircularBufferSize = 3, LogWindowSeconds = 60 };
        var registry = new MetricsRegistry(options);

        // Act - Adiciona mais métricas que o limite
        for (int i = 0; i < 10; i++)
        {
            registry.AddRequestMetric(latencyMs: 100 + i, isHttpError: i % 2 == 0);
        }

        // Assert
        registry.GetRequestMetricsCount().Should().BeLessOrEqualTo(3,
            "Buffer de métricas de requisição deve respeitar o limite");

        _output.WriteLine($"✅ Métricas de request: {registry.GetRequestMetricsCount()} (limite: 3)");
    }

    #region 🚀 BENCHMARK TESTS - Memory & CPU Performance

    [Fact]
    public void Benchmark_RecordLogEvent_MemoryLeakValidation()
    {
        // Arrange
        var options = new SmartLogOptions
        {
            CircularBufferSize = 1000,
            LogWindowSeconds = 60
        };
        var registry = new MetricsRegistry(options);

        // Force initial GC to get clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(true);
        _output.WriteLine($"🔍 Memória inicial: {initialMemory:N0} bytes");

        // Act - Simulate high load scenario
        const int iterations = 100_000;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            registry.RecordLogEvent(LogEventLevel.Error);

            // Simulate mixed workload
            if (i % 10 == 0) registry.RecordLogEvent(LogEventLevel.Warning);
            if (i % 100 == 0) registry.AddRequestMetric(latencyMs: 100 + i % 50, isHttpError: i % 20 == 0);
        }

        stopwatch.Stop();

        // Force GC to measure actual memory usage
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        var avgTimePerOperation = stopwatch.Elapsed.TotalMicroseconds / iterations;
        var throughput = iterations / stopwatch.Elapsed.TotalSeconds;

        // Assert - Memory should be bounded (not growing indefinitely)
        var bufferCount = registry.GetLogEventsSnapshotCount();
        var memoryLeakPrevented = bufferCount <= options.CircularBufferSize;
        var performanceGood = avgTimePerOperation < 10;
        var maxExpectedMemoryIncrease = options.CircularBufferSize * 100;
        var memoryReasonable = memoryIncrease < maxExpectedMemoryIncrease;

        bufferCount.Should().BeLessOrEqualTo(options.CircularBufferSize,
            "Buffer não deve exceder o limite configurado");
        memoryIncrease.Should().BeLessThan(maxExpectedMemoryIncrease,
            "Aumento de memória deve ser limitado pelo buffer circular");
        avgTimePerOperation.Should().BeLessThan(10,
            "Operação deve ser muito rápida (< 10 microsegundos)");

        // 📊 Report to BenchmarkReporter
        BenchmarkReporter.AddResult(new BenchmarkResult
        {
            Method = "Validação de Vazamento de Memória ao Registrar Eventos de Log",
            Operations = iterations,
            MeanTime = avgTimePerOperation,
            StdDev = 0.0, // Execução única
            MemoryUsage = memoryIncrease,
            Throughput = throughput,
            Passed = memoryLeakPrevented && performanceGood && memoryReasonable,
            Notes = $"Teste se o buffer circular limita o uso de memória e descarta eventos antigos corretamente. Buffer final: {bufferCount}/{options.CircularBufferSize}"
        });

        _output.WriteLine($"📊 BENCHMARK RESULTS:");
        _output.WriteLine($"   ⚡ {iterations:N0} operações em {stopwatch.ElapsedMilliseconds:N0}ms");
        _output.WriteLine($"   🚀 {avgTimePerOperation:F2} μs por operação");
        _output.WriteLine($"   📈 Memória aumentou: {FormatMemoryForOutput(memoryIncrease)}");
        _output.WriteLine($"   📦 Buffer final: {bufferCount:N0} eventos (limite: {options.CircularBufferSize:N0})");
        _output.WriteLine($"   🎯 Throughput: {throughput:N0} ops/sec");
        _output.WriteLine($"   ✅ Memory leak: {(memoryIncrease < maxExpectedMemoryIncrease ? "PREVENTED" : "DETECTED")}");
    }

    [Theory]
    [InlineData(100, 1_000)]      // Small buffer, low load
    [InlineData(1000, 10_000)]    // Medium buffer, medium load
    [InlineData(5000, 50_000)]    // Large buffer, high load
    public async Task Benchmark_ConcurrentAccess_PerformanceStability(int bufferSize, int operations)
    {
        // Arrange
        var options = new SmartLogOptions
        {
            CircularBufferSize = bufferSize,
            LogWindowSeconds = 120
        };
        var registry = new MetricsRegistry(options);

        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();
        var concurrentThreads = Environment.ProcessorCount;

        // Act - Concurrent load test
        for (int thread = 0; thread < concurrentThreads; thread++)
        {
            int threadId = thread;
            tasks.Add(Task.Run(() =>
            {
                var operationsPerThread = operations / concurrentThreads;

                for (int i = 0; i < operationsPerThread; i++)
                {
                    // Mix different operations
                    switch (i % 4)
                    {
                        case 0: registry.RecordLogEvent(LogEventLevel.Error); break;
                        case 1: registry.RecordLogEvent(LogEventLevel.Warning); break;
                        case 2: registry.AddRequestMetric(100 + i % 200, i % 10 == 0); break;
                        case 3: _ = registry.GetBufferHealthStats(); break; // Read operation
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert & Measure
        var finalBufferSize = registry.GetLogEventsSnapshotCount();
        var requestMetricsCount = registry.GetRequestMetricsCount();
        var throughput = operations / stopwatch.Elapsed.TotalSeconds;

        // Verify buffer limits maintained under concurrent access
        finalBufferSize.Should().BeLessOrEqualTo(bufferSize,
            "Buffer size deve ser respeitado mesmo com acesso concorrente");

        // Performance should remain reasonable
        var performancePassed = throughput > 1000;
        throughput.Should().BeGreaterThan(1000,
            "Throughput deve ser > 1000 ops/sec mesmo com concorrência");

        // 📊 Report to BenchmarkReporter
        BenchmarkReporter.AddResult(new BenchmarkResult
        {
            Method = $"Validação de Estabilidade e Performance com Acesso Concorrente (Buffer {bufferSize})",
            Operations = operations,
            MeanTime = stopwatch.Elapsed.TotalMicroseconds / operations,
            StdDev = 0.0,
            MemoryUsage = finalBufferSize + requestMetricsCount, // Aproximação
            Throughput = throughput,
            Passed = finalBufferSize <= bufferSize && performancePassed,
            Notes = $"Teste se o buffer mantém limites e alta performance mesmo com múltiplas threads simultâneas. Threads: {concurrentThreads}, Buffer final: {finalBufferSize}/{bufferSize}"
        });

        _output.WriteLine($"🏁 CONCURRENT BENCHMARK ({concurrentThreads} threads):");
        _output.WriteLine($"   📊 {operations:N0} operações em {stopwatch.ElapsedMilliseconds:N0}ms");
        _output.WriteLine($"   🚀 Throughput: {throughput:N0} ops/sec");
        _output.WriteLine($"   📦 Buffer final: {finalBufferSize:N0}/{bufferSize:N0} eventos");
        _output.WriteLine($"   📈 Request metrics: {requestMetricsCount:N0}");
        _output.WriteLine($"   ✅ Thread safety: VALIDATED");
    }

    [Fact]
    public void Benchmark_SlidingWindow_PerformanceValidation()
    {
        // Arrange - Small buffer to force frequent cleanup
        var options = new SmartLogOptions
        {
            CircularBufferSize = 100,    // Small buffer
            LogWindowSeconds = 5         // Short window
        };

        var registry = new MetricsRegistry(options);

        var stopwatch = Stopwatch.StartNew();
        var cleanupOperations = 0;
        const int totalOperations = 10_000;

        // Act - Simulate continuous operation over time
        for (int i = 0; i < totalOperations; i++)
        {
            registry.RecordLogEvent(LogEventLevel.Error);

            // Force time progression simulation
            if (i % 100 == 0)
            {
                Thread.Sleep(1); // Micro delay to trigger time-based cleanup
                cleanupOperations++;
            }
        }

        stopwatch.Stop();

        // Measure final state
        var bufferHealth = registry.GetBufferHealthStats();
        var avgTimePerOp = stopwatch.Elapsed.TotalMicroseconds / totalOperations;

        // Assert - Sliding window should maintain performance
        bufferHealth.TotalLogEvents.Should().BeLessOrEqualTo(options.CircularBufferSize);
        bufferHealth.BufferUtilization.Should().BeLessOrEqualTo(1.0);

        // Performance should remain good even with frequent cleanup
        var performancePassed = avgTimePerOp < 200;
        avgTimePerOp.Should().BeLessThan(200,
            "Sliding window cleanup não deve degradar performance significativamente");

        // 📊 Report to BenchmarkReporter
        BenchmarkReporter.AddResult(new BenchmarkResult
        {
            Method = "Validação de Performance do Cleanup por Janela Deslizante",
            Operations = totalOperations,
            MeanTime = avgTimePerOp,
            StdDev = 0.0,
            MemoryUsage = bufferHealth.TotalLogEvents,
            Throughput = totalOperations / stopwatch.Elapsed.TotalSeconds,
            Passed = performancePassed && bufferHealth.TotalLogEvents <= options.CircularBufferSize,
            Notes = $"Teste se o cleanup por janela temporal mantém o buffer eficiente e rápido mesmo sob alta carga. Limpezas: {cleanupOperations}, Utilização: {bufferHealth.BufferUtilization:P1}"
        });

        _output.WriteLine($"🔄 SLIDING WINDOW BENCHMARK:");
        _output.WriteLine($"   ⚡ {totalOperations:N0} operações em {stopwatch.ElapsedMilliseconds:N0}ms");
        _output.WriteLine($"   🚀 {avgTimePerOp:F2} μs por operação");
        _output.WriteLine($"   🧹 {cleanupOperations:N0} operações de cleanup");
        _output.WriteLine($"   📊 Utilização buffer: {bufferHealth.BufferUtilization:P1}");
        _output.WriteLine($"   📈 Eventos na janela: {bufferHealth.EventsInWindow:N0}");
        _output.WriteLine($"   ✅ Sliding window efficiency: VALIDATED");
    }

    [Fact]
    public void Benchmark_MemoryPressure_StabilityTest()
    {
        // Arrange - Test under memory pressure
        var options = new SmartLogOptions
        {
            CircularBufferSize = 10_000,  // Large buffer
            LogWindowSeconds = 300
        };
        var registry = new MetricsRegistry(options);

        // Create memory pressure
        var memoryPressure = new List<byte[]>();
        for (int i = 0; i < 100; i++)
        {
            memoryPressure.Add(new byte[1024 * 1024]); // 1MB chunks
        }

        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        // Act - Operations under memory pressure
        const int operations = 50_000;
        for (int i = 0; i < operations; i++)
        {
            registry.RecordLogEvent(LogEventLevel.Error);

            if (i % 1000 == 0)
            {
                // Force occasional GC during operations
                if (i % 5000 == 0) GC.Collect(0, GCCollectionMode.Optimized);
            }
        }

        stopwatch.Stop();

        // Clean up memory pressure
        memoryPressure.Clear();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        // Assert - Should remain stable under pressure
        var bufferCount = registry.GetLogEventsSnapshotCount();
        var memoryDelta = finalMemory - initialMemory;
        var throughput = operations / stopwatch.Elapsed.TotalSeconds;
        var stabilityPassed = stopwatch.Elapsed < TimeSpan.FromSeconds(10);

        bufferCount.Should().BeLessOrEqualTo(options.CircularBufferSize);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "Operações devem completar rapidamente mesmo sob pressão de memória");

        // 📊 Report to BenchmarkReporter
        BenchmarkReporter.AddResult(new BenchmarkResult
        {
            Method = "Estabilidade sob Pressão de Memória (Stress Test)",
            Operations = operations,
            MeanTime = stopwatch.Elapsed.TotalMicroseconds / operations,
            StdDev = 0.0,
            MemoryUsage = Math.Abs(memoryDelta),
            Throughput = throughput,
            Passed = stabilityPassed && bufferCount <= options.CircularBufferSize,
            Notes = $"Simula ambiente com pouca RAM para garantir que o buffer não cresce indefinidamente e mantém performance. Pressão: ~100MB, Buffer final: {bufferCount}/{options.CircularBufferSize}"
        });

        _output.WriteLine($"💾 MEMORY PRESSURE BENCHMARK:");
        _output.WriteLine($"   ⚡ {operations:N0} operações em {stopwatch.ElapsedMilliseconds:N0}ms");
        _output.WriteLine($"   📊 Memória inicial: {initialMemory:N0} bytes");
        _output.WriteLine($"   📈 Memória final: {finalMemory:N0} bytes");
        _output.WriteLine($"   🎯 Throughput: {throughput:N0} ops/sec");
        _output.WriteLine($"   📦 Buffer final: {bufferCount:N0}/{options.CircularBufferSize:N0}");
        _output.WriteLine($"   ✅ Stability under pressure: VALIDATED");
    }

    /// <summary>
    /// Rode .\run-benchmarks.ps1 dentro da pasta do projeto com power Shell
    /// </summary>
    [Fact]
    public void Benchmark_GenerateReport()
    {
        // Este teste deve executar por último para gerar o relatório final
        // Verifica se temos resultados de benchmarks anteriores
        BenchmarkReporter.GenerateReport();

        _output.WriteLine("📊 Relatórios de benchmark gerados com sucesso!");
        _output.WriteLine("   🌐 HTML: BenchmarkResults/BenchmarkResults.html");
        _output.WriteLine("   📋 JSON: BenchmarkResults/BenchmarkResults.json");
        _output.WriteLine("   📈 CSV: BenchmarkResults/BenchmarkResults.csv");
        _output.WriteLine("");
        _output.WriteLine("🎯 Para visualizar os resultados:");
        _output.WriteLine("   1. Abra o arquivo HTML no navegador");
        _output.WriteLine("   2. Use o CSV no Excel/Google Sheets");
        _output.WriteLine("   3. Use o JSON para análises programáticas");
    }

    #endregion 🚀 BENCHMARK TESTS - Memory & CPU Performance
}
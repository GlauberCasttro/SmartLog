using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using Microsoft.Extensions.Configuration.Json;

namespace SmartLog.Testes.Helpers
{
    /// <summary>
    /// Factory para criar instâncias de teste configuradas
    /// </summary>
    public static class TestFactory
    {
        /// <summary>
        /// Cria uma configuração básica para testes
        /// </summary>
        public static SmartLogOptions CreateTestOptions(
            int bufferSize = 100,
            int windowSeconds = 60,
            int errorThreshold = 10,
            LogEventLevel economy = LogEventLevel.Warning,
            LogEventLevel highVerbosity = LogEventLevel.Debug)
        {
            return new SmartLogOptions("prd")
            {
                CircularBufferSize = bufferSize,
                LogWindowSeconds = windowSeconds,
                AbsoluteErrorThreshold = errorThreshold,
                EconomyLevel = economy,
                HighVerbosityLevel = highVerbosity,
                DetectionInterval = TimeSpan.FromSeconds(30),
                MinimumHighVerbosityDurationInMinute = 2,
                EnableRedisChannelListener = false
            };
        }

        /// <summary>
        /// Cria um MetricsRegistry para testes
        /// </summary>
        public static MetricsRegistry CreateTestRegistry(SmartLogOptions options = null)
        {
            options ??= CreateTestOptions();
            return new MetricsRegistry(options);
        }

        /// <summary>
        /// Cria uma configuração de teste
        /// </summary>
        public static IConfiguration CreateTestConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"ConnectionStrings:Redis", "localhost:6379"},
                    {"REDIS_HOST", "localhost"},
                    {"REDIS_PORT", "6379"},
                    {"REDIS_PASSWORD", ""},
                    {"REDIS_USE_SSL", "false"}
                });

            return configurationBuilder.Build();
        }

        /// <summary>
        /// Cria um ServiceCollection configurado para testes
        /// </summary>
        public static IServiceCollection CreateTestServices()
        {
            var services = new ServiceCollection();
            var configuration = CreateTestConfiguration();

            services.AddSingleton(configuration);

            return services;
        }
    }

    /// <summary>
    /// Extensões para facilitar testes
    /// </summary>
    public static class TestExtensions
    {
        /// <summary>
        /// Adiciona múltiplos eventos de log para teste
        /// </summary>
        public static void AddTestLogEvents(this MetricsRegistry registry,
            int errorCount = 0,
            int warningCount = 0,
            int infoCount = 0)
        {
            for (int i = 0; i < errorCount; i++)
                registry.RecordLogEvent(LogEventLevel.Error);

            for (int i = 0; i < warningCount; i++)
                registry.RecordLogEvent(LogEventLevel.Warning);

            for (int i = 0; i < infoCount; i++)
                registry.RecordLogEvent(LogEventLevel.Information);
        }

        /// <summary>
        /// Adiciona múltiplas métricas de request para teste
        /// </summary>
        public static void AddTestRequestMetrics(this MetricsRegistry registry,
            int totalRequests = 10,
            double errorRate = 0.1)
        {
            for (int i = 0; i < totalRequests; i++)
            {
                bool isError = i < totalRequests * errorRate;
                int latency = isError ? 1000 + i * 100 : 100 + i * 10;

                registry.AddRequestMetric(latency, isError);
            }
        }

        /// <summary>
        /// Verifica se a configuração está válida para testes
        /// </summary>
        public static bool IsValidForTesting(this SmartLogOptions options)
        {
            try
            {
                if (!string.IsNullOrEmpty(options.AppName))
                    options.Validate();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Dados de teste para usar em Theory tests
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// Cenários de teste para diferentes configurações de buffer
        /// </summary>
        public static IEnumerable<object[]> BufferSizeScenarios =>
            [
                [1, 5, "Buffer mínimo"],
                [10, 50, "Buffer pequeno"],
                [100, 500, "Buffer médio"],
                [1000, 5000, "Buffer grande"]
            ];

        /// <summary>
        /// Cenários de teste para diferentes contagens de erro
        /// </summary>
        public static IEnumerable<object[]> ErrorCountScenarios =>
            [
                [ 0, false, "Nenhum erro" ],
                [ 5, false, "Poucos erros" ],
                [ 10, true, "Limite de erros" ],
                [ 25, true, "Muitos erros" ],
                [ 100, true, "Erros excessivos" ]
            ];

        /// <summary>
        /// Configurações de nível de log para teste
        /// </summary>
        public static IEnumerable<object[]> LogLevelConfigurations =>
            [
                [ LogEventLevel.Error, LogEventLevel.Debug, "Produção típica" ],
                [ LogEventLevel.Warning, LogEventLevel.Information, "Desenvolvimento" ],
                [ LogEventLevel.Fatal, LogEventLevel.Error, "Crítico apenas" ],
                [ LogEventLevel.Information, LogEventLevel.Verbose, "Debug intenso" ]
            ];
    }
}
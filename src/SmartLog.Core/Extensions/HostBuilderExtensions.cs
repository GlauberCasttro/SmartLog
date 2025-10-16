using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.Extensions;

/// <summary>
/// Configuração para adicionar o serilog
/// </summary>
[ExcludeFromCodeCoverage]
public static class HostBuilderExtensions
{
    /// <summary>
    /// Configurador do serilog para a ferramenta
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <param name="configureMainLogger"></param>
    /// <returns></returns>
    public static IHostBuilder UseSmartLogWithConfigurator(
        this IHostBuilder hostBuilder,
        Func<HostBuilderContext, 
            IServiceProvider, LoggerConfiguration, LoggerConfiguration> configureMainLogger)
    {
        ArgumentNullException.ThrowIfNull(configureMainLogger);

        return hostBuilder.UseSerilog((context, services, loggerConfig) =>
        {
            LoggingLevelSwitch levelSwitch = GetInitialLevelSwitch(services);

            var metricsRegistry = services.GetRequiredService<MetricsRegistry>();

            var defaultLoggerConfig = new LoggerConfiguration()
                .ReadFrom
                .Configuration(context.Configuration);

            var mainLoggerConfig = configureMainLogger(context, services, defaultLoggerConfig);

            mainLoggerConfig = ApplyLevelSwitch(mainLoggerConfig, levelSwitch);

            var mainLogger = CreateMainLogger(mainLoggerConfig);

            ConfigureGlobalLogger(loggerConfig, metricsRegistry, mainLogger);
        });
    }

    /// <summary>
    /// Altera o novel de log   inicial do levelSwitch
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    private static LoggingLevelSwitch GetInitialLevelSwitch(IServiceProvider services)
    {
        var levelSwitch = services.GetRequiredService<LoggingLevelSwitch>();
        var configLevel = services.GetRequiredService<SmartLogOptions>();
        levelSwitch.MinimumLevel = configLevel.EconomyLevel;
        return levelSwitch;
    }

    /// <summary>
    /// Aplica o controle de nível via levelSwitch
    /// </summary>
    /// <param name="loggerConfig"></param>
    /// <param name="levelSwitch"></param>
    /// <returns></returns>
    private static LoggerConfiguration ApplyLevelSwitch(LoggerConfiguration loggerConfig, LoggingLevelSwitch levelSwitch) => loggerConfig.MinimumLevel.ControlledBy(levelSwitch);

    /// <summary>
    /// Cria o logger principal a partir da configuração
    /// </summary>
    /// <param name="loggerConfig"></param>
    /// <returns></returns>
    private static Logger CreateMainLogger(LoggerConfiguration loggerConfig) => loggerConfig.CreateLogger();

    /// <summary>
    /// Configura o logger global (sink + encaminhamento para o principal)
    /// </summary>
    /// <param name="loggerConfig"></param>
    /// <param name="metricsRegistry"></param>
    /// <param name="mainLogger"></param>
    private static void ConfigureGlobalLogger(LoggerConfiguration loggerConfig, MetricsRegistry metricsRegistry, ILogger mainLogger) => loggerConfig
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new LogCountingSink(metricsRegistry))
            .WriteTo.Logger(mainLogger);
}
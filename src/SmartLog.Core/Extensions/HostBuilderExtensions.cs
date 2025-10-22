using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SmartLog.Core.Interceptors;
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
    /// Altera o nível de log inicial do levelSwitch
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
    /// Aplica o controle de nível via levelSwitch com suporte a force logging
    /// Permite que logs de QUALQUER NÍVEL com force=true sejam processados mesmo quando
    /// o nível de verbosidade estiver configurado como Error
    /// Permite TODOS os logs chegarem ao filtro
    // With(new ForceLoggingInterceptor(levelSwitch));
    // Pode configurar diferentes propriedades
    /// </summary>
    /// <param name="loggerConfig"></param>
    /// <param name="levelSwitch"></param>
    /// <returns></returns>
    private static LoggerConfiguration ApplyLevelSwitch(LoggerConfiguration loggerConfig, LoggingLevelSwitch levelSwitch, bool addInterceptorFlag = false)
    {
        if (addInterceptorFlag)
        {

            //Aumenta a quantidade de logs que chegam ao filtro, mas da a possibilidade de informar flags, em logs mais verbosos, forçando o log
            //Por exemplo se seu app tiver com a verbosidade de error/warning, mas existem alguns logs que voce não quer logar como Warning, mas que ter o registro do mesmo. 
            //Consegue configurar uma flag por exemplo force, passando como true, logando esse registro
            //Log.Information("Teste de informação com o force {Numero}, Force: {force}", 1, true);
            return loggerConfig
             .MinimumLevel.Verbose()
             .Filter.With(new ForceLoggingInterceptor(levelSwitch, ["force"]));
        }

        //Limita o controle de todo log que passa pela pipeline do serilog
        return loggerConfig.MinimumLevel.ControlledBy(levelSwitch);
    }

    /// <summary>
    /// Cria o logger principal a partir da configuração
    /// </summary>
    /// <param name="loggerConfig"></param>
    /// <returns></returns>
    private static Logger CreateMainLogger(LoggerConfiguration loggerConfig) => loggerConfig.CreateLogger();

    /// <summary>
    /// Configura o logger global (sink + encaminhamento para o principal)
    /// Logs de QUALQUER NÍVEL com force=true são processados de forma síncrona
    /// </summary>
    /// <param name="loggerConfig"></param>
    /// <param name="metricsRegistry"></param>
    /// <param name="mainLogger"></param>
    private static void ConfigureGlobalLogger(LoggerConfiguration loggerConfig, MetricsRegistry metricsRegistry, ILogger mainLogger) => loggerConfig
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new LogCountingInterceptor(metricsRegistry))
            .WriteTo.Logger(mainLogger,
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                levelSwitch: null); // Garante processamento síncrono para todos os logs forçados
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using SmartLog.Core.Interceptors;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.Extensions;

/// <summary>
/// Configura��o para adicionar o serilog - Vers�o 2 com Configura��o Fluente SEM DUPLICA��O
/// </summary>
[ExcludeFromCodeCoverage]
public static class HostBuilderExtensions
{
    /// <summary>
    /// Configurador com API fluente. Se n�o informar o Builder ir� ser constru�do os logs com seguinte formato.
    /// Considerando melhores pr�ticas de logs com render compact.
    /// Considerando Overrides da Microsoft como Warning
    /// E adicionando o contexto do serilog
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <param name="configure">Fun��o para configurar o logger de forma fluente (opcional)</param>
    /// <returns></returns>
    public static IHostBuilder UseSmartLogFluent(this IHostBuilder hostBuilder, Action<FluentLoggerBuilder> configure = null)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfig) =>
        {
            LoggingLevelSwitch levelSwitch = GetInitialLevelSwitch(services);
            var metricsRegistry = services.GetRequiredService<MetricsRegistry>();

            // Cria o builder fluente que configura diretamente o logger global
            // N�O l� configura��o do appsettings.json para evitar conflitos
            var builder = new FluentLoggerBuilder(loggerConfig, context, readFromConfiguration: false);

            bool forceLoggingEnabled = false;
            string[] forceLoggingProperties = ["force"];

            // Aplica configura��o customizada se fornecida, sen�o aplica padr�o
            if (configure != null)
            {
                configure(builder);
                forceLoggingEnabled = builder.IsForceLoggingEnabled;
                forceLoggingProperties = builder.ForceLoggingProperties;
            }
            else
            {
                // Configura��o padr�o apenas se n�o h� customiza��o
                builder
                .WithConsole(renderCompact: true)
                .WithMicrosoftOverrides()
                .WithFromContext();
            }

            //Aplica as configura��es de n�vel com suporte a force logging
            ApplyLevelSwitch(loggerConfig, levelSwitch, metricsRegistry, builder);
        });
    }

    /// <summary>
    /// Altera o n�vel de log inicial do levelSwitch
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
    /// Aplica o controle de n�vel via levelSwitch com suporte a force logging
    /// </summary>
    /// <param name="loggerConfig"></param>
    /// <param name="levelSwitch"></param>
    /// <param name="enableForceLogging"></param>
    /// <param name="forceLoggingProperties"></param>
    /// <returns></returns>
    private static LoggerConfiguration ApplyLevelSwitch(LoggerConfiguration loggerConfig, LoggingLevelSwitch levelSwitch, MetricsRegistry metricsRegistry, FluentLoggerBuilder builder)
    {
        if (builder.IsForceLoggingEnabled)
        {
            var properties = builder.ForceLoggingProperties ?? ["force"];
            return loggerConfig
                .MinimumLevel.Information()
                .Filter.With(new ForceLoggingInterceptor(levelSwitch, properties)); //Adiciona interceptor de for�ar determinados logs
        }
        return loggerConfig
            .WriteTo.Sink(new LogCountingInterceptor(metricsRegistry)) // Adiciona interceptor de contagem
            .MinimumLevel.ControlledBy(levelSwitch);
    }
}
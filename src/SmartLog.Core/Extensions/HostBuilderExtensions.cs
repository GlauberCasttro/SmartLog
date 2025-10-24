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
/// Configuração para adicionar o serilog - Versão 2 com Configuração Fluente SEM DUPLICAÇÃO
/// </summary>
[ExcludeFromCodeCoverage]
public static class HostBuilderExtensions
{
    /// <summary>
    /// Configurador com API fluente. Se não informar o Builder irá ser construído os logs com seguinte formato.
    /// Considerando melhores práticas de logs com render compact.
    /// Considerando Overrides da Microsoft como Warning
    /// E adicionando o contexto do serilog
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <param name="configure">Função para configurar o logger de forma fluente (opcional)</param>
    /// <returns></returns>
    public static IHostBuilder UseSmartLogFluent(this IHostBuilder hostBuilder, Action<FluentLoggerBuilder> configure = null)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfig) =>
        {
            LoggingLevelSwitch levelSwitch = GetInitialLevelSwitch(services);
            var metricsRegistry = services.GetRequiredService<MetricsRegistry>();

            // Cria o builder fluente que configura diretamente o logger global
            // NÃO lê configuração do appsettings.json para evitar conflitos
            var builder = new FluentLoggerBuilder(loggerConfig, context, readFromConfiguration: false);

            bool forceLoggingEnabled = false;
            string[] forceLoggingProperties = ["force"];

            // Aplica configuração customizada se fornecida, senão aplica padrão
            if (configure != null)
            {
                configure(builder);
                forceLoggingEnabled = builder.IsForceLoggingEnabled;
                forceLoggingProperties = builder.ForceLoggingProperties;
            }
            else
            {
                // Configuração padrão apenas se não há customização
                builder
                .WithConsole(renderCompact: true)
                .WithMicrosoftOverrides()
                .WithFromContext();
            }

            //Aplica as configurações de nível com suporte a force logging
            ApplyLevelSwitch(loggerConfig, levelSwitch, metricsRegistry, builder);
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
                .Filter.With(new ForceLoggingInterceptor(levelSwitch, properties)); //Adiciona interceptor de forçar determinados logs
        }
        return loggerConfig
            .WriteTo.Sink(new LogCountingInterceptor(metricsRegistry)) // Adiciona interceptor de contagem
            .MinimumLevel.ControlledBy(levelSwitch);
    }
}
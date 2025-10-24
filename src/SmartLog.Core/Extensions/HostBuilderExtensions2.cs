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
/// Configuração para adicionar o serilog - Versão 2 com Configuração Fluente SEM DUPLICAÇÃO
/// </summary>
[ExcludeFromCodeCoverage]
public static class HostBuilderExtensions2
{
    /// <summary>
    /// Configurador com API fluente - SEM duplicação de logs (RECOMENDADO)
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <param name="configure">Função para configurar o logger de forma fluente (opcional)</param>
    /// <returns></returns>
    public static IHostBuilder UseSmartLogFluent(
        this IHostBuilder hostBuilder,
        Action<FluentLoggerBuilder> configure = null)
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
                .WithConsoleWithTemplate()
                .WithMicrosoftOverrides(levelSwitch.MinimumLevel)
                .WithFromContext();
            }

            // Aplica level switch no logger global (com force logging se habilitado)
            //ApplyLevelSwitch(loggerConfig, levelSwitch, builder.IsForceLoggingEnabled, builder.ForceLoggingProperties);
            ApplyLevelSwitch(loggerConfig, levelSwitch, builder.IsForceLoggingEnabled, builder.ForceLoggingProperties);

            // Adiciona interceptor de contagem
            loggerConfig.WriteTo.Sink(new LogCountingInterceptor(metricsRegistry));
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
    private static LoggerConfiguration ApplyLevelSwitch(
        LoggerConfiguration loggerConfig,
        LoggingLevelSwitch levelSwitch,
        bool enableForceLogging = false,
        string[] forceLoggingProperties = null)
    {
        if (enableForceLogging)
        {
            var properties = forceLoggingProperties ?? ["force"];
            return loggerConfig
                .MinimumLevel.Information()
                .Filter.With(new ForceLoggingInterceptor(levelSwitch, properties));
        }

        return loggerConfig.MinimumLevel.ControlledBy(levelSwitch);
    }

    /// <summary>
    /// Builder fluente que configura diretamente o logger global - SEM DUPLICAÇÃO
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class FluentLoggerBuilder
    {
        private readonly LoggerConfiguration _globalConfig;
        private readonly HostBuilderContext _context;
        private bool _forceLoggingEnabled = false;
        private string[] _forceLoggingProperties = ["force"];

        public FluentLoggerBuilder(LoggerConfiguration globalConfig, HostBuilderContext context, bool readFromConfiguration = true)
        {
            _globalConfig = globalConfig ?? throw new ArgumentNullException(nameof(globalConfig));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // Só lê configuração do appsettings.json se solicitado
            // Para evitar duplicação, por padrão não lemos a configuração automática
            if (readFromConfiguration)
            {
                _globalConfig.ReadFrom.Configuration(context.Configuration);
            }
        }

        /// <summary>
        /// Indica se o force logging está habilitado
        /// </summary>
        internal bool IsForceLoggingEnabled => _forceLoggingEnabled;

        /// <summary>
        /// Propriedades especiais para force logging
        /// </summary>
        internal string[] ForceLoggingProperties => _forceLoggingProperties;

        /// <summary>
        /// Adiciona sink do console com template customizável
        /// </summary>
        /// <param name="template">Template de saída (opcional)</param>
        /// <returns></returns>
        public FluentLoggerBuilder WithConsoleWithTemplate(string template = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {NewLine}{Exception}")
        {
            if (template is null)
            {
                _globalConfig.WriteTo.Console();
                return this;
            }

            _globalConfig.WriteTo.Console(outputTemplate: template);
            return this;
        }

        /// <summary>
        /// Adiciona sink de arquivo com configurações padrão
        /// </summary>
        /// <param name="path">Caminho do arquivo (opcional)</param>
        /// <param name="template">Template de saída (opcional)</param>
        /// <returns></returns>
        public FluentLoggerBuilder WithFile(
            string path = "logs/app-.log",
            string template = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {NewLine}{Exception}")
        {
            _globalConfig.WriteTo.File(
                path: path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: template);
            return this;
        }

        /// <summary>
        /// Adiciona overrides para suprimir logs do framework Microsoft
        /// Pacoctes que estão ocorrendo os overrides, Microsoft, Microsoft.AspNetCore, Microsoft.AspNetCore.Hosting, Microsoft.AspNetCore.Routing, Microsoft.Hosting.Lifetime, System.Net.Http.HttpClient
        /// </summary>
        /// <param name="level">Nível de override (padrão: Warning)</param>
        /// <returns></returns>
        public FluentLoggerBuilder WithMicrosoftOverrides(LogEventLevel level = LogEventLevel.Warning)
        {
            _globalConfig
                .MinimumLevel.Override("Microsoft", level)
                .MinimumLevel.Override("Microsoft.AspNetCore", level)
                .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", level)
                .MinimumLevel.Override("Microsoft.AspNetCore.Routing", level)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", level)
                .MinimumLevel.Override("System.Net.Http.HttpClient", level);

            return this;
        }

        public FluentLoggerBuilder WithOverrides(Dictionary<string, LogEventLevel> overrides)
        {
            foreach (var (key, value) in overrides)
            {
                _globalConfig.MinimumLevel.Override(key, value);
            }

            return this;
        }

        /// <summary>
        /// Adiciona enrichment customizado
        /// </summary>
        /// <param name="propertyName">Nome da propriedade</param>
        /// <param name="value">Valor da propriedade</param>
        /// <returns></returns>
        public FluentLoggerBuilder WithProperty(string propertyName, object value)
        {
            _globalConfig.Enrich.WithProperty(propertyName, value);
            return this;
        }

        public FluentLoggerBuilder WithFromContext()
        {
            _globalConfig.Enrich.FromLogContext();
            return this;
        }

        /// <summary>
        /// Habilita suporte a force logging - logs com propriedades especiais serão sempre processados
        /// </summary>
        /// <param name="specialPropertyNames">Nomes das propriedades especiais (padrão: "force")</param>
        /// <returns></returns>
        public FluentLoggerBuilder WithForceLogging(params string[] specialPropertyNames)
        {
            _forceLoggingEnabled = true;
            _forceLoggingProperties = specialPropertyNames?.Length > 0 ? specialPropertyNames : ["force"];
            return this;
        }
    }
}
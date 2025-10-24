using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SmartLog.Core.Extensions;

/// <summary>
/// Builder fluente que configura diretamente o logger global - SEM DUPLICAÇÃO
/// </summary>
public class FluentLoggerBuilder
{
    private const string _templateDefault = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {NewLine}{Exception}";
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
            _globalConfig.ReadFrom.Configuration(_context.Configuration);
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
    /// Adiciona sink do console com template customizável. Ordem, se informar compact=true, não considerará o template informado, se informar withTemplateDefault=true, usará o template default.
    /// </summary>
    /// <param name="template">Template de saída (opcional)</param>
    /// <returns></returns>
    public FluentLoggerBuilder WithConsole(string template = null, bool renderCompact = false, bool withTemplateDefault = false)
    {
        template = withTemplateDefault ? _templateDefault : template;
        if (renderCompact)
        {
            _globalConfig.WriteTo.Console(new RenderedCompactJsonFormatter());
            return this;
        }

        if (!string.IsNullOrWhiteSpace(template))
        {
            _globalConfig.WriteTo.Console(outputTemplate: template);
            return this;
        }

        _globalConfig.WriteTo.Console();
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
        string template = _templateDefault)
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

    /// <summary>
    /// Possibilidade sobreescrever qualquer service externo (MassTransit)
    /// </summary>
    /// <param name="overrides"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Adiciona o contexto do Serilog
    /// </summary>
    /// <returns></returns>
    public FluentLoggerBuilder WithFromContext()
    {
        _globalConfig.Enrich.FromLogContext();
        return this;
    }

    /// <summary>
    /// Habilita suporte a force logging - logs com propriedades especiais serão sempre processados. Não adicionar valores com carateres especiais, pois o serilog suprimi.
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
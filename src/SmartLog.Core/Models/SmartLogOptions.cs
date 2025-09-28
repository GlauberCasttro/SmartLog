using Serilog.Events;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SmartLog.Core.Models;

[ExcludeFromCodeCoverage]
public class SmartLogOptions
{
    public SmartLogOptions(string enviroment = "dev") => GetAppNameForNamespace(enviroment);

    /// <summary>
    /// Tamanho do buffer circular para armazenamento de dados históricos (não utilizado nesta versão simplificada).
    /// </summary>
    public int CircularBufferSize { get; set; } = 1000;

    /// <summary>
    /// Intervalo entre execuções do ciclo de detecção.
    /// Ex: 60s = a cada minuto.
    /// </summary>
    public TimeSpan DetectionInterval { get; set; } = TimeSpan.FromMinutes(120);

    /// <summary>
    /// Nível de log padrão para operação normal.
    /// Ex: Warning ou Error para reduzir ruído.
    /// </summary>
    public LogEventLevel EconomyLevel { get; set; } = LogEventLevel.Warning;

    /// <summary>
    /// Nível de log detalhado para quando há erros excessivos.
    /// Ex: Information ou Debug para capturar mais contexto.
    /// </summary>
    public LogEventLevel HighVerbosityLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Quantidade absoluta de erros recentes (dentro da janela) que dispara aumento de verbosidade.
    /// Ex: 30 erros nos últimos 2 minutos.
    /// </summary>
    public int AbsoluteErrorThreshold { get; set; } = 30;

    /// <summary>
    /// Janela de tempo (em segundos) a ser considerada ao contar erros.
    /// Ex: 120s = últimos 2 minutos.
    /// </summary>
    public int LogWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Tempo mínimo que o sistema deve permanecer em modo detalhado após subir o nível de log.
    /// Evita oscilações rápidas.
    /// </summary>
    public int MinimumHighVerbosityDurationInMinute { get; set; } = 15;

    /// <summary>
    /// Habilita o canal Redis para ouvir comandos externos de alteração de nível de log.
    /// </summary>
    public bool EnableRedisChannelListener { get; set; } = true;

    /// <summary>
    /// Ambiente em que está a aplicação (dev, homol, prod, etc.)
    /// </summary>
    public string Enviroment { get; set; }

    /// <summary>
    /// Nome da aplicação, definido pelo assembly
    /// </summary>
    public string AppName => $"{Assembly.GetEntryAssembly()?.GetName().Name}-{Enviroment}".ToLower();

    /// <summary>
    /// Tempo de sincronia do worker
    /// </summary>
    public int LoadWorkerSincronizedInMinute { get; set; } = 5;

    // ============= MÉTODOS FLUENTES =============

    /// <summary>
    /// Definição do nome da aplicação para namespace de logs.
    /// </summary>
    /// <param name="enviroment"></param>
    private void GetAppNameForNamespace(string enviroment) => Enviroment = enviroment;

    /// <summary>
    /// Define os níveis de log para economia e alta verbosidade.
    /// </summary>
    public SmartLogOptions WithLogLevels(LogEventLevel economy, LogEventLevel highVerbosity)
    {
        EconomyLevel = economy;
        HighVerbosityLevel = highVerbosity;
        return this;
    }

    /// <summary>
    /// Configura os intervalos de detecção e janela de log.
    /// </summary>
    public SmartLogOptions WithTimings(TimeSpan detectionInterval, int logWindowSeconds)
    {
        DetectionInterval = detectionInterval;
        LogWindowSeconds = logWindowSeconds;
        return this;
    }

    /// <summary>
    /// Define o threshold de erros e duração mínima em modo verboso.
    /// </summary>
    public SmartLogOptions WithThresholds(int absoluteErrorThreshold, int minimumHighVerbosityDurationInMinute)
    {
        AbsoluteErrorThreshold = absoluteErrorThreshold;
        MinimumHighVerbosityDurationInMinute = minimumHighVerbosityDurationInMinute;
        return this;
    }

    /// <summary>
    /// Habilita o listener do canal Redis.
    /// </summary>
    public SmartLogOptions EnableRedis(bool enable = true)
    {
        EnableRedisChannelListener = enable;
        return this;
    }

    /// <summary>
    /// Desabilita o listener do canal Redis.
    /// </summary>
    public SmartLogOptions DisableRedis()
    {
        EnableRedisChannelListener = false;
        return this;
    }

    /// <summary>
    /// Configuração rápida para ambiente de desenvolvimento com opções personalizáveis.
    /// </summary>
    public SmartLogOptions ForDevelopment(
        LogEventLevel economy = LogEventLevel.Error,
        LogEventLevel highVerbosity = LogEventLevel.Debug,
        TimeSpan? detectionInterval = null,
        int logWindowSeconds = 60,
        int absoluteErrorThreshold = 5,
        int minimumHighVerbosityDurationInMinute = 2,
        bool enableRedis = false)
    {
        return WithLogLevels(economy, highVerbosity)
               .WithTimings(detectionInterval ?? TimeSpan.FromSeconds(30), logWindowSeconds)
               .WithThresholds(absoluteErrorThreshold, minimumHighVerbosityDurationInMinute)
               .EnableRedis(enableRedis);
    }

    /// <summary>
    /// Configuração rápida para ambiente de produção com opções personalizáveis.
    /// </summary>
    public SmartLogOptions ForProduction(
        LogEventLevel economy = LogEventLevel.Error,
        LogEventLevel highVerbosity = LogEventLevel.Information,
        TimeSpan? detectionInterval = null,
        int logWindowSeconds = 300,
        int absoluteErrorThreshold = 30,
        int minimumHighVerbosityDurationInMinute = 15,
        bool enableRedis = true)
    {
        var options = WithLogLevels(economy, highVerbosity)
               .WithTimings(detectionInterval ?? TimeSpan.FromMinutes(2), logWindowSeconds)
               .WithThresholds(absoluteErrorThreshold, minimumHighVerbosityDurationInMinute)
               .EnableRedis(enableRedis);

        Validate();

        return options;
    }

    /// <summary>
    /// Valida e lança exceção se inválido.
    /// </summary>
    public void Validate()
    {
        if (!EnableRedisChannelListener) throw new ArgumentException("O redis deve estar habilitado no ambiente produtivo para que seja possível propagar os níveis de verbosidade entre as instâncias.");
        if (DetectionInterval <= TimeSpan.Zero) throw new ArgumentException("O intervalo de detecção deve ser maior que zero.");
        if (AbsoluteErrorThreshold <= 0) throw new ArgumentException("O limite absoluto de erros deve ser maior que zero.");
        if (LogWindowSeconds <= 0) throw new ArgumentException("A janela de log deve ser maior que zero.");
        if (MinimumHighVerbosityDurationInMinute <= 0) throw new ArgumentException("A duração mínima em alta verbosidade deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(AppName)) throw new ArgumentException("O nome da aplicação é obrigatório e não pode ser nulo ou vazio.");
        if (HighVerbosityLevel > EconomyLevel) throw new ArgumentException("O nível de alta verbosidade deve ser menor que o nível de economia.");
        ArgumentException.ThrowIfNullOrEmpty(AppName, "O nome da aplicação (AppName) é obrigatório e não foi configurado.");
    }
}
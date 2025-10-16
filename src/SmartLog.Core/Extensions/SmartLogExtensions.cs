using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using SmartLog.Core.CustomMiddleware;
using SmartLog.Core.Helpers;
using SmartLog.Core.Interfaces;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using SmartLog.Core.Worker;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;
using static System.Console;

namespace SmartLog.Core.Extensions;

/// <summary>
/// Métodos de extensão para configurar e registrar a SDK de Logging Inteligente no pipeline da aplicação.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SmartLogExtensions
{
    /// <summary>
    /// **Passo 1: Registra os serviços da SDK.**
    /// Prepara o terreno registrando o detector, o level switch, o registry de métricas e o Redis
    /// no container de injeção de dependência do .NET.
    /// </summary>
    public static IServiceCollection AddSmartLogEconomy(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<SmartLogOptions> actionConfig = null)
    {
        // Configura e registra as opções da SDK
        SmartLogOptions options = GetSmartOptions(configuration, actionConfig);

        services.AddSingleton(options);

        // Registra componentes principais
        RegisterCoreServices(services, options);

        // Configura Redis
        AddRedis(services);

        // Inicia listener Redis se habilitado
        StartRedisChannelListenerIfEnabled(services, options);

        AddBackGroundServices(services, options);

        ConfigureProblemDetaisResul(services);

        return services;
    }

    private static void ConfigureProblemDetaisResul(IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(x => x.InvalidModelStateResponseFactory = ctx => new ValidationProblemDetailsResult());
    }

    /// <summary>
    /// Adiciona o concumer do channel
    /// Adiciona o Worker Sync para quando uma POD nova nascer...
    /// </summary>
    /// <param name="services"></param>
    private static void AddBackGroundServices(IServiceCollection services, SmartLogOptions options)
    {
        if (options.EnableRedisChannelListener)
        {
            services.AddHostedService<LogLevelRedisSyncWorker>();
            services.AddHostedService<RedisChannelConsumer>();
        }
    }

    /// <summary>
    /// Obtem os dados de configuração da SDK de Logging Inteligente do delagate ou da seção.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="actionConfig"></param>
    /// <returns></returns>
    private static SmartLogOptions GetSmartOptions(IConfiguration configuration, Action<SmartLogOptions> actionConfig)
    {
        try
        {
            // Obtém o assembly principal da aplicação
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? throw new ArgumentNullException(nameof(configuration), "ASPNETCORE_ENVIRONMENT não definido");

            var options = new SmartLogOptions(environment);
            if (actionConfig != null)
            {
                actionConfig(options);
                return options;
            }

            if (environment.Equals("prd"))
                configuration.GetSection("smartLogEconomy-prd").Bind(options);
            else
                configuration.GetSection("smartLogEconomy-dev").Bind(options);

            options.Validate();

            return options;
        }
        catch (Exception ex)
        {
            WriteLine($"[ERROR]: Falha ao carregar configuração da SDK não será possível habilitar em {Environment.MachineName}: Erro: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// **Passo 3: Adiciona o middleware.**
    /// </summary>
    public static IApplicationBuilder UseMiddlewareSmartLogEconomy(this IApplicationBuilder app) => app.UseMiddleware<SmartLogMiddleware>();

    /// <summary>
    /// Registra as dependencias dos serviços principais da SDK.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    private static void RegisterCoreServices(IServiceCollection services, SmartLogOptions options)
    {
        var levelSwitch = new LoggingLevelSwitch(options.EconomyLevel);
        var metricsRegistry = new MetricsRegistry(options);

        services.AddSingleton(levelSwitch);
        services.AddSingleton(metricsRegistry);
        services.AddSingleton<ILogLevelSwitcherService, MediatorLogLevelSwitcher>();
        services.AddSingleton<ISmartLogEconomyDetector, SmartLogEconomyDetector>();
    }

    /// <summary>
    /// Adiciona e configura a conexão com o Redis.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void AddRedis(IServiceCollection services)
    {
        try
        {
            var redisConnectionString = GlobalConfig.BuildRedisConnectionStringFromEnvironment();
            if (string.IsNullOrEmpty(redisConnectionString)) throw new InvalidOperationException("A string de conexão 'ConnectionStrings:Redis' é obrigatória.");

            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
        }
        catch (Exception ex)
        {
            WriteLine($"[ERROR]: Erro ao conectar no redis... {ex.Message}");
            throw new InvalidOperationException("Falha ao configurar a conexão com o Redis...", ex);
        }
    }

    /// <summary>
    /// Constrói provedor temporário para inicializar o canal
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    private static void StartRedisChannelListenerIfEnabled(IServiceCollection services, SmartLogOptions options)
    {
        if (options.EnableRedisChannelListener) services.AddSingleton<IEventChannelRedis, EventChannelRedis>();
    }
}
using Serilog;
using SmartLog.Core.Extensions;
using static SmartLog.Core.Extensions.SmartLogExtensions;

// ============================================================================
// EXEMPLO 1: CONFIGURAÇÃO BÁSICA (MAIS SIMPLES)
// ============================================================================

Log.Information("Iniciando a API...");

//try
//{
//    var builder = WebApplication.CreateBuilder(args);

//    // Serviços básicos
//    builder.Services.AddControllers();
//    builder.Services.AddEndpointsApiExplorer();
//    builder.Services.AddSwaggerGen();

//    // PASSO 1: Registra a SDK
//    builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
//    {
//        config.EconomyLevel = LogEventLevel.Error;
//        config.HighVerbosityLevel = LogEventLevel.Information;
//        config.MinimumHighVerbosityDurationIntMinute = 2;
//        config.DetectionInterval = TimeSpan.FromSeconds(60);
//        config.EnableRedisChannelListener = true;
//        config.AppName = "MinhaAPI";
//    });

//    // PASSO 2: Configuração padrão do Serilog
//    builder.Host.UseSmartLogEconomy();

//    var app = builder.Build();

//    // PASSO 3: Middleware
//    app.UseSmartLogEconomy();

//    if (app.Environment.IsDevelopment())
//    {
//        app.UseSwagger();
//        app.UseSwaggerUI();
//    }

//    app.MapControllers();
//    app.Run();
//}
//catch (Exception ex)
//{
//    Log.Fatal(ex, "A aplicação falhou ao iniciar.");
//}
//finally
//{
//    Log.CloseAndFlush();
//}

//// ============================================================================
//// EXEMPLO 2: CONFIGURAÇÃO CUSTOMIZADA (MAIS FLEXIBILIDADE)
//// ============================================================================

//Log.Information("Iniciando a API com configuração customizada...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serviços básicos
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    //PASSO 1: Registra a SDK
    //builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    //{
    //    config.CircularBufferSize = 1000;
    //    config.EconomyLevel = LogEventLevel.Error;
    //    config.HighVerbosityLevel = LogEventLevel.Debug;
    //    config.MinimumHighVerbosityDurationInMinute = 5;
    //    config.DetectionInterval = TimeSpan.FromSeconds(30);
    //    config.EnableRedisChannelListener = true; // habilitando neste exemplo
    //    config.AbsoluteErrorThreshold = 10;
    //    config.LogWindowSeconds = 120;
    //    config.LoadWorkerSincronizedInMinute = 5;
    //});

    //"CircularBufferSize": 1000,
    //"EconomyLevel": "Error",
    //"HighVerbosityLevel": "Information",
    //"DetectionInterval": "00:00:30",
    //"MinimumHighVerbosityDurationInMinute": 1,
    //"AbsoluteErrorThreshold": 15,
    //"LogWindowSeconds": 60,
    //"EnableRedisChannelListener": true,
    //"LoadWorkerSincronizedInMinute": 5

    builder.Services.AddSmartLogEconomy(builder.Configuration);

    #region Exemplos de uso

    // Configuração básica
    //builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    //    config.WithLogLevels(LogEventLevel.Error, LogEventLevel.Debug)
    //          .WithTimings(TimeSpan.FromSeconds(30), 300)
    //          .WithThresholds(30, 5)
    //          .ForApp("centralmonitoramento")
    //          .EnableRedis()
    //);

    //// Usando preset de produção totalmente customizado
    //builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    //    config.ForProduction(
    //        economy: LogEventLevel.Warning,
    //        highVerbosity: LogEventLevel.Debug,
    //        detectionInterval: TimeSpan.FromMinutes(5),
    //        logWindowSeconds: 600,
    //        absoluteErrorThreshold: 50,
    //        minimumHighVerbosityDurationInMinute: 20,
    //        enableRedis: true
    //    ).ForApp("centralmonitoramento")
    //);

    //// Usando preset de desenvolvimento customizado
    //builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    //    config.ForDevelopment(
    //        detectionInterval: TimeSpan.FromSeconds(15),
    //        logWindowSeconds: 120,
    //        enableRedis: true
    //    ).ForApp("centralmonitoramento-dev")
    //);

    //// Misturando preset com customizações
    //builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    //    config.ForProduction()
    //          .WithTimings(TimeSpan.FromSeconds(45), 400)  // Override dos timings              // Override do buffer
    //          .ForApp("centralmonitoramento")
    //          .EnableRedis()
    //);

    #endregion Exemplos de uso

    // PASSO 2: Configuração customizada do Serilog

    //    builder.Host.UseSmartLogWithConfigurator((context, services, mainLoggerConfig) =>
    //    {
    //        // Configuração específica para esta aplicação
    //        return mainLoggerConfig

    //             .Enrich.WithProperty("dd_env", Environment.GetEnvironmentVariable("AMBIENTE"))
    //             .Enrich.WithProperty("dd_trace_id", CorrelationIdentifier.TraceId.ToString())
    //             .Enrich.WithProperty("dd_service", CorrelationIdentifier.Service)
    //             .Enrich.WithProperty("dd_version", CorrelationIdentifier.Version)
    //             .Enrich.WithProperty("dd_span_id", CorrelationIdentifier.SpanId.ToString())

    //#if DEBUG
    //                 .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}")
    //#else
    //                .WriteTo.Console(new RenderedCompactJsonFormatter())
    //#endif

    //            .Enrich.WithProperty("squad", "seguranca de frota");
    //    });

    // PASSO 2: Configuração customizada do Serilog
    builder.Host.UseSmartLogWithConfigurator((context, services, mainLoggerConfig) =>
    {
        // Configuração específica para esta aplicação
        return mainLoggerConfig
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/app-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {NewLine}{Exception}")
            .Enrich.FromLogContext()
            .Enrich.WithProperty("AppName", "APICustomizada");
    });

    var app = builder.Build();

    // PASSO 3: Middleware
    app.UseMiddlewareSmartLogEconomy();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush();
}

// ============================================================================
// EXEMPLO 3: CONFIGURAÇÃO AVANÇADA COM DIFERENTES AMBIENTES
// ============================================================================
/*
Log.Information("Iniciando a API com configuração por ambiente...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serviços básicos
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // PASSO 1: Registra a SDK
    builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    {
        // Configuração baseada no ambiente
        if (builder.Environment.IsDevelopment())
        {
            config.EconomyLevel = LogEventLevel.Information;
            config.HighVerbosityLevel = LogEventLevel.Debug;
            config.EnableRedisChannelListener = false;
        }
        else
        {
            config.EconomyLevel = LogEventLevel.Warning;
            config.HighVerbosityLevel = LogEventLevel.Information;
            config.EnableRedisChannelListener = true;
        }

        config.MinimumHighVerbosityDurationIntMinute = 3;
        config.DetectionInterval = TimeSpan.FromMinutes(1);
        config.AppName = builder.Configuration["AppName"] ?? "MinhaAPI";
    });

    // PASSO 2: Configuração por ambiente
    builder.Host.UseSmartLogEconomy((context, services, mainLoggerConfig) =>
    {
        var config = mainLoggerConfig;

        if (context.HostingEnvironment.IsDevelopment())
        {
            // Desenvolvimento: Console colorido e detalhado
            config = config
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {NewLine}{Exception}")
                .WriteTo.Debug();
        }
        else if (context.HostingEnvironment.IsStaging())
        {
            // Staging: Console + arquivo + sistema externo
            config = config
                .WriteTo.Console()
                .WriteTo.File("logs/staging-.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Seq(context.Configuration.GetConnectionString("Seq"));
        }
        else // Production
        {
            // Produção: Arquivo estruturado + sistemas de monitoramento
            config = config
                .WriteTo.File(
                    path: "logs/production-.json",
                    formatter: new Serilog.Formatting.Json.JsonFormatter(),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .WriteTo.Seq(context.Configuration.GetConnectionString("Seq"))
                .WriteTo.ApplicationInsights(context.Configuration.GetConnectionString("ApplicationInsights"));
        }

        return config
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .Enrich.WithProperty("AppName", context.Configuration["AppName"])
            .Enrich.WithMachineName()
            .Enrich.WithThreadId();
    });

    var app = builder.Build();

    // PASSO 3: Middleware
    app.UseSmartLogEconomy();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush();
}

// ============================================================================
// EXEMPLO ALTERNATIVO: USANDO WebApplicationBuilder (SEM PACOTE HOSTING)
// ============================================================================
/*
Log.Information("Iniciando a API com WebApplicationBuilder...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serviços básicos
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // PASSO 1: Registra a SDK
    builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    {
        config.EconomyLevel = LogEventLevel.Error;
        config.HighVerbosityLevel = LogEventLevel.Information;
        config.MinimumHighVerbosityDurationIntMinute = 2;
        config.DetectionInterval = TimeSpan.FromSeconds(60);
        config.EnableRedisChannelListener = true;
        config.AppName = "MinhaAPI";
    });

    // PASSO 2: Configuração usando WebApplicationBuilder
    builder.ConfigureSmartLogEconomy((configuration, services, mainLoggerConfig) =>
    {
        return mainLoggerConfig
            .WriteTo.Console()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day);
    });

    var app = builder.Build();

    // PASSO 3: Middleware
    app.UseSmartLogEconomy();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush();
}

*/

//Regras novas:
//criar regra para que seja possivel alterar o nivel de verbosidade, em algum dia da semana especifico, ou seja, em um horario especifico
using Serilog;
using Serilog.Events;
using SmartLog.Core.Extensions;
using static SmartLog.Core.Extensions.SmartLogExtensions;

// ============================================================================
// CONFIGURAÇÃO CORRIGIDA - SEM DUPLICAÇÃO COM FORCE LOGGING!
// ============================================================================

Log.Information("Iniciando a API...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serviços básicos
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // PASSO 1: Registra a SDK
    builder.Services.AddSmartLogEconomy(builder.Configuration);

    // PASSO 2: CONFIGURAÇÃO CORRIGIDA - SEM DUPLICAÇÃO COM FORCE LOGGING!
    // Usando a nova API que NÃO causa duplicação de logs e suporta ForceLoggingInterceptor
    builder.Host.UseSmartLogFluent(config =>
        config
        .WithConsole(renderCompact: false)
              //.WithFile()
              .WithMicrosoftOverrides(level: LogEventLevel.Warning)
              .WithProperty("env", "dev")
              .WithProperty("Squad", "Segurança de frota")
              .WithProperty("Produto", "Central")
              .WithProperty("AppName", "Central de alertas")
              .WithFromContext()
              .WithForceLogging("forcehere")); // 🎯 SE A VERBOSIDADE ATUAL FOR ERROR -> Força logs INFO que tiver a propriedade "force-here" = true

    //builder.Host.UseSmartLogFluent();

    var app = builder.Build();

    // PASSO 3: Middleware SmartLog primeiro
    app.UseMiddlewareSmartLogEconomy();

    // Depois o Serilog request logging (se necessário)
    // app.UseSerilogRequestLogging();

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
// EXEMPLOS DE USO DO FORCE LOGGING
// ============================================================================

/*
// EXEMPLO: Configuração com múltiplas propriedades de force
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole()
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("SmartLogApp")
          .WithForceLogging("force", "urgent", "critical")); // Múltiplas propriedades

// EXEMPLO: Configuração para produção com force logging
builder.Host.UseSmartLogFluent(config =>
    config.ForProduction()
          .WithForceLogging("force"));

// EXEMPLO: No Controller, logs que sempre aparecem mesmo com EconomyLevel=Error
Log.Information("Log normal - pode não aparecer se EconomyLevel for Error");
Log.Information("Log forçado - SEMPRE aparece {Message}, Force: {force}", "Importante!", true);
Log.Information("Log com urgência {Message}, Urgent: {urgent}", "Super importante!", true);
*/
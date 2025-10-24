# ?? SmartLog - Arquitetura e Guia Completo

## ?? �ndice
1. [Problema Original e Solu��o](#problema-original-e-solu��o)
2. [Arquitetura Atual](#arquitetura-atual)
3. [Componentes Principais](#componentes-principais)
4. [Guia de Uso](#guia-de-uso)
5. [Configura��es Avan�adas](#configura��es-avan�adas)
6. [Troubleshooting](#troubleshooting)

---

## ?? Problema Original e Solu��o

### ? **Problema: Duplica��o de Logs**

**Sintoma:**
```
[19:56:11 INF] Teste de informa��o sem o force: 1
[19:56:11 INF] Teste de informa��o sem o force: 1  ? DUPLICADO!
[19:56:11 INF] Teste com force
[19:56:11 INF] Teste com force  ? DUPLICADO!
```

**Causa Raiz:**
A implementa��o original criava dois loggers escrevendo para o mesmo destino.

```csharp
// ? ARQUITETURA PROBLEM�TICA (ANTIGA)
var mainLogger = CreateMainLogger(mainLoggerConfig); // ? Console Sink 1
loggerConfig.WriteTo.Logger(mainLogger);             // ? Console Sink 2 (encaminha para mainLogger)

// Resultado: Cada log aparece 2x!
```

**Fluxo Problem�tico:**
```
Log.Information("teste")
    ?
Logger Global ? WriteTo.Logger(mainLogger)
    ?                    ?
Console Sink 1      mainLogger ? Console Sink 2
    ?                    ?
"Log duplicado!"    "Log duplicado!"
```

### ? **Solu��o Implementada**

**Arquitetura Corrigida:**
```csharp
// ? NOVA ARQUITETURA (CORRETA)
public static IHostBuilder UseSmartLogFluent(
    this IHostBuilder hostBuilder,
    Action<FluentLoggerBuilder> configure = null)
{
    return hostBuilder.UseSerilog((context, services, loggerConfig) =>
    {
        // 1. Configura��o DIRETA no logger global (sem logger secund�rio)
        var builder = new FluentLoggerBuilder(loggerConfig, context, false);
        
        // 2. Aplicar configura��es via builder fluente
        if (configure != null)
            configure(builder);
        else
            builder.WithConsole(renderCompact: true)
                   .WithMicrosoftOverrides()
                   .WithFromContext();

        // 3. Aplicar controles din�micos (m�tricas + force logging)
        ApplyLevelSwitch(loggerConfig, levelSwitch, metricsRegistry, builder);
    });
}
```

**Fluxo Correto:**
```
Log.Information("teste")
    ?
Logger Global (configurado diretamente)
    ? [Sub-logger isolado para m�tricas]
    ? [ForceLoggingInterceptor se habilitado]
    ? [LoggingLevelSwitch din�mico]
    ?
Console Sink (�NICO)
    ?
"Log �nico! ?"
```

---

## ??? Arquitetura Atual

### **Vis�o Geral**

```
???????????????????????????????????????????????????????????????
?                    SmartLog Architecture                     ?
???????????????????????????????????????????????????????????????
?                                                              ?
?  Log.Information("teste")                                   ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?    Logger Global (Serilog)           ?                  ?
?  ?  (configurado via FluentLoggerBuilder)?                 ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?  1. Sub-logger ISOLADO (m�tricas)    ?                  ?
?  ?     - MinimumLevel.Verbose()         ?                  ?
?  ?     - LogCountingInterceptor         ?                  ?
?  ?     - Captura TODOS os eventos       ?                  ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?  2. ForceLoggingInterceptor          ?                  ?
?  ?     (se habilitado)                  ?                  ?
?  ?     - Verifica propriedades especiais ?                 ?
?  ?     - Permite logs for�ados          ?                  ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?  3. LoggingLevelSwitch (din�mico)    ?                  ?
?  ?     - Controlado por SmartLogOptions ?                 ?
?  ?     - Muda em runtime                ?                  ?
?  ?     - Sincroniza��o Redis            ?                  ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?  4. Sinks Principais                 ?                  ?
?  ?     - Console (renderCompact ou n�o) ?                 ?
?  ?     - File (se configurado)          ?                  ?
?  ?     - Outros sinks customizados      ?                  ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?     Output Final                                            ?
?                                                              ?
???????????????????????????????????????????????????????????????
```

---

## ?? Componentes Principais

### **1. GetInitialLevelSwitch - Controle Din�mico**

```csharp
private static LoggingLevelSwitch GetInitialLevelSwitch(IServiceProvider services)
{
    var levelSwitch = services.GetRequiredService<LoggingLevelSwitch>();
    var configLevel = services.GetRequiredService<SmartLogOptions>();
    levelSwitch.MinimumLevel = configLevel.EconomyLevel; // ? Din�mico!
    return levelSwitch;
}
```

**Caracter�sticas:**
- ? **Controle din�mico** baseado em `SmartLogOptions.EconomyLevel`
- ? **Mudan�a em runtime** quando SmartLog detecta problemas
- ? **Sincroniza��o Redis** entre m�ltiplas inst�ncias/pods
- ? **Adaptativo** - aumenta verbosidade em situa��es cr�ticas

**Exemplo de comportamento:**
```csharp
// Situa��o Normal:
EconomyLevel = Error  // Logs: Error, Fatal

// SmartLog detecta 15 erros em 60s:
EconomyLevel = Information  // Logs: Information, Warning, Error, Fatal (AUTOM�TICO!)

// Situa��o normaliza ap�s 5 minutos:
EconomyLevel = Error  // Volta ao normal (AUTOM�TICO!)
```

---

### **2. ApplyLevelSwitch - Pipeline Completo**

```csharp
private static LoggerConfiguration ApplyLevelSwitch(
    LoggerConfiguration loggerConfig,
    LoggingLevelSwitch levelSwitch,
    MetricsRegistry metricsRegistry,
    FluentLoggerBuilder builder)
{
    // 1. Sub-logger ISOLADO para m�tricas
    var config = loggerConfig
        .WriteTo.Logger(metricsLogger => metricsLogger
            .MinimumLevel.Verbose()  // Captura TUDO
            .WriteTo.Sink(new LogCountingInterceptor(metricsRegistry)));

    // 2. Force logging filter (se habilitado)
    if (builder.IsForceLoggingEnabled)
        config = config.Filter.With(new ForceLoggingInterceptor(levelSwitch, properties));

    // 3. Controle din�mico global
    return config.MinimumLevel.ControlledBy(levelSwitch);
}
```

**Pipeline de Processamento:**

| Passo | Componente | Descri��o | N�vel |
|-------|-----------|-----------|-------|
| **1** | Sub-logger M�tricas | Captura TODOS os eventos para an�lise | `Verbose` |
| **2** | ForceLoggingInterceptor | Permite logs com `force: true` | Condicional |
| **3** | LoggingLevelSwitch | Controle din�mico do n�vel | `EconomyLevel` |
| **4** | Sinks Principais | Console, File, etc. | Filtrado |

---

### **3. ForceLoggingInterceptor - Logs For�ados**

```csharp
public class ForceLoggingInterceptor : ILogEventFilter
{
    public bool IsEnabled(LogEvent logEvent)
    {
        // Se tem propriedade especial = true, permite SEMPRE
        if (HasForceProperty(logEvent)) return true;
        
        // Caso contr�rio, respeita o levelSwitch
        return logEvent.Level >= _levelSwitch.MinimumLevel;
    }
    
    private bool HasForceProperty(LogEvent logEvent) => 
        _specialPropertyNames.Any(prop =>
            logEvent.Properties.TryGetValue(prop, out var value)
            && value is ScalarValue { Value: true });
}
```

**Como Funciona:**

```csharp
// EconomyLevel = Error (s� mostra Error/Fatal)

// ? Bloqueado (n�vel Information < Error)
Log.Information("Log normal");

// ? Permitido (force: true ignora n�vel)
Log.Information("Log for�ado {Data}, Force: {force}", data, true);

// ? Permitido (n�vel Error >= Error)
Log.Error("Log de erro");
```

---

### **4. FluentLoggerBuilder - API Fluente**

```csharp
public class FluentLoggerBuilder
{
    public FluentLoggerBuilder WithConsole(
        string template = null, 
        bool renderCompact = false, 
        bool withTemplateDefault = false);
    
    public FluentLoggerBuilder WithFile(
        string path = "logs/app-.log",
        string template = _templateDefault);
    
    public FluentLoggerBuilder WithMicrosoftOverrides(
        LogEventLevel level = LogEventLevel.Warning);
    
    public FluentLoggerBuilder WithFromContext();
    
    public FluentLoggerBuilder WithForceLogging(
        params string[] specialPropertyNames);
}
```

---

## ?? Guia de Uso

### **1. Configura��o B�sica (Padr�o)**

```csharp
// Program.cs
builder.Host.UseSmartLogFluent();

// Resultado:
// ? Console com renderCompact
// ? Overrides Microsoft = Warning
// ? Contexto Serilog habilitado
// ? Controle din�mico ativo
```

### **2. Configura��o Customizada**

```csharp
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(renderCompact: true)
          .WithFile("logs/app-.log")
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithFromContext()
          .WithForceLogging("force", "urgent", "critical"));
```

### **3. Configura��o por Ambiente**

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Host.UseSmartLogFluent(config =>
        config.WithConsole(renderCompact: false) // Texto leg�vel
              .WithMicrosoftOverrides(LogEventLevel.Information)
              .WithFromContext());
}
else // Production
{
    builder.Host.UseSmartLogFluent(config =>
        config.WithConsole(renderCompact: true) // JSON compacto
              .WithFile("logs/production-.log")
              .WithMicrosoftOverrides(LogEventLevel.Warning)
              .WithFromContext()
              .WithForceLogging("force"));
}
```

### **4. Templates Customizados**

```csharp
// Template customizado
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(
        template: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message}",
        renderCompact: false));

// Template padr�o
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(withTemplateDefault: true));

// Render Compact (JSON)
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(renderCompact: true));
```

---

## ?? Configura��es Avan�adas

### **1. SmartLogOptions - Configura��o Din�mica**

```json
// appsettings.json
{
  "smartLog-dev": {
    "CircularBufferSize": 1000,
    "EconomyLevel": "Error",           // ? N�vel normal
    "HighVerbosityLevel": "Information", // ? N�vel em crise
    "DetectionInterval": "00:00:10",   // ? Verifica a cada 10s
    "MinimumHighVerbosityDurationInMinute": 5, // ? M�nimo 5min em alta verbosidade
    "AbsoluteErrorThreshold": 15,      // ? 15 erros disparam mudan�a
    "LogWindowSeconds": 60,            // ? Janela de 60s
    "EnableRedisChannelListener": true // ? Sincroniza via Redis
  }
}
```

**Como o SmartLog Funciona:**

```
???????????????????????????????????????????
?   SmartLog Adaptive Logging System      ?
???????????????????????????????????????????
?                                          ?
?  1. Monitoramento Cont�nuo              ?
?     - A cada 10s verifica m�tricas      ?
?     - Conta erros nos �ltimos 60s       ?
?                                          ?
?  2. Detec��o de Problema                ?
?     - Se erros >= 15 em 60s:            ?
?       ? EconomyLevel = Information      ?
?       ? Registra timestamp da mudan�a   ?
?                                          ?
?  3. Per�odo M�nimo                      ?
?     - Permanece em Information por 5min ?
?     - Evita oscila��es                  ?
?                                          ?
?  4. Normaliza��o                        ?
?     - Ap�s 5min, verifica novamente     ?
?     - Se situa��o normalizou:           ?
?       ? EconomyLevel = Error            ?
?                                          ?
?  5. Sincroniza��o (Redis)               ?
?     - Todas inst�ncias sincronizam      ?
?     - Mudan�a em um pod afeta todos     ?
?                                          ?
???????????????????????????????????????????
```

### **2. Force Logging - Uso Avan�ado**

```csharp
// Configura��o
builder.Host.UseSmartLogFluent(config =>
    config.WithForceLogging("force", "urgent", "critical", "debug"));

// No c�digo
public class OrderService
{
    public void ProcessOrder(Order order)
    {
        // Log normal - pode ser bloqueado
        Log.Information("Processando pedido {OrderId}", order.Id);
        
        // Log for�ado - SEMPRE aparece
        if (order.Total > 10000)
        {
            Log.Warning(
                "Pedido de alto valor {OrderId}: {Total}, Urgent: {urgent}", 
                order.Id, 
                order.Total, 
                true); // ? SEMPRE registrado!
        }
        
        // Debug for�ado em situa��o espec�fica
        if (needsInvestigation)
        {
            Log.Debug(
                "Estado do pedido {OrderId}: {@OrderState}, Debug: {debug}",
                order.Id,
                order.GetState(),
                true); // ? Mesmo com EconomyLevel=Error!
        }
    }
}
```

### **3. Overrides Customizados**

```csharp
builder.Host.UseSmartLogFluent(config =>
    config.WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithOverrides(new Dictionary<string, LogEventLevel>
          {
              { "MassTransit", LogEventLevel.Error },
              { "EntityFrameworkCore", LogEventLevel.Warning },
              { "Npgsql", LogEventLevel.Error },
              { "Hangfire", LogEventLevel.Information }
          }));
```

---

## ?? Troubleshooting

### **Problema: Logs Duplicados**

**Sintoma:**
```
[19:56:11 INF] Teste
[19:56:11 INF] Teste  ? Duplicado!
```

**Solu��o:**
? Certifique-se de usar `UseSmartLogFluent` (nova implementa��o)
? **N�O** use `UseSmartLogWithConfigurator` (implementa��o antiga)

```csharp
// ? CORRETO
builder.Host.UseSmartLogFluent(config => ...);

// ? PROBLEM�TICO (antiga implementa��o)
builder.Host.UseSmartLogWithConfigurator((context, services, mainLoggerConfig) => ...);
```

---

### **Problema: ForceLogging N�o Funciona**

**Sintoma:**
```csharp
Log.Information("Teste {Message}, Force: {force}", "teste", true);
// ? N�o aparece mesmo com force: true
```

**Solu��o:**
Habilite o ForceLoggingInterceptor:

```csharp
// ? CORRETO
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole()
          .WithForceLogging("force")); // ? Necess�rio!
```

---

### **Problema: N�vel Din�mico N�o Muda**

**Sintoma:**
O n�vel de log n�o muda automaticamente mesmo com muitos erros.

**Verifica��es:**

1. **SmartLogOptions configurado?**
```csharp
builder.Services.AddSmartLogEconomy(builder.Configuration);
```

2. **appsettings.json correto?**
```json
{
  "smartLog-dev": {
    "EconomyLevel": "Error",
    "HighVerbosityLevel": "Information",
    "AbsoluteErrorThreshold": 15,
    "LogWindowSeconds": 60
  }
}
```

3. **Middleware registrado?**
```csharp
app.UseMiddlewareSmartLogEconomy();
```

---

### **Problema: M�tricas N�o Capturadas**

**Sintoma:**
`MetricsRegistry` sempre retorna 0 eventos.

**Solu��o:**
O sub-logger de m�tricas � configurado automaticamente em `ApplyLevelSwitch`. Verifique:

```csharp
// Sub-logger de m�tricas (j� configurado automaticamente)
.WriteTo.Logger(metricsLogger => metricsLogger
    .MinimumLevel.Verbose()
    .WriteTo.Sink(new LogCountingInterceptor(metricsRegistry)));
```

---

## ?? Compara��o: Antes vs Depois

| Aspecto | ANTES (Problem�tico) | DEPOIS (Corrigido) |
|---------|---------------------|-------------------|
| **Duplica��o** | ? Logs aparecem 2x | ? Zero duplica��o |
| **Controle Din�mico** | ? N�o funcionava | ? Totalmente funcional |
| **ForceLogging** | ? N�o integrado | ? API fluente `.WithForceLogging()` |
| **M�tricas** | ? Captura limitada | ? Sub-logger isolado captura tudo |
| **Overrides** | ? N�o aplicavam | ? Funcionando corretamente |
| **API** | ?? Confusa | ? Fluente e intuitiva |
| **Performance** | ?? Overhead desnecess�rio | ? Otimizada |

---

## ?? Checklist de Implementa��o

- [ ] Adicionar pacote SmartLog.Core ao projeto
- [ ] Registrar servi�os: `builder.Services.AddSmartLogEconomy(builder.Configuration)`
- [ ] Configurar logger: `builder.Host.UseSmartLogFluent(config => ...)`
- [ ] Adicionar middleware: `app.UseMiddlewareSmartLogEconomy()`
- [ ] Configurar `appsettings.json` com `smartLog-dev` ou `smartLog-prd`
- [ ] Testar controle din�mico gerando erros
- [ ] Testar force logging com propriedades especiais
- [ ] Verificar m�tricas no `MetricsRegistry`
- [ ] Validar sincroniza��o Redis (se habilitado)

---

## ?? Exemplo Completo

```csharp
// Program.cs
using Serilog;
using SmartLog.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Registrar SmartLog
builder.Services.AddSmartLogEconomy(builder.Configuration);

// 2. Configurar Logger
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(renderCompact: true)
          .WithFile("logs/app-.log")
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithFromContext()
          .WithForceLogging("force", "urgent"));

var app = builder.Build();

// 3. Middleware
app.UseMiddlewareSmartLogEconomy();

app.MapGet("/", () =>
{
    // Log normal
    Log.Information("Requisi��o recebida");
    
    // Log for�ado
    Log.Debug("Debug for�ado {Data}, Force: {force}", DateTime.Now, true);
    
    return "SmartLog funcionando!";
});

app.Run();
```

---

**?? Vers�o:** 2.0 (Configura��o Fluente sem Duplica��o)  
**?? �ltima Atualiza��o:** 2024  
**?? Reposit�rio:** https://github.com/GlauberCasttro/SmartLog


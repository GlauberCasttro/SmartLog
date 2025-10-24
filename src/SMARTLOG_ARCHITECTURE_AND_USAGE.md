# ?? SmartLog - Arquitetura e Guia Completo

## ?? Índice
1. [Problema Original e Solução](#problema-original-e-solução)
2. [Arquitetura Atual](#arquitetura-atual)
3. [Componentes Principais](#componentes-principais)
4. [Guia de Uso](#guia-de-uso)
5. [Configurações Avançadas](#configurações-avançadas)
6. [Troubleshooting](#troubleshooting)

---

## ?? Problema Original e Solução

### ? **Problema: Duplicação de Logs**

**Sintoma:**
```
[19:56:11 INF] Teste de informação sem o force: 1
[19:56:11 INF] Teste de informação sem o force: 1  ? DUPLICADO!
[19:56:11 INF] Teste com force
[19:56:11 INF] Teste com force  ? DUPLICADO!
```

**Causa Raiz:**
A implementação original criava dois loggers escrevendo para o mesmo destino.

```csharp
// ? ARQUITETURA PROBLEMÁTICA (ANTIGA)
var mainLogger = CreateMainLogger(mainLoggerConfig); // ? Console Sink 1
loggerConfig.WriteTo.Logger(mainLogger);             // ? Console Sink 2 (encaminha para mainLogger)

// Resultado: Cada log aparece 2x!
```

**Fluxo Problemático:**
```
Log.Information("teste")
    ?
Logger Global ? WriteTo.Logger(mainLogger)
    ?                    ?
Console Sink 1      mainLogger ? Console Sink 2
    ?                    ?
"Log duplicado!"    "Log duplicado!"
```

### ? **Solução Implementada**

**Arquitetura Corrigida:**
```csharp
// ? NOVA ARQUITETURA (CORRETA)
public static IHostBuilder UseSmartLogFluent(
    this IHostBuilder hostBuilder,
    Action<FluentLoggerBuilder> configure = null)
{
    return hostBuilder.UseSerilog((context, services, loggerConfig) =>
    {
        // 1. Configuração DIRETA no logger global (sem logger secundário)
        var builder = new FluentLoggerBuilder(loggerConfig, context, false);
        
        // 2. Aplicar configurações via builder fluente
        if (configure != null)
            configure(builder);
        else
            builder.WithConsole(renderCompact: true)
                   .WithMicrosoftOverrides()
                   .WithFromContext();

        // 3. Aplicar controles dinâmicos (métricas + force logging)
        ApplyLevelSwitch(loggerConfig, levelSwitch, metricsRegistry, builder);
    });
}
```

**Fluxo Correto:**
```
Log.Information("teste")
    ?
Logger Global (configurado diretamente)
    ? [Sub-logger isolado para métricas]
    ? [ForceLoggingInterceptor se habilitado]
    ? [LoggingLevelSwitch dinâmico]
    ?
Console Sink (ÚNICO)
    ?
"Log único! ?"
```

---

## ??? Arquitetura Atual

### **Visão Geral**

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
?  ?  1. Sub-logger ISOLADO (métricas)    ?                  ?
?  ?     - MinimumLevel.Verbose()         ?                  ?
?  ?     - LogCountingInterceptor         ?                  ?
?  ?     - Captura TODOS os eventos       ?                  ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?  2. ForceLoggingInterceptor          ?                  ?
?  ?     (se habilitado)                  ?                  ?
?  ?     - Verifica propriedades especiais ?                 ?
?  ?     - Permite logs forçados          ?                  ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?  3. LoggingLevelSwitch (dinâmico)    ?                  ?
?  ?     - Controlado por SmartLogOptions ?                 ?
?  ?     - Muda em runtime                ?                  ?
?  ?     - Sincronização Redis            ?                  ?
?  ????????????????????????????????????????                  ?
?         ?                                                    ?
?  ????????????????????????????????????????                  ?
?  ?  4. Sinks Principais                 ?                  ?
?  ?     - Console (renderCompact ou não) ?                 ?
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

### **1. GetInitialLevelSwitch - Controle Dinâmico**

```csharp
private static LoggingLevelSwitch GetInitialLevelSwitch(IServiceProvider services)
{
    var levelSwitch = services.GetRequiredService<LoggingLevelSwitch>();
    var configLevel = services.GetRequiredService<SmartLogOptions>();
    levelSwitch.MinimumLevel = configLevel.EconomyLevel; // ? Dinâmico!
    return levelSwitch;
}
```

**Características:**
- ? **Controle dinâmico** baseado em `SmartLogOptions.EconomyLevel`
- ? **Mudança em runtime** quando SmartLog detecta problemas
- ? **Sincronização Redis** entre múltiplas instâncias/pods
- ? **Adaptativo** - aumenta verbosidade em situações críticas

**Exemplo de comportamento:**
```csharp
// Situação Normal:
EconomyLevel = Error  // Logs: Error, Fatal

// SmartLog detecta 15 erros em 60s:
EconomyLevel = Information  // Logs: Information, Warning, Error, Fatal (AUTOMÁTICO!)

// Situação normaliza após 5 minutos:
EconomyLevel = Error  // Volta ao normal (AUTOMÁTICO!)
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
    // 1. Sub-logger ISOLADO para métricas
    var config = loggerConfig
        .WriteTo.Logger(metricsLogger => metricsLogger
            .MinimumLevel.Verbose()  // Captura TUDO
            .WriteTo.Sink(new LogCountingInterceptor(metricsRegistry)));

    // 2. Force logging filter (se habilitado)
    if (builder.IsForceLoggingEnabled)
        config = config.Filter.With(new ForceLoggingInterceptor(levelSwitch, properties));

    // 3. Controle dinâmico global
    return config.MinimumLevel.ControlledBy(levelSwitch);
}
```

**Pipeline de Processamento:**

| Passo | Componente | Descrição | Nível |
|-------|-----------|-----------|-------|
| **1** | Sub-logger Métricas | Captura TODOS os eventos para análise | `Verbose` |
| **2** | ForceLoggingInterceptor | Permite logs com `force: true` | Condicional |
| **3** | LoggingLevelSwitch | Controle dinâmico do nível | `EconomyLevel` |
| **4** | Sinks Principais | Console, File, etc. | Filtrado |

---

### **3. ForceLoggingInterceptor - Logs Forçados**

```csharp
public class ForceLoggingInterceptor : ILogEventFilter
{
    public bool IsEnabled(LogEvent logEvent)
    {
        // Se tem propriedade especial = true, permite SEMPRE
        if (HasForceProperty(logEvent)) return true;
        
        // Caso contrário, respeita o levelSwitch
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
// EconomyLevel = Error (só mostra Error/Fatal)

// ? Bloqueado (nível Information < Error)
Log.Information("Log normal");

// ? Permitido (force: true ignora nível)
Log.Information("Log forçado {Data}, Force: {force}", data, true);

// ? Permitido (nível Error >= Error)
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

### **1. Configuração Básica (Padrão)**

```csharp
// Program.cs
builder.Host.UseSmartLogFluent();

// Resultado:
// ? Console com renderCompact
// ? Overrides Microsoft = Warning
// ? Contexto Serilog habilitado
// ? Controle dinâmico ativo
```

### **2. Configuração Customizada**

```csharp
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(renderCompact: true)
          .WithFile("logs/app-.log")
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithFromContext()
          .WithForceLogging("force", "urgent", "critical"));
```

### **3. Configuração por Ambiente**

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Host.UseSmartLogFluent(config =>
        config.WithConsole(renderCompact: false) // Texto legível
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

// Template padrão
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(withTemplateDefault: true));

// Render Compact (JSON)
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole(renderCompact: true));
```

---

## ?? Configurações Avançadas

### **1. SmartLogOptions - Configuração Dinâmica**

```json
// appsettings.json
{
  "smartLog-dev": {
    "CircularBufferSize": 1000,
    "EconomyLevel": "Error",           // ? Nível normal
    "HighVerbosityLevel": "Information", // ? Nível em crise
    "DetectionInterval": "00:00:10",   // ? Verifica a cada 10s
    "MinimumHighVerbosityDurationInMinute": 5, // ? Mínimo 5min em alta verbosidade
    "AbsoluteErrorThreshold": 15,      // ? 15 erros disparam mudança
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
?  1. Monitoramento Contínuo              ?
?     - A cada 10s verifica métricas      ?
?     - Conta erros nos últimos 60s       ?
?                                          ?
?  2. Detecção de Problema                ?
?     - Se erros >= 15 em 60s:            ?
?       ? EconomyLevel = Information      ?
?       ? Registra timestamp da mudança   ?
?                                          ?
?  3. Período Mínimo                      ?
?     - Permanece em Information por 5min ?
?     - Evita oscilações                  ?
?                                          ?
?  4. Normalização                        ?
?     - Após 5min, verifica novamente     ?
?     - Se situação normalizou:           ?
?       ? EconomyLevel = Error            ?
?                                          ?
?  5. Sincronização (Redis)               ?
?     - Todas instâncias sincronizam      ?
?     - Mudança em um pod afeta todos     ?
?                                          ?
???????????????????????????????????????????
```

### **2. Force Logging - Uso Avançado**

```csharp
// Configuração
builder.Host.UseSmartLogFluent(config =>
    config.WithForceLogging("force", "urgent", "critical", "debug"));

// No código
public class OrderService
{
    public void ProcessOrder(Order order)
    {
        // Log normal - pode ser bloqueado
        Log.Information("Processando pedido {OrderId}", order.Id);
        
        // Log forçado - SEMPRE aparece
        if (order.Total > 10000)
        {
            Log.Warning(
                "Pedido de alto valor {OrderId}: {Total}, Urgent: {urgent}", 
                order.Id, 
                order.Total, 
                true); // ? SEMPRE registrado!
        }
        
        // Debug forçado em situação específica
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

**Solução:**
? Certifique-se de usar `UseSmartLogFluent` (nova implementação)
? **NÃO** use `UseSmartLogWithConfigurator` (implementação antiga)

```csharp
// ? CORRETO
builder.Host.UseSmartLogFluent(config => ...);

// ? PROBLEMÁTICO (antiga implementação)
builder.Host.UseSmartLogWithConfigurator((context, services, mainLoggerConfig) => ...);
```

---

### **Problema: ForceLogging Não Funciona**

**Sintoma:**
```csharp
Log.Information("Teste {Message}, Force: {force}", "teste", true);
// ? Não aparece mesmo com force: true
```

**Solução:**
Habilite o ForceLoggingInterceptor:

```csharp
// ? CORRETO
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole()
          .WithForceLogging("force")); // ? Necessário!
```

---

### **Problema: Nível Dinâmico Não Muda**

**Sintoma:**
O nível de log não muda automaticamente mesmo com muitos erros.

**Verificações:**

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

### **Problema: Métricas Não Capturadas**

**Sintoma:**
`MetricsRegistry` sempre retorna 0 eventos.

**Solução:**
O sub-logger de métricas é configurado automaticamente em `ApplyLevelSwitch`. Verifique:

```csharp
// Sub-logger de métricas (já configurado automaticamente)
.WriteTo.Logger(metricsLogger => metricsLogger
    .MinimumLevel.Verbose()
    .WriteTo.Sink(new LogCountingInterceptor(metricsRegistry)));
```

---

## ?? Comparação: Antes vs Depois

| Aspecto | ANTES (Problemático) | DEPOIS (Corrigido) |
|---------|---------------------|-------------------|
| **Duplicação** | ? Logs aparecem 2x | ? Zero duplicação |
| **Controle Dinâmico** | ? Não funcionava | ? Totalmente funcional |
| **ForceLogging** | ? Não integrado | ? API fluente `.WithForceLogging()` |
| **Métricas** | ? Captura limitada | ? Sub-logger isolado captura tudo |
| **Overrides** | ? Não aplicavam | ? Funcionando corretamente |
| **API** | ?? Confusa | ? Fluente e intuitiva |
| **Performance** | ?? Overhead desnecessário | ? Otimizada |

---

## ?? Checklist de Implementação

- [ ] Adicionar pacote SmartLog.Core ao projeto
- [ ] Registrar serviços: `builder.Services.AddSmartLogEconomy(builder.Configuration)`
- [ ] Configurar logger: `builder.Host.UseSmartLogFluent(config => ...)`
- [ ] Adicionar middleware: `app.UseMiddlewareSmartLogEconomy()`
- [ ] Configurar `appsettings.json` com `smartLog-dev` ou `smartLog-prd`
- [ ] Testar controle dinâmico gerando erros
- [ ] Testar force logging com propriedades especiais
- [ ] Verificar métricas no `MetricsRegistry`
- [ ] Validar sincronização Redis (se habilitado)

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
    Log.Information("Requisição recebida");
    
    // Log forçado
    Log.Debug("Debug forçado {Data}, Force: {force}", DateTime.Now, true);
    
    return "SmartLog funcionando!";
});

app.Run();
```

---

**?? Versão:** 2.0 (Configuração Fluente sem Duplicação)  
**?? Última Atualização:** 2024  
**?? Repositório:** https://github.com/GlauberCasttro/SmartLog


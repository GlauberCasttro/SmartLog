# 🎯 **CENÁRIOS DE USO AVANÇADOS - INTELLIGENT LOGGING**

> **Exemplos práticos e configurações especializadas para diferentes tipos de aplicação**

---

## 📋 **Índice de Cenários**

- [Cenário 1: Observabilidade com DataDog](#cenário-23-observabilidade-com-datadog)
- [Cenário 2: Aplicação de Alto Volume](#cenário-24-aplicação-de-alto-volume-avançado)
- [Cenário 3: Microserviços com Dependências](#cenário-25-microserviços-com-dependências)
- [Cenário 3: Aplicação com Picos Sazonais](#cenário-26-aplicação-com-picos-sazonais-avançado)
- [Cenário 5: Otimização para Alta Concorrência](#cenário-28-otimização-para-alta-concorrência)
- [Cenário 6: Configuração Balanceada](#cenário-29-configuração-balanceada)

---

## 🔧 **Cenário 1: Observabilidade com DataDog**

```csharp
// Program.cs - Configuração completa com DataDog
var builder = WebApplication.CreateBuilder(args);

// Configuração via builder
builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    config.ForProduction(
        economy: LogEventLevel.Warning,
        highVerbosity: LogEventLevel.Information,
        detectionInterval: TimeSpan.FromMinutes(2),
        logWindowSeconds: 300,
        absoluteErrorThreshold: 30,
        minimumHighVerbosityDurationInMinute: 15,
        enableRedis: true
    ));

### 🔄 **Fluxo de Funcionamento**
2. Configuração no appSettings

```json
{
    "smartLogEconomy-dev": {
        "CircularBufferSize": 1000,
        "EconomyLevel": "Error",
        "HighVerbosityLevel": "Information",
        "DetectionInterval": "00:00:30",
        "MinimumHighVerbosityDurationInMinute": 1,
        "AbsoluteErrorThreshold": 15,
        "LogWindowSeconds": 60,
        "EnableRedisChannelListener": true,
        "LoadWorkerSincronizedInMinute": 5
    },
    "smartLogEconomy-prd": {
        "CircularBufferSize": 5000,
        "EconomyLevel": "Error",
        "HighVerbosityLevel": "Information",
        "DetectionInterval": "00:05:00",
        "MinimumHighVerbosityDurationInMinute": 30,
        "AbsoluteErrorThreshold": 50,
        "LogWindowSeconds": 300,
        "EnableRedisChannelListener": true,
        "LoadWorkerSincronizedInMinute": 5
    }
}
`````
----
//Configuração direta
builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    {
        config.CircularBufferSize = 1000;
        config.EconomyLevel = LogEventLevel.Error;
        config.HighVerbosityLevel = LogEventLevel.Debug;
        config.MinimumHighVerbosityDurationInMinute = 5;
        config.DetectionInterval = TimeSpan.FromSeconds(30);
        config.EnableRedisChannelListener = true;
        config.AbsoluteErrorThreshold = 10;
        config.LogWindowSeconds = 120;
        config.LoadWorkerSincronizedInMinute = 5;
    });

----
// Configuração avançada do Serilog com DataDog
builder.Host.UseSmartLogWithConfigurator((context, services, mainLoggerConfig) =>
{
    // Configuração específica para esta aplicação
    return mainLoggerConfig
        .Enrich.WithProperty("dd_env", Environment.GetEnvironmentVariable("AMBIENTE"))
        .Enrich.WithProperty("dd_trace_id", CorrelationIdentifier.TraceId.ToString())
        .Enrich.WithProperty("dd_service", CorrelationIdentifier.Service)
        .Enrich.WithProperty("dd_version", CorrelationIdentifier.Version)
        .Enrich.WithProperty("dd_span_id", CorrelationIdentifier.SpanId.ToString())
        #if DEBUG
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}")
        #else
        .WriteTo.Console(new RenderedCompactJsonFormatter())
        #endif
        .Enrich.WithProperty("squad", "seguranca de frota");
});

var app = builder.Build();
app.UseSmartLogEconomy();
app.MapControllers();
app.Run();
```

## 🔧 **Cenário 2: Aplicação de Alto Volume (Avançado)**

```csharp
builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
{
    return config
        .WithLogLevels(LogEventLevel.Error, LogEventLevel.Warning)
        .WithTimings(TimeSpan.FromMinutes(1), 600)  // Detecção frequente, janela maior
        .WithThresholds(100, 60)                     // Threshold alto, duração longa
        .EnableRedis(true)
        .WithBufferSize(5000);                       // Buffer maior para alto volume
});
```

### **Justificativa da Configuração:**

- **`DetectionInterval = 1min`**: Detecção frequente para capturar picos rapidamente
- **`LogWindowSeconds = 600`**: Janela de 10min para suavizar variações naturais
- **`AbsoluteErrorThreshold = 100`**: Alto threshold apropriado para aplicações de grande escala
- **`CircularBufferSize = 5000`**: Buffer maior para suportar alto volume de eventos
- **`MinimumDuration = 60min`**: Duração longa para evitar oscilações em sistemas estáveis
---

## 🏢 **Cenário 3: Microserviços com Dependências**

```csharp
// Serviço A - API Gateway (Ponto de entrada crítico)
builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    config.ForProduction()
          .WithThresholds(30, 10)); // Mais sensível para detectar problemas rapidamente

// Serviço B - Worker em Background (Tolerante a falhas)
builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    config.ForProduction()
          .WithThresholds(50, 30)); // Mais tolerante, pode ter falhas ocasionais

// Serviço C - Cache/Redis (Crítico para performance)
builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
    config.ForProduction()
          .WithThresholds(20, 15)); // Muito sensível a qualquer falha
```

## ⚡ **Cenário 4: Otimização para Alta Concorrência**

```csharp
builder.Services.AddSmartLogEconomy(builder.Configuration, config =>
{
    return config
        .WithBufferSize(10000)                    // Buffer grande para suportar volume
        .WithTimings(TimeSpan.FromMinutes(1), 300) // Detecção frequente
        .WithThresholds(100, 30)                  // Threshold alto para filtrar ruído
        .EnableRedis(true);                       // Sincronização entre instâncias
});

### **Estratégia por Criticidade:**

| Tipo de Serviço | Threshold | Duração Mín. | Justificativa |
|------------------|-----------|--------------|---------------|
| **API Gateway** | 30 erros | 10 min | Ponto único de falha, precisa detecção rápida |
| **Worker** | 50 erros | 30 min | Pode reprocessar, tolerante a falhas temporárias |
| **Cache** | 20 erros | 15 min | Performance crítica, falhas afetam todo sistema |

### **Benefícios:**
- **Redução de Ruído**: Menos alertas em horários de baixo tráfego
- **Detecção Precoce**: Maior sensibilidade durante picos esperados
- **Economia de Recursos**: Logging otimizado por contexto temporal
```

### **Otimizações Implementadas:**

1. **Buffer Circular Grande**: 10.000 eventos para evitar overflow
2. **Sinks Assíncronos**: Não bloqueia threads da aplicação
6. **Buffer de Escrita**: Usa buffer interno do Serilog

---
### **Características da Configuração Balanceada:**

| Parâmetro | Valor | Justificativa |
|-----------|-------|---------------|
| **Buffer Size** | 1000 | Bom histórico sem uso excessivo de memória. Ou a critério |
| **Detection Interval** | 2 min | Responsivo mas não excessivo |
| **Log Window** | 5 min | Janela para análise de tendências |
| **Error Threshold** | 30 | Sensível a problemas reais, tolerante a ruído |
| **Min Duration** | 15 min | Estabilidade sem inércia excessiva |

---

## 🎯 **Escolhendo o Cenário Ideal**

### **Critérios de Decisão:**

1. **Volume de Tráfego**: Quantos requests/logs por minuto?
2. **Criticidade**: Tolerância a falhas e tempo de recuperação
3. **Recursos**: Memória e CPU disponíveis
4. **Arquitetura**: Monolito vs. microserviços
5. **Padrões de Uso**: Constante vs. sazonal


*Estes cenários cobrem os principais casos de uso e podem ser combinados ou adaptados conforme necessário.*

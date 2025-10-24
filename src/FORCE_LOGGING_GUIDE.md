# ?? SmartLog - ForceLoggingInterceptor Guia de Uso

## ? **Problema Resolvido: Duplicação de Logs + ForceLoggingInterceptor Funcional**

A duplicação de logs foi **completamente resolvida** e o `ForceLoggingInterceptor` agora está totalmente integrado à API fluente!

## ?? **Como Usar o ForceLoggingInterceptor**

### **1. Configuração Básica**
```csharp
// Habilita force logging com propriedade padrão "force"
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole()
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("SmartLogApp")
          .WithForceLogging()); // ?? Habilita com "force" padrão
```

### **2. Configuração com Múltiplas Propriedades**
```csharp
// Habilita force logging com múltiplas propriedades especiais
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole()
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("SmartLogApp")
          .WithForceLogging("force", "urgent", "critical")); // ?? Múltiplas propriedades
```

### **3. No Código - Como Usar**

```csharp
// Configuração: EconomyLevel = Error (só mostra logs Error ou Fatal)

// ? Este log NÃO aparecerá (nível Information < Error)
Log.Information("Log normal - não aparece");

// ? Este log SEMPRE aparecerá (force=true ignora nível)
Log.Information("Log forçado - SEMPRE aparece {Message}, Force: {force}", "Importante!", true);

// ? Este log também aparecerá (urgent=true)
Log.Information("Log urgente {Message}, Urgent: {urgent}", "Super importante!", true);

// ? Este log aparecerá (critical=true)
Log.Debug("Log de debug crítico {Details}, Critical: {critical}", "Detalhes importantes", true);
```

## ?? **Como Funciona Internamente**

### **Fluxo Normal (sem force)**
```
Log.Information("teste") 
    ? Level: Information
    ? EconomyLevel: Error  
    ? Information < Error
    ? LOG BLOQUEADO
```

### **Fluxo com ForceLogging**
```
Log.Information("teste {force}", true)
    ? ForceLoggingInterceptor detecta force=true
    ? HasForceProperty() retorna true
    ? LOG PERMITIDO (ignora nível)
```

## ?? **Configurações Disponíveis**

### **UseSmartLogFluentTest (Recomendado para teste)**
```csharp
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole("[{Timestamp:HH:mm:ss} {Level:u3}] {Message}")
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("MeuApp")
          .WithForceLogging("force", "urgent"));
```

### **UseSmartLogFluent (Versão principal)**
```csharp
builder.Host.UseSmartLogFluent(config =>
    config.WithConsole()
          .WithMicrosoftOverrides()
          .WithEnrichment()
          .WithForceLogging());
```

### **Presets com ForceLogging**
```csharp
// Para desenvolvimento
builder.Host.UseSmartLogWithPreset(LoggerPreset.Development);
// Adicione force logging depois se necessário

// Para produção
builder.Host.UseSmartLogFluent(config =>
    config.ForProduction()
          .WithForceLogging("force"));
```

## ?? **Testando se Funciona**

### **1. Configure sua aplicação:**
```csharp
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole()
          .WithForceLogging("force"));
```

### **2. No seu Controller:**
```csharp
[HttpGet("test-force")]
public IActionResult TestForceLogging()
{
    // Com EconomyLevel=Error, apenas o segundo log aparecerá
    Log.Information("Log normal - pode não aparecer");
    Log.Information("Log forçado - SEMPRE aparece {Message}, Force: {force}", "Funciona!", true);
    
    return Ok("Teste executado!");
}
```

### **3. Execute e verifique:**
- ? **SEM duplicação**: Cada log aparece apenas UMA vez
- ? **Force working**: Logs com `force: true` sempre aparecem
- ? **Overrides funcionando**: Logs Microsoft suprimidos

## ?? **Cenários de Uso**

### **Debug em Produção**
```csharp
// Produção com EconomyLevel=Error, mas debug específico
Log.Debug("Processando usuário {UserId}, Force: {force}", userId, needsDebugging);
```

### **Logs Críticos**
```csharp
// Sempre registrar eventos críticos independente do nível
Log.Information("Falha de segurança detectada {Details}, Force: {force}", details, true);
```

### **Troubleshooting**
```csharp
// Durante investigação, forçar logs específicos
Log.Verbose("Estado do cache {CacheState}, Force: {force}", state, isInvestigating);
```

## ?? **Comparação: Antes vs Depois**

### **? ANTES (Problemático)**
```
[19:56:11 INF] Teste de informação sem o force: 1
[19:56:11 INF] Teste de informação sem o force: 1  ? DUPLICADO!
[19:56:11 INF] Teste com force
[19:56:11 INF] Teste com force  ? DUPLICADO!
```

### **? DEPOIS (Corrigido)**
```
[19:56:11 INF] Teste de informação sem o force: 1
[19:56:11 INF] Teste forçado - SEMPRE aparece Funciona!, Force: True
```

## ?? **Propriedades Suportadas**

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| `force` | `bool` | Propriedade padrão para forçar logs |
| `urgent` | `bool` | Para logs urgentes |
| `critical` | `bool` | Para logs críticos |
| `debug` | `bool` | Para debug forçado |
| **Custom** | `bool` | Qualquer nome que você definir |

## ? **Status Final**

- ? **Duplicação resolvida**: Zero logs duplicados
- ? **ForceLoggingInterceptor funcionando**: Logs com propriedades especiais sempre aparecem
- ? **API fluente completa**: Configuração intuitiva e flexível
- ? **Compatibilidade mantida**: Métodos antigos ainda funcionam (marcados como obsoletos)
- ? **Performance otimizada**: Um único logger, sem overhead

## ?? **Exemplo Completo de Uso**

```csharp
// Program.cs
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole("[{Timestamp:HH:mm:ss} {Level:u3}] {Message}")
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("MeuApp")
          .WithForceLogging("force", "urgent", "critical"));

// Controller.cs
[HttpGet("exemplo")]
public IActionResult Exemplo()
{
    // EconomyLevel = Error
    
    Log.Information("Log normal - não aparece");                               // ? Bloqueado
    Log.Warning("Log warning - não aparece");                                 // ? Bloqueado  
    Log.Error("Log error - aparece");                                         // ? Permitido
    Log.Information("Log forçado {Value}, Force: {force}", "teste", true);    // ? Forçado
    Log.Debug("Debug urgente {Data}, Urgent: {urgent}", data, true);          // ? Forçado
    
    return Ok("Exemplo executado!");
}
```

---

**?? O SmartLog agora está completamente funcional sem duplicação e com ForceLoggingInterceptor integrado!**
# ?? SmartLog - ForceLoggingInterceptor Guia de Uso

## ? **Problema Resolvido: Duplica��o de Logs + ForceLoggingInterceptor Funcional**

A duplica��o de logs foi **completamente resolvida** e o `ForceLoggingInterceptor` agora est� totalmente integrado � API fluente!

## ?? **Como Usar o ForceLoggingInterceptor**

### **1. Configura��o B�sica**
```csharp
// Habilita force logging com propriedade padr�o "force"
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole()
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("SmartLogApp")
          .WithForceLogging()); // ?? Habilita com "force" padr�o
```

### **2. Configura��o com M�ltiplas Propriedades**
```csharp
// Habilita force logging com m�ltiplas propriedades especiais
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole()
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("SmartLogApp")
          .WithForceLogging("force", "urgent", "critical")); // ?? M�ltiplas propriedades
```

### **3. No C�digo - Como Usar**

```csharp
// Configura��o: EconomyLevel = Error (s� mostra logs Error ou Fatal)

// ? Este log N�O aparecer� (n�vel Information < Error)
Log.Information("Log normal - n�o aparece");

// ? Este log SEMPRE aparecer� (force=true ignora n�vel)
Log.Information("Log for�ado - SEMPRE aparece {Message}, Force: {force}", "Importante!", true);

// ? Este log tamb�m aparecer� (urgent=true)
Log.Information("Log urgente {Message}, Urgent: {urgent}", "Super importante!", true);

// ? Este log aparecer� (critical=true)
Log.Debug("Log de debug cr�tico {Details}, Critical: {critical}", "Detalhes importantes", true);
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
    ? LOG PERMITIDO (ignora n�vel)
```

## ?? **Configura��es Dispon�veis**

### **UseSmartLogFluentTest (Recomendado para teste)**
```csharp
builder.Host.UseSmartLogFluentTest(config =>
    config.WithConsole("[{Timestamp:HH:mm:ss} {Level:u3}] {Message}")
          .WithMicrosoftOverrides(LogEventLevel.Warning)
          .WithEnrichment("MeuApp")
          .WithForceLogging("force", "urgent"));
```

### **UseSmartLogFluent (Vers�o principal)**
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
// Adicione force logging depois se necess�rio

// Para produ��o
builder.Host.UseSmartLogFluent(config =>
    config.ForProduction()
          .WithForceLogging("force"));
```

## ?? **Testando se Funciona**

### **1. Configure sua aplica��o:**
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
    // Com EconomyLevel=Error, apenas o segundo log aparecer�
    Log.Information("Log normal - pode n�o aparecer");
    Log.Information("Log for�ado - SEMPRE aparece {Message}, Force: {force}", "Funciona!", true);
    
    return Ok("Teste executado!");
}
```

### **3. Execute e verifique:**
- ? **SEM duplica��o**: Cada log aparece apenas UMA vez
- ? **Force working**: Logs com `force: true` sempre aparecem
- ? **Overrides funcionando**: Logs Microsoft suprimidos

## ?? **Cen�rios de Uso**

### **Debug em Produ��o**
```csharp
// Produ��o com EconomyLevel=Error, mas debug espec�fico
Log.Debug("Processando usu�rio {UserId}, Force: {force}", userId, needsDebugging);
```

### **Logs Cr�ticos**
```csharp
// Sempre registrar eventos cr�ticos independente do n�vel
Log.Information("Falha de seguran�a detectada {Details}, Force: {force}", details, true);
```

### **Troubleshooting**
```csharp
// Durante investiga��o, for�ar logs espec�ficos
Log.Verbose("Estado do cache {CacheState}, Force: {force}", state, isInvestigating);
```

## ?? **Compara��o: Antes vs Depois**

### **? ANTES (Problem�tico)**
```
[19:56:11 INF] Teste de informa��o sem o force: 1
[19:56:11 INF] Teste de informa��o sem o force: 1  ? DUPLICADO!
[19:56:11 INF] Teste com force
[19:56:11 INF] Teste com force  ? DUPLICADO!
```

### **? DEPOIS (Corrigido)**
```
[19:56:11 INF] Teste de informa��o sem o force: 1
[19:56:11 INF] Teste for�ado - SEMPRE aparece Funciona!, Force: True
```

## ?? **Propriedades Suportadas**

| Propriedade | Tipo | Descri��o |
|-------------|------|-----------|
| `force` | `bool` | Propriedade padr�o para for�ar logs |
| `urgent` | `bool` | Para logs urgentes |
| `critical` | `bool` | Para logs cr�ticos |
| `debug` | `bool` | Para debug for�ado |
| **Custom** | `bool` | Qualquer nome que voc� definir |

## ? **Status Final**

- ? **Duplica��o resolvida**: Zero logs duplicados
- ? **ForceLoggingInterceptor funcionando**: Logs com propriedades especiais sempre aparecem
- ? **API fluente completa**: Configura��o intuitiva e flex�vel
- ? **Compatibilidade mantida**: M�todos antigos ainda funcionam (marcados como obsoletos)
- ? **Performance otimizada**: Um �nico logger, sem overhead

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
    
    Log.Information("Log normal - n�o aparece");                               // ? Bloqueado
    Log.Warning("Log warning - n�o aparece");                                 // ? Bloqueado  
    Log.Error("Log error - aparece");                                         // ? Permitido
    Log.Information("Log for�ado {Value}, Force: {force}", "teste", true);    // ? For�ado
    Log.Debug("Debug urgente {Data}, Urgent: {urgent}", data, true);          // ? For�ado
    
    return Ok("Exemplo executado!");
}
```

---

**?? O SmartLog agora est� completamente funcional sem duplica��o e com ForceLoggingInterceptor integrado!**
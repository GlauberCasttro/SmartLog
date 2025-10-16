# 📊 Smart Logging - Performance Benchmarks

## 🚀 Como Executar os Benchmarks

### Opção 1: Script Automatizado (Recomendado)
```powershell
.\run-benchmarks.ps1
```

### Opção 2: Comando Manual
```bash
dotnet test --filter "Benchmark" --logger:console --verbosity:normal
```

## 📈 Relatórios Gerados

Após executar os benchmarks, são gerados 3 tipos de relatórios:

### 🌐 HTML Report (Visual)
- **Localização**: `testes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\BenchmarkResults.html`
- **Conteúdo**: Relatório visual completo com tabelas, gráficos e análises
- **Uso**: Abra no navegador para visualização interativa

### 📋 JSON Report (Programático)
- **Localização**: `estes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\BenchmarkResults.json`
- **Conteúdo**: Dados estruturados com metadados do ambiente
- **Uso**: Análises programáticas

### 📈 CSV Report (Planilhas)
- **Localização**: `estes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\BenchmarkResults.csv`
- **Conteúdo**: Dados tabulares para análise
- **Uso**: Excel, Google Sheets, análises estatísticas

## 🧪 Benchmarks Implementados

### 1. Memory Leak Validation
- **Objetivo**: Validar prevenção de memory leak
- **Carga**: 100,000 operações mistas
- **Métricas**: Tempo por operação, uso de memória, throughput

### 2. Concurrent Access Performance
- **Objetivo**: Testar thread safety e performance concorrente
- **Cenários**: 3 configurações (buffer pequeno/médio/grande)
- **Métricas**: Throughput, estabilidade de buffer

### 3. Sliding Window Performance
- **Objetivo**: Validar eficiência do cleanup automático
- **Carga**: 10,000 operações com cleanup frequente
- **Métricas**: Impacto na performance, utilização do buffer

### 4. Memory Pressure Stability
- **Objetivo**: Comportamento sob pressão de memória
- **Condições**: 100MB de pressão adicional + 50,000 operações
- **Métricas**: Estabilidade, performance sob stress

## 📊 Resultados Típicos Esperados

```
| Method                              | Operations | Mean        | Memory     | Throughput    |
| ----------------------------------- | ---------- | ----------- | ---------- | ------------- |
| RecordLogEvent_MemoryLeakValidation |    100,000 |     ~0.12μs |    ~72KB   |   8,300k ops/s|
| ConcurrentAccess_Buffer1000_Ops10k  |     10,000 |     ~5.89μs |    ~2KB    |     170k ops/s|
| SlidingWindow_PerformanceValidation |     10,000 |    ~15.0μs  |   ~100B    |     667k ops/s|
| MemoryPressure_StabilityTest        |     50,000 |     ~0.34μs |  ~103MB    |   2,950k ops/s|
```

## 🎯 Interpretação dos Resultados

### ✅ Critérios de Sucesso
- **Memory Leak**: Buffer sempre ≤ limite configurado
- **Performance**: < 200μs por operação na pior condição
- **Throughput**: > 1,000 ops/sec mesmo com concorrência
- **Thread Safety**: Sem corrupção de dados

### 🔍 Análise de Métricas

#### Memory Leak Prevention
- ✅ **PASSED**: Buffer controlado, memória limitada
- ❌ **FAILED**: Crescimento descontrolado de memória

#### Performance Benchmarks
- ✅ **EXCELLENT**: < 10μs por operação
- ✅ **GOOD**: 10-100μs por operação  
- ⚠️ **ACCEPTABLE**: 100-200μs por operação
- ❌ **POOR**: > 200μs por operação

#### Throughput Analysis
- ✅ **HIGH**: > 100k ops/sec
- ✅ **MEDIUM**: 10k-100k ops/sec
- ⚠️ **LOW**: 1k-10k ops/sec
- ❌ **CRITICAL**: < 1k ops/sec

## 🔧 Troubleshooting

### Problema: Benchmarks Muito Lentos
```bash
# Solução: Executar em Release mode
dotnet test -c Release --filter "Benchmark"
```

### Problema: Relatórios Não Gerados
```bash
# Verificar se o diretório existe
ls testes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\
```

### Problema: Memory Pressure Test Falha
- **Causa**: Sistema com pouca RAM disponível
- **Solução**: Fechar aplicações que consomem memória

## 📚 Recursos Adicionais

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Performance Best Practices](https://docs.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/performance-warnings)
- [Memory Management in .NET](https://docs.microsoft.com/dotnet/standard/garbage-collection/)

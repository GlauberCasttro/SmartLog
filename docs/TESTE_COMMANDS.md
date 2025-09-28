# 🧪 INTELLIGENT LOGGING - COMANDOS DE TESTE

## ⚡ Execução Rápida
```bash
# Executar todos os testes
dotnet test

# Executar com output detalhado
dotnet test --logger:console --verbosity:normal

# Executar testes específicos
dotnet test --filter "MetricsRegistryTests"
dotnet test --filter "SmartLogEconomyDetectorTests"
```

## 📊 Relatórios e Coverage
```bash
# Executar com coverage
dotnet test --collect:"XPlat Code Coverage"

# Gerar relatório HTML
dotnet test --logger:html --results-directory:TestResults

# Executar com análise de performance
dotnet test --logger:console --verbosity:detailed
```

## 🎯 Testes por Categoria
```bash
# Testes unitários apenas
dotnet test --filter "Category=Unit"

# Testes de integração apenas  
dotnet test --filter "Category=Integration"

# Testes críticos (memory leak)
dotnet test --filter "FullyQualifiedName~MetricsRegistryTests"
```

## 🔍 Debug e Troubleshooting
```bash
# Executar em modo debug
dotnet test --logger:console --verbosity:diagnostic

# Executar teste específico com debug
dotnet test --filter "RecordLogEvent_QuandoBufferCheio_DeveNaoBloqueiarNovosEventos" --logger:console

# Executar com breakpoints (VS Code)
# Usar: Test Explorer > Debug Test
```

## 📈 CI/CD Pipeline
```bash
# Para pipeline automatizado
dotnet test --logger:trx --results-directory:TestResults
dotnet test --logger:"junit;LogFilePath=TestResults/results.xml"

# Com coverage para SonarQube
dotnet test --collect:"XPlat Code Coverage" --logger:trx --results-directory:TestResults
```

## 🎪 Testes de Performance
```bash
# Executar benchmarks específicos
dotnet test --filter "Benchmark" --logger:console --verbosity:normal

# Executar via script PowerShell (Recomendado)
.\run-benchmarks.ps1

# Executar benchmarks em modo Release (Produção)
dotnet test --filter "Benchmark" -c Release --logger:console --verbosity:normal

# Teste de stress do buffer
dotnet test --filter "TestWithLargeDataSets" --logger:console
```

## 🚀 Script de Benchmarks (run-benchmarks.ps1)
```powershell
# Executar o script completo de benchmarks
.\run-benchmarks.ps1

# Alternativas manuais para casos específicos:

# Apenas benchmarks de memória
dotnet test --filter "Benchmark_MemoryPressure"

# Apenas benchmarks de concorrência
dotnet test --filter "Benchmark_ConcurrentAccess"

# Apenas validação de memory leak
dotnet test --filter "Benchmark_RecordLogEvent_MemoryLeakValidation"

# Gerar apenas relatórios (sem executar testes)
dotnet test --filter "Benchmark_GenerateReport"
```

## 📊 Visualização de Resultados
```bash
# Após executar benchmarks, os relatórios ficam em:
# ./tests/Intelligent.Logging.Tests/bin/Debug/net8.0/BenchmarkResults/

# Abrir relatório HTML manualmente
start ./testes/SmartLog.Testes/bin/Debug/net8.0/BenchmarkResults/BenchmarkResults.html

# Ver resultados em CSV
type ./testes/SmartLog.Testes/bin/Debug/net8.0/BenchmarkResults/BenchmarkResults.csv

# Ver histórico de performance
type ./testes/SmartLog.Testes/bin/Debug/net8.0/BenchmarkResults/BenchmarkHistory.json
```

## 🏗️ Build e Test em Sequência
```bash
# Build completo + testes
dotnet build --configuration Release
dotnet test --configuration Release --no-build

# Clean build + teste
dotnet clean
dotnet build
dotnet test
```

## 🎯 FOCO NO MEMORY LEAK FIX ()
```bash
# Testa especificamente a correção do memory leak
dotnet test --filter "RecordLogEvent_QuandoBufferCheio_DeveNaoBloqueiarNovosEventos"

# Validação completa do MetricsRegistry
dotnet test --filter "MetricsRegistryTests" --logger:console --verbosity:normal

# Testes de comportamento contínuo
dotnet test --filter "RecordLogEvent_SempreQueChamado_DeveAceitarNovosEventos"
```

## 📱 Watch Mode (Desenvolvimento)
```bash
# Executa testes automaticamente ao modificar código
dotnet watch test

# Watch com filtro específico
dotnet watch test --filter "MetricsRegistryTests"
```

## 🔧 Troubleshooting Comum

### Problema: Testes não encontram dependências
```bash
# Solução: Restaurar pacotes
dotnet restore
dotnet build
```

### Problema: Redis connection errors nos testes
```bash
# Solução: Usar mocks (já implementado nos testes)
# Os testes não dependem de Redis real
```

### Problema: Testes lentos
```bash
# Solução: Executar em paralelo
dotnet test --parallel

# Ou rodar subconjunto específico
dotnet test --filter "Category=Fast"
```

### Problema: Script PowerShell não executa
```bash
# Solução 1: Verificar política de execução
Get-ExecutionPolicy
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Solução 2: Executar diretamente
powershell -ExecutionPolicy Bypass -File run-benchmarks.ps1

# Solução 3: Via VS Code Terminal
.\run-benchmarks.ps1
```

### Problema: Relatórios de benchmark não são gerados
```bash
# Verificar se testes de benchmark passaram
dotnet test --filter "Benchmark" --logger:console

# Verificar diretório de saída
ls ./tests/Intelligent.Logging.Tests/bin/Debug/net8.0/BenchmarkResults/

# Executar teste específico de geração de relatório
dotnet test --filter "Benchmark_GenerateReport"
```

### Problema: Performance degradada nos benchmarks
```bash
# Executar em modo Release para resultados precisos
dotnet test --filter "Benchmark" -c Release

# Verificar se sistema está sob carga
# Feche aplicações desnecessárias antes dos benchmarks

# Comparar com histórico anterior
type ./tests/Intelligent.Logging.Tests/bin/Debug/net8.0/BenchmarkResults/BenchmarkHistory.json
```

## 🇧🇷 NOMENCLATURA DOS TESTES EM PORTUGUÊS

### Padrão Adotado: `NomeDoMetodo_QuandoFizerAlgo_OQueEspera`

**Exemplos dos testes implementados:**
- `RecordLogEvent_QuandoBufferCheio_DeveNaoBloqueiarNovosEventos` ✅
- `GetStatus_QuandoChamado_DeveRetornarEstadoAtual` ✅
- `Validate_ComValoresInvalidos_DeveLancarArgumentException` ✅
- `RunDetectionCycleAsync_ComMuitosErros_DeveMudarParaAltaVerbosidade` ✅

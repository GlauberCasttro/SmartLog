# Intelligent Logging - Benchmark Runner
# Execute este script para rodar benchmarks e visualizar resultados

Write-Host "Executando Benchmarks do Intelligent Logging..." -ForegroundColor Cyan
Write-Host ""

# Executar benchmarks
dotnet test --filter "Benchmark" --logger:console --verbosity:normal

Write-Host ""
Write-Host "Benchmarks concluidos!" -ForegroundColor Green
Write-Host ""

# Encontrar o caminho do relatorio HTML
$projectRoot = Join-Path $PSScriptRoot ".." | Resolve-Path
$reportPath = Join-Path $projectRoot "testes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\BenchmarkResults.html"
$jsonPath = Join-Path $projectRoot "testes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\BenchmarkResults.json"
$csvPath = Join-Path $projectRoot "testes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\BenchmarkResults.csv"
$historyPath = Join-Path $projectRoot "testes\SmartLog.Testes\bin\Debug\net8.0\BenchmarkResults\BenchmarkHistory.json"

if (Test-Path $reportPath) {
    Write-Host "Abrindo relatorio HTML no navegador..." -ForegroundColor Yellow
    Start-Process $reportPath
    
    # Estatisticas dos arquivos gerados
    $htmlSize = if (Test-Path $reportPath) { (Get-Item $reportPath).Length } else { 0 }
    $jsonSize = if (Test-Path $jsonPath) { (Get-Item $jsonPath).Length } else { 0 }
    $csvSize = if (Test-Path $csvPath) { (Get-Item $csvPath).Length } else { 0 }
    
    Write-Host ""
    Write-Host "Arquivos de relatorio gerados:" -ForegroundColor Cyan
    Write-Host "   HTML: $reportPath ($([math]::Round($htmlSize/1024, 1)) KB)" -ForegroundColor White
    if (Test-Path $jsonPath) {
        Write-Host "   JSON: $jsonPath ($([math]::Round($jsonSize/1024, 1)) KB)" -ForegroundColor White
    }
    if (Test-Path $csvPath) {
        Write-Host "   CSV:  $csvPath ($([math]::Round($csvSize/1024, 1)) KB)" -ForegroundColor White
    }
    
    # Verificar historico de performance
    if (Test-Path $historyPath) {
        Write-Host "   Historico: $historyPath" -ForegroundColor Gray
    }
    
} else {
    Write-Host "Relatorio HTML nao encontrado em: $reportPath" -ForegroundColor Red
    
    # Procurar relatorios em outros locais
    Write-Host "Procurando relatorios em outros diretorios..." -ForegroundColor Yellow
    $foundReports = Get-ChildItem -Path "." -Recurse -Name "BenchmarkResults.html" -ErrorAction SilentlyContinue
    if ($foundReports) {
        Write-Host "Relatorios encontrados:" -ForegroundColor Cyan
        foreach ($report in $foundReports) {
            Write-Host "   $report" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "Dicas:" -ForegroundColor Yellow
Write-Host "   • Use '-c Release' para benchmarks de producao" -ForegroundColor Gray
Write-Host "   • Relatorios sao salvos automaticamente" -ForegroundColor Gray
Write-Host "   • Execute regularmente para detectar regressoes" -ForegroundColor Gray
Write-Host ""
Write-Host "Processo concluido!" -ForegroundColor Green

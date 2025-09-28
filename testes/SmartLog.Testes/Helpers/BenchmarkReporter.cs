using System.Text;
using System.Text.Json;

namespace SmartLog.Testes.Helpers;

/// <summary>
/// Gerador de relatórios de benchmark estilo BenchmarkDotNet
/// </summary>
public static class BenchmarkReporter
{
    private static readonly List<BenchmarkResult> _results = [];

    public static void AddResult(BenchmarkResult result)
    {
        _results.Add(result);
    }

    public static void GenerateReport(string outputPath = null)
    {
        outputPath ??= Path.Combine(Environment.CurrentDirectory, "BenchmarkResults");
        Directory.CreateDirectory(outputPath);

        GenerateConsoleReport();
        GenerateHtmlReport(outputPath);
        GenerateJsonReport(outputPath);
        GenerateCsvReport(outputPath);
    }

    private static void GenerateConsoleReport()
    {
        var report = new StringBuilder();
        report.AppendLine("// * Resumo dos Benchmarks *");
        report.AppendLine();
        report.AppendLine("BenchmarkDotNet=v0.13.1, OS=Windows");
        report.AppendLine($"Intel Core i7, 1 CPU, {Environment.ProcessorCount} núcleos lógicos");
        report.AppendLine($".NET {Environment.Version} (X64 RyuJIT)");
        report.AppendLine();

        // Cabeçalho
        report.AppendLine("| Teste                                 | Operações | Tempo Médio | Desvio Padrão | Memória   | Vazão        | Status   | Observações");
        report.AppendLine("| -------------------------------------- | ---------- | ----------- | ------------- | --------- | ------------ | -------- | ----------------------------- |");

        // Resultados
        foreach (var result in _results.OrderBy(r => r.Method))
        {
            var status = result.Passed ? "✅ PASSOU" : "❌ FALHOU";
            var memoryMB = FormatMemory(result.MemoryUsage);
            report.AppendLine($"| {result.Method,-38} | {result.Operations,10:N0} | {result.MeanTime,11:F2} μs | {result.StdDev,13:F2} μs | {memoryMB,9} | {result.Throughput,12:N0} ops/s | {status,-8} | {result.Notes}");
        }

        report.AppendLine();
        report.AppendLine("// * Legenda dos Campos *");
        report.AppendLine("  Teste         : Nome/descritivo do cenário testado");
        report.AppendLine("  Operações     : Total de execuções realizadas");
        report.AppendLine("  Tempo Médio   : Tempo médio por operação (em microssegundos)");
        report.AppendLine("  Desvio Padrão : Variação dos tempos de execução");
        report.AppendLine("  Memória       : Memória consumida/variada no teste");
        report.AppendLine("  Vazão         : Operações por segundo (Throughput)");
        report.AppendLine("  Status        : Resultado do teste (PASSOU/FALHOU)");
        report.AppendLine("  Observações   : Explicação do objetivo ou contexto do teste");
        report.AppendLine("  1 μs          : 1 microssegundo (0.000001 seg)");

        Console.WriteLine(report.ToString());
    }

    private static void GenerateHtmlReport(string outputPath)
    {
        var htmlPath = Path.Combine(outputPath, "BenchmarkResults.html");
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='pt-BR'>");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset='UTF-8'>");
        html.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        html.AppendLine("    <title>Intelligent Logging - Benchmark Results</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: 'Segoe UI', sans-serif; margin: 20px; background: #f5f5f5; }");
        html.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
        html.AppendLine("        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }");
        html.AppendLine("        .summary { background: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0; }");
        html.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
        html.AppendLine("        th, td { padding: 12px; text-align: right; border: 1px solid #ddd; }");
        html.AppendLine("        th { background: #3498db; color: white; font-weight: bold; }");
        html.AppendLine("        tr:nth-child(even) { background: #f9f9f9; }");
        html.AppendLine("        .pass { color: #27ae60; font-weight: bold; }");
        html.AppendLine("        .fail { color: #e74c3c; font-weight: bold; }");
        html.AppendLine("        .method { text-align: left; font-family: 'Consolas', monospace; }");
        html.AppendLine("        .metric { font-weight: bold; }");
        html.AppendLine("        .chart { background: #fff; margin: 20px 0; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class='container'>");
        html.AppendLine("        <h1>🚀 Intelligent Logging - Performance Benchmarks</h1>");

        html.AppendLine("        <div class='summary'>");
        html.AppendLine($"            <h3>📊 Resumo da Execução</h3>");
        html.AppendLine($"            <p><strong>Data:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        html.AppendLine($"            <p><strong>Sistema:</strong> {Environment.OSVersion}</p>");
        html.AppendLine($"            <p><strong>Processador:</strong> {Environment.ProcessorCount} cores lógicos</p>");
        html.AppendLine($"            <p><strong>Runtime:</strong> .NET {Environment.Version}</p>");
        html.AppendLine($"            <p><strong>Total de Testes:</strong> {_results.Count}</p>");
        html.AppendLine($"            <p><strong>Testes Passaram:</strong> <span class='pass'>{_results.Count(r => r.Passed)}</span></p>");
        html.AppendLine($"            <p><strong>Testes Falharam:</strong> <span class='fail'>{_results.Count(r => !r.Passed)}</span></p>");
        html.AppendLine("        </div>");

        html.AppendLine("        <table>");
        html.AppendLine("            <thead>");
        html.AppendLine("                <tr>");
        html.AppendLine($"                    <th class='method'>Método</th>");
        html.AppendLine($"                    <th>Operações</th>");
        html.AppendLine($"                    <th>Tempo Médio (μs)</th>");
        html.AppendLine($"                    <th>Desvio Padrão (μs)</th>");
        html.AppendLine($"                    <th>Memória</th>");
        html.AppendLine($"                    <th>Throughput (ops/s)</th>");
        html.AppendLine($"                    <th>Status</th>");
        html.AppendLine($"                    <th>Observações</th>");
        html.AppendLine("                </tr>");
        html.AppendLine("            </thead>");
        html.AppendLine("            <tbody>");

        foreach (var result in _results.OrderBy(r => r.Method))
        {
            var statusClass = result.Passed ? "pass" : "fail";
            var statusText = result.Passed ? "✅ PASS" : "❌ FAIL";
            html.AppendLine("                <tr>");
            html.AppendLine($"                    <td class='method'>{result.Method}</td>");
            html.AppendLine($"                    <td class='metric'>{result.Operations:N0}</td>");
            html.AppendLine($"                    <td class='metric'>{result.MeanTime:F2}</td>");
            html.AppendLine($"                    <td class='metric'>{result.StdDev:F2}</td>");
            html.AppendLine($"                    <td class='metric'>{FormatMemory(result.MemoryUsage)}</td>");
            html.AppendLine($"                    <td class='metric'>{result.Throughput:N0}</td>");
            html.AppendLine($"                    <td class='{statusClass}'>{statusText}</td>");
            html.AppendLine($"                    <td class='metric'>{result.Notes}</td>");
            html.AppendLine("                </tr>");
        }

        html.AppendLine("            </tbody>");
        html.AppendLine("        </table>");

        // Performance Charts Section
        html.AppendLine("        <div class='chart'>");
        html.AppendLine("            <h3>📈 Análise de Performance</h3>");
        html.AppendLine("            <h4>🏆 Top Performers (Throughput)</h4>");
        html.AppendLine("            <ul>");

        var topPerformers = _results.Where(r => r.Passed).OrderByDescending(r => r.Throughput).Take(3);
        foreach (var result in topPerformers)
        {
            html.AppendLine($"                <li><strong>{result.Method}</strong>: {result.Throughput:N0} ops/s</li>");
        }

        html.AppendLine("            </ul>");
        html.AppendLine("            <h4>⚡ Fastest Operations (Tempo Médio)</h4>");
        html.AppendLine("            <ul>");

        var fastest = _results.Where(r => r.Passed).OrderBy(r => r.MeanTime).Take(3);
        foreach (var result in fastest)
        {
            html.AppendLine($"                <li><strong>{result.Method}</strong>: {result.MeanTime:F2} μs</li>");
        }

        html.AppendLine("            </ul>");
        html.AppendLine("        </div>");

        html.AppendLine("        <div class='summary'>");
        html.AppendLine("            <h3>🔍 Legenda dos Campos</h3>");
        html.AppendLine("            <ul>");
        html.AppendLine("                <li><strong>Método:</strong> Nome/descritivo do cenário testado</li>");
        html.AppendLine("                <li><strong>Operações:</strong> Total de execuções realizadas</li>");
        html.AppendLine("                <li><strong>Tempo Médio:</strong> Tempo médio por operação (em microssegundos)</li>");
        html.AppendLine("                <li><strong>Desvio Padrão:</strong> Variação dos tempos de execução</li>");
        html.AppendLine("                <li><strong>Memória:</strong> Memória consumida/variada no teste</li>");
        html.AppendLine("                <li><strong>Throughput:</strong> Operações por segundo</li>");
        html.AppendLine("                <li><strong>Status:</strong> Resultado do teste (PASSOU/FALHOU)");
        html.AppendLine("                <li><strong>Observações:</strong> Explicação do objetivo ou contexto do teste</li>");
        html.AppendLine("            </ul>");
        html.AppendLine("        </div>");

        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        // Retry logic para lidar com arquivo em uso
        var maxRetries = 3;
        var retryDelay = 100; // ms

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                File.WriteAllText(htmlPath, html.ToString());
                Console.WriteLine($"📊 HTML Report gerado: {htmlPath}");
                break;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Console.WriteLine($"⚠️ Tentativa {attempt} falhou (arquivo em uso), tentando novamente em {retryDelay}ms...");
                Thread.Sleep(retryDelay);
                retryDelay *= 2; // Exponential backoff
            }
            catch (IOException ex) when (attempt == maxRetries)
            {
                Console.WriteLine($"❌ Falha ao salvar HTML após {maxRetries} tentativas: {ex.Message}");
                // Tenta salvar com nome alternativo
                var alternativeHtmlPath = Path.Combine(outputPath, $"BenchmarkResults_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                File.WriteAllText(alternativeHtmlPath, html.ToString());
                Console.WriteLine($"📊 HTML Report gerado com nome alternativo: {alternativeHtmlPath}");
                break;
            }
        }
    }

    private static void GenerateJsonReport(string outputPath)
    {
        var jsonPath = Path.Combine(outputPath, "BenchmarkResults.json");
        var data = new
        {
            Timestamp = DateTime.Now,
            Environment = new
            {
                OS = Environment.OSVersion.ToString(),
                Environment.ProcessorCount,
                Runtime = Environment.Version.ToString(),
                Environment.WorkingSet
            },
            Results = _results,
            Summary = new
            {
                TotalTests = _results.Count,
                Passed = _results.Count(r => r.Passed),
                Failed = _results.Count(r => !r.Passed),
                AverageThroughput = _results.Where(r => r.Passed).Any() ? _results.Where(r => r.Passed).Average(r => r.Throughput) : 0.0,
                AverageMemoryUsage = _results.Where(r => r.Passed).Any() ? _results.Where(r => r.Passed).Average(r => r.MemoryUsage) : 0.0
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(jsonPath, json);
        Console.WriteLine($"📋 JSON Report gerado: {jsonPath}");
    }

    private static void GenerateCsvReport(string outputPath)
    {
        var csvPath = Path.Combine(outputPath, "BenchmarkResults.csv");
        var csv = new StringBuilder();

        csv.AppendLine("Metodo,Operacoes,TempoMedio_μs,DesvioPadrao_μs,Memoria_bytes,Memoria_formatada,Throughput_ops_por_segundo,Passou,Observacoes");

        foreach (var result in _results)
        {
            csv.AppendLine($"{result.Method},{result.Operations},{result.MeanTime:F2},{result.StdDev:F2},{result.MemoryUsage},{FormatMemory(result.MemoryUsage)},{result.Throughput:F0},{result.Passed},{result.Notes}");
        }

        File.WriteAllText(csvPath, csv.ToString());
        Console.WriteLine($"📈 CSV Report gerado: {csvPath}");
    }

    public static void Clear()
    {
        _results.Clear();
    }

    private static string FormatMemory(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) // GB
        {
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
        else if (bytes >= 1024 * 1024) // MB
        {
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
        else if (bytes >= 1024) // KB
        {
            return $"{bytes / 1024.0:F1} KB";
        }
        else
        {
            return $"{bytes} B";
        }
    }
}
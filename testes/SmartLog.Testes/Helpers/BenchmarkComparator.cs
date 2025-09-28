using System.Text.Json;
using System.Text;

namespace SmartLog.Testes.Helpers;

/// <summary>
/// Helper para comparar resultados de benchmark ao longo do tempo
/// Útil para detectar regressões de performance
/// </summary>
public class BenchmarkComparator(string historyFilePath, double regressionThreshold = 20.0)
{
    public class BenchmarkHistory
    {
        public DateTime Date { get; set; }
        public string GitCommit { get; set; } = string.Empty;
        public List<BenchmarkResult> Results { get; set; } = [];
    }

    public class PerformanceRegression
    {
        public string TestMethod { get; set; } = string.Empty;
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double RegressionPercentage { get; set; }
        public string Metric { get; set; } = string.Empty;
    }

    private readonly string _historyFilePath = historyFilePath;
    private readonly double _regressionThreshold = regressionThreshold;

    /// <summary>
    /// Salva os resultados atuais no histórico
    /// </summary>
    public async Task SaveCurrentResults(List<BenchmarkResult> results, string gitCommit = "")
    {
        var history = await LoadHistory();
        
        var entry = new BenchmarkHistory
        {
            Date = DateTime.UtcNow,
            GitCommit = gitCommit,
            Results = results
        };

        history.Add(entry);

        // Manter apenas os últimos 30 registros
        if (history.Count > 30)
        {
            history.RemoveRange(0, history.Count - 30);
        }

        string json = JsonSerializer.Serialize(history, options: new JsonSerializerOptions
        {
            WriteIndented = true 
        });

        await File.WriteAllTextAsync(_historyFilePath, json);
    }

    /// <summary>
    /// Detecta regressões de performance comparando com a baseline
    /// </summary>
    public async Task<List<PerformanceRegression>> DetectRegressions(List<BenchmarkResult> currentResults)
    {
        var history = await LoadHistory();
        if (history.Count < 2) return [];

        var baseline = CalculateBaseline([.. history.TakeLast(5)]);
        var regressions = new List<PerformanceRegression>();

        foreach (var current in currentResults)
        {
            var baselineEntry = baseline.FirstOrDefault(b => b.Method == current.Method);
            if (baselineEntry == null) continue;

            // Verificar regressão no tempo médio (convertendo de microseconds para nanoseconds)
            var currentTimeNs = current.MeanTime * 1000;
            var baselineTimeNs = baselineEntry.MeanTime * 1000;
            
            if (currentTimeNs > 0 && baselineTimeNs > 0)
            {
                var regressionPct = (currentTimeNs - baselineTimeNs) / baselineTimeNs * 100;
                if (regressionPct > _regressionThreshold)
                {
                    regressions.Add(new PerformanceRegression
                    {
                        TestMethod = current.Method,
                        BaselineValue = baselineTimeNs,
                        CurrentValue = currentTimeNs,
                        RegressionPercentage = regressionPct,
                        Metric = "MeanTime"
                    });
                }
            }

            // Verificar regressão na memória
            if (current.MemoryUsage > 0 && baselineEntry.MemoryUsage > 0)
            {
                var regressionPct = (current.MemoryUsage - baselineEntry.MemoryUsage) / (double)baselineEntry.MemoryUsage * 100;
                if (regressionPct > _regressionThreshold)
                {
                    regressions.Add(new PerformanceRegression
                    {
                        TestMethod = current.Method,
                        BaselineValue = baselineEntry.MemoryUsage,
                        CurrentValue = current.MemoryUsage,
                        RegressionPercentage = regressionPct,
                        Metric = "Memory"
                    });
                }
            }
        }

        return regressions;
    }

    /// <summary>
    /// Gera relatório de tendências de performance
    /// </summary>
    public async Task<string> GenerateTrendReport()
    {
        var history = await LoadHistory();
        if (history.Count < 2) return "Histórico insuficiente para análise de tendências.";

        var report = new StringBuilder();
        report.AppendLine("# 📈 Relatório de Tendências de Performance");
        report.AppendLine();
        report.AppendLine($"**Período**: {history.First().Date:yyyy-MM-dd} até {history.Last().Date:yyyy-MM-dd}");
        report.AppendLine($"**Total de execuções**: {history.Count}");
        report.AppendLine();

        // Análise por método de teste
        var methodNames = history.SelectMany(h => h.Results.Select(r => r.Method)).Distinct();

        foreach (var method in methodNames)
        {
            report.AppendLine($"## 🧪 {method}");
            report.AppendLine();

            var methodHistory = history
                .Where(h => h.Results.Any(r => r.Method == method))
                .Select(h => new
                {
                    h.Date,
                    Result = h.Results.First(r => r.Method == method)
                })
                .OrderBy(x => x.Date)
                .ToList();

            if (methodHistory.Count >= 2)
            {
                var first = methodHistory.First().Result;
                var last = methodHistory.Last().Result;

                // Tendência de tempo (convertendo microseconds para nanoseconds)
                var firstTimeNs = first.MeanTime * 1000;
                var lastTimeNs = last.MeanTime * 1000;
                
                if (firstTimeNs > 0 && lastTimeNs > 0)
                {
                    var timeTrend = (lastTimeNs - firstTimeNs) / firstTimeNs * 100;
                    var trendIcon = timeTrend > 5 ? "📈" : timeTrend < -5 ? "📉" : "➡️";
                    report.AppendLine($"- **Tempo**: {trendIcon} {timeTrend:+0.0;-0.0;0.0}% ({firstTimeNs:N0}ns → {lastTimeNs:N0}ns)");
                }

                // Tendência de memória
                if (first.MemoryUsage > 0 && last.MemoryUsage > 0)
                {
                    var memoryTrend = (last.MemoryUsage - first.MemoryUsage) / (double)first.MemoryUsage * 100;
                    var trendIcon = memoryTrend > 5 ? "📈" : memoryTrend < -5 ? "📉" : "➡️";
                    report.AppendLine($"- **Memória**: {trendIcon} {memoryTrend:+0.0;-0.0;0.0}% ({FormatBytes(first.MemoryUsage)} → {FormatBytes(last.MemoryUsage)})");
                }
            }

            report.AppendLine();
        }

        return report.ToString();
    }

    private async Task<List<BenchmarkHistory>> LoadHistory()
    {
        if (!File.Exists(_historyFilePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_historyFilePath);
            return JsonSerializer.Deserialize<List<BenchmarkHistory>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<BenchmarkResult> CalculateBaseline(List<BenchmarkHistory> recentHistory)
    {
        var baseline = new List<BenchmarkResult>();
        var methodNames = recentHistory.SelectMany(h => h.Results.Select(r => r.Method)).Distinct();

        foreach (var method in methodNames)
        {
            var methodResults = recentHistory
                .SelectMany(h => h.Results.Where(r => r.Method == method))
                .ToList();

            if (methodResults.Count != 0)
            {
                baseline.Add(new BenchmarkResult
                {
                    Method = method,
                    MeanTime = methodResults.Average(r => r.MeanTime),
                    MemoryUsage = (long)methodResults.Average(r => r.MemoryUsage),
                    Operations = methodResults.First().Operations,
                    Throughput = methodResults.Average(r => r.Throughput)
                });
            }
        }

        return baseline;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:N1}{suffixes[counter]}";
    }
}

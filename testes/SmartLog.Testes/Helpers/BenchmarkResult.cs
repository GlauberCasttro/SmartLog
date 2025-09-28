namespace SmartLog.Testes.Helpers;

/// <summary>
/// Representa o resultado de um benchmark de desempenho.
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// Nome do método testado.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Número de operações realizadas no benchmark.
    /// </summary>
    public int Operations { get; set; }

    /// <summary>
    /// Tempo médio por operação (em microssegundos).
    /// </summary>
    public double MeanTime { get; set; } // microseconds

    /// <summary>
    /// Desvio padrão do tempo das operações.
    /// </summary>
    public double StdDev { get; set; } = 0.0;

    /// <summary>
    /// Uso de memória durante o benchmark (em bytes).
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Taxa de transferência (operações por segundo).
    /// </summary>
    public double Throughput { get; set; } // ops/sec

    /// <summary>
    /// Indica se o benchmark foi aprovado.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Observações adicionais sobre o benchmark.
    /// </summary>
    public string Notes { get; set; } = "";

    /// <summary>
    /// Data e hora em que o benchmark foi realizado.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

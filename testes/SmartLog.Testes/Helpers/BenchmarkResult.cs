namespace SmartLog.Testes.Helpers;

/// <summary>
/// Representa o resultado de um benchmark de desempenho.
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// Nome do m�todo testado.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// N�mero de opera��es realizadas no benchmark.
    /// </summary>
    public int Operations { get; set; }

    /// <summary>
    /// Tempo m�dio por opera��o (em microssegundos).
    /// </summary>
    public double MeanTime { get; set; } // microseconds

    /// <summary>
    /// Desvio padr�o do tempo das opera��es.
    /// </summary>
    public double StdDev { get; set; } = 0.0;

    /// <summary>
    /// Uso de mem�ria durante o benchmark (em bytes).
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Taxa de transfer�ncia (opera��es por segundo).
    /// </summary>
    public double Throughput { get; set; } // ops/sec

    /// <summary>
    /// Indica se o benchmark foi aprovado.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Observa��es adicionais sobre o benchmark.
    /// </summary>
    public string Notes { get; set; } = "";

    /// <summary>
    /// Data e hora em que o benchmark foi realizado.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

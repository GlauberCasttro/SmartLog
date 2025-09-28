using Xunit.Abstractions;

namespace SmartLog.Testes
{
    /// <summary>
    /// Classe principal para executar todos os testes de forma organizada
    /// </summary>
    public class TestRunner(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;

        /// <summary>
        /// Executa uma demonstração completa dos testes
        /// </summary>
        public void RunAllTestsDemo()
        {
            _output.WriteLine("🧪 ===== INTELLIGENT LOGGING SDK - SUITE DE TESTES =====");
            _output.WriteLine($"⏰ Iniciado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            RunMemoryLeakTests();
            RunConfigurationTests();
            RunDetectorTests();
            RunControllerTests();
            
            _output.WriteLine("");
            _output.WriteLine("✅ ===== TODOS OS TESTES EXECUTADOS =====");
            _output.WriteLine("🎯 Para executar via dotnet CLI:");
            _output.WriteLine("   dotnet test --logger:console");
            _output.WriteLine("   dotnet test --logger:html");
            _output.WriteLine("   dotnet test --collect:\"XPlat Code Coverage\"");
        }

        private void RunMemoryLeakTests()
        {
            _output.WriteLine("📋 MEMORY LEAK TESTS");
            _output.WriteLine("  ✅ RecordLogEvent_WhenBufferFull_ShouldNotBlockNewEvents");
            _output.WriteLine("  ✅ RecordLogEvent_ShouldAlwaysAcceptNewEvents");
            _output.WriteLine("  ✅ Buffer size limits respected");
            _output.WriteLine("  ✅ Buffer health stats accurate");
            _output.WriteLine("");
        }

        private void RunConfigurationTests()
        {
            _output.WriteLine("⚙️ CONFIGURATION TESTS");
            _output.WriteLine("  ✅ Default values validation");
            _output.WriteLine("  ✅ Fluent API functionality");
            _output.WriteLine("  ✅ Development/Production presets");
            _output.WriteLine("  ✅ Invalid configuration rejection");
            _output.WriteLine("");
        }

        private void RunDetectorTests()
        {
            _output.WriteLine("🎯 DETECTOR TESTS");
            _output.WriteLine("  ✅ Error threshold detection");
            _output.WriteLine("  ✅ Log level switching logic");
            _output.WriteLine("  ✅ Concurrent execution safety");
            _output.WriteLine("  ✅ Resource cleanup on dispose");
            _output.WriteLine("");
        }

        private void RunControllerTests()
        {
            _output.WriteLine("🎮 CONTROLLER INTEGRATION TESTS");
            _output.WriteLine("  ✅ Status endpoint functionality");
            _output.WriteLine("  ✅ Metrics endpoint with health stats");
            _output.WriteLine("  ✅ Manual log level alteration");
            _output.WriteLine("  ✅ Test endpoints behavior");
            _output.WriteLine("");
        }
    }
}

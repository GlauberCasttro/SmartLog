using System.Text;

namespace SmartLog.Core.Helpers;
    internal static class GlobalConfig
    {
        /// <summary>
        /// Nome do canal Redis utilizado para publicar eventos de mudança de nível de log.
        /// </summary>
        private const string Channel = "smartlog:level_change_channel";

        /// <summary>
        /// Chave no Redis que armazena o timestamp da última troca de nível
        /// </summary>
        private const string RedisSwitchTimestampKey = "smartlog:last_switch_timestamp";

        /// <summary>
        /// Config do Channel
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        public static string GetRedisKeyChannel(string appName) => $"{appName}:{Channel}";

        /// <summary>
        /// Config do timeStamp
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        public static string GetRedisSwitchTimestampKey(string appName) => $"{appName}:{RedisSwitchTimestampKey}";

        /// <summary>
        /// Constrói uma string de conexão Redis otimizada para AWS ElastiCache em Kubernetes.
        /// Inspirado nas melhores práticas enterprise (Localiza BuildingBlocks).
        /// </summary>
        /// <param name="allowAdmin">Permite comandos administrativos no Redis</param>
        /// <param name="abortConnect">Define se deve abortar em caso de falha na conexão</param>
        /// <param name="connectTimeout">Timeout de conexão em segundos (otimizado para AWS)</param>
        /// <returns>String de conexão formatada para o Redis</returns>
        public static string BuildRedisConnectionStringFromEnvironment(bool allowAdmin = false, bool abortConnect = false, int connectTimeout = 15)
        {
            // Obtenção com fallbacks seguros (apenas variáveis essenciais)
            var host = GetEnvironmentVariableOrDefault("REDIS_HOST", "localhost");
            var portString = GetEnvironmentVariableOrDefault("REDIS_PORT", "6379");
            var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
            var useSslString = GetEnvironmentVariableOrDefault("REDIS_USE_SSL", "false");

            // Parse com fallbacks seguros
            var port = int.TryParse(portString, out var parsedPort) ? parsedPort : 6379;
            var useSsl = bool.TryParse(useSslString, out var parsedSsl) && parsedSsl;

            // Construção otimizada para AWS ElastiCache
            var connectionStringBuilder = new StringBuilder($"{host}:{port}");

            // Configurações administrativas (raramente necessário no ElastiCache)
            if (allowAdmin)
                connectionStringBuilder.Append(",allowAdmin=true");

            // Autenticação segura
            if (!string.IsNullOrWhiteSpace(password))
                connectionStringBuilder.Append($",password={password}");

            // CONFIGURAÇÕES HARDCODED OTIMIZADAS PARA AWS/KUBERNETES
            connectionStringBuilder.Append($",abortConnect={abortConnect.ToString().ToLowerInvariant()}");
            connectionStringBuilder.Append($",connectTimeout={connectTimeout * 1000}"); // 15s em ms
            connectionStringBuilder.Append(",syncTimeout=10000");      // 10s hardcoded (otimizado)
            connectionStringBuilder.Append(",asyncTimeout=10000");     // 10s hardcoded (otimizado)
            connectionStringBuilder.Append(",connectRetry=3");         // 3 retries hardcoded (estabilidade)
            connectionStringBuilder.Append(",keepAlive=30");           // 30s hardcoded (AWS load balancer)

            // SSL para segurança
            if (useSsl)
            {
                connectionStringBuilder.Append(",ssl=true");
                connectionStringBuilder.Append(",checkCertificateRevocation=false"); // Hardcoded AWS
            }

            return connectionStringBuilder.ToString();
        }

        /// <summary>
        /// Obtém variável de ambiente com fallback (não obrigatória).
        /// </summary>
        /// <param name="variableName">Nome da variável de ambiente</param>
        /// <param name="defaultValue">Valor padrão se não encontrar</param>
        /// <returns>Valor da variável ou valor padrão</returns>
        private static string GetEnvironmentVariableOrDefault(string variableName, string defaultValue) => Environment.GetEnvironmentVariable(variableName) ?? defaultValue;
    }
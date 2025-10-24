using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace SmartLog.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TesteErroController() : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(int quantidadeErro = 10)
        {
            for (int i = 0; i < quantidadeErro; i++)
            {
                Log.Error("Teste de erro {Numero}", i + 1);
            }

            return Ok();
        }

        [HttpGet("info")]
        public IActionResult GetInfo()
        {
            Log.Information("Teste de informa��o sem o force: {Numero}", 1);
            ////Se estiver configurado o ForceLoggingInterceptor, apenas esse log ser� registrado mesmo com o n�vel de verbosidade baixo
            //Log.Information("Teste de informa��o com o force caso {Numero}, Data: {date}, fluxo: {fluxo} Force: {forcehere}", 15258, DateTime.UtcNow, "ocorrencias", true);

            //Log.Warning("Teste de warning com o force caso {Numero},  Data: {date}, fluxo: {fluxo}", 15258, DateTime.UtcNow, "ocorrencias");

            return Ok();
        }
    }
}
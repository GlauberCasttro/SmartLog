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
    }
}
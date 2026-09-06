using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Registro.Functions;

public class RegistrarDocumento
{
    private readonly ILogger<RegistrarDocumento> _logger;

    public RegistrarDocumento(ILogger<RegistrarDocumento> logger)
    {
        _logger = logger;
    }

    [Function("RegistrarDocumento")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}

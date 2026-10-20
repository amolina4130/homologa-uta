using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Documentos.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    /// <summary>Indica si el servicio está en línea</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { estado = "EN_LINEA", servicio = "Documentos" });
}
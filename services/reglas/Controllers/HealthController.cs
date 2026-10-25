using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Reglas.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    /// <summary>Indica si el servicio está en línea</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { estado = "EN_LINEA", servicio = "Reglas" });
}

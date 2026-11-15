using System.Security.Claims;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Descarga del informe de homologación en PDF (HU-18).</summary>
[ApiController]
[Route("homologacion")]
public class InformeController : ControllerBase
{
    private readonly IInformeService _informe;

    public InformeController(IInformeService informe)
    {
        _informe = informe;
    }

    /// <summary>Genera y devuelve el informe de la solicitud en PDF</summary>
    /// <remarks>El estudiante solo puede descargar sus propias solicitudes; el Coordinador, cualquiera.</remarks>
    /// <response code="200">PDF del informe</response>
    /// <response code="403">La solicitud es de otro estudiante</response>
    /// <response code="404">La solicitud o el informe no existen</response>
    [HttpGet("{id:int}/informe")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Descargar(int id, CancellationToken ct)
    {
        var rol = User.FindFirstValue("rol");
        var esCoordinador = rol is "COORDINADOR" or "ADMINISTRADOR";

        try
        {
            var pdf = await _informe.GenerarPdfAsync(id, Correo(), esCoordinador, ct);
            return File(pdf, "application/pdf", $"Informe_SOL-{id:D4}.pdf");
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
    }

    private string? Correo() =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
}

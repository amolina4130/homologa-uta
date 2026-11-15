using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Solicitudes.DTOs;

public class SolicitudRequest
{
    /// <summary>INTERNO o EXTERNO. Si no se envía, se asume INTERNO.</summary>
    public string Modulo { get; set; } = "INTERNO";

    public int CarreraOrigenId { get; set; }
    public int CarreraDestinoId { get; set; }

    /// <summary>Obligatoria en el módulo externo (HU-15).</summary>
    [MaxLength(200)]
    public string? UniversidadOrigen { get; set; }

    /// <summary>Obligatoria en el módulo externo (HU-15).</summary>
    [MaxLength(200)]
    public string? UniversidadDestino { get; set; }

    public List<int> AsignaturasIds { get; set; } = new();
}

public class SolicitudAsignaturaDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public int Horas { get; set; }
}

public class SolicitudDetalleDto
{
    public int Id { get; set; }
    public string NumeroProceso { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Modulo { get; set; } = "INTERNO";
    public int CarreraOrigenId { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public int CarreraDestinoId { get; set; }
    public string CarreraDestino { get; set; } = string.Empty;
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }
    public string EstudianteCorreo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Editable { get; set; }
    public List<SolicitudAsignaturaDto> Asignaturas { get; set; } = new();
}

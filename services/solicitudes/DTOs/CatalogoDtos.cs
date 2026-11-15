using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Solicitudes.DTOs;

public class PlanAnaliticoDto
{
    public int AsignaturaId { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public string ResultadosAprendizaje { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
}

public class CargaPlanResponse
{
    public int Cargados { get; set; }
    public List<PlanOmitidoDto> Omitidos { get; set; } = new();
}

public class PlanOmitidoDto
{
    public string Archivo { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

public class EquivalenciaRequest
{
    [Range(1, int.MaxValue)]
    public int CarreraOrigenId { get; set; }

    [Range(1, int.MaxValue)]
    public int AsignaturaOrigenId { get; set; }

    [Range(1, int.MaxValue)]
    public int CarreraDestinoId { get; set; }

    [Range(1, int.MaxValue)]
    public int AsignaturaDestinoId { get; set; }

    public bool Activa { get; set; } = true;
}

public class EquivalenciaDto
{
    public int Id { get; set; }
    public int CarreraOrigenId { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public int AsignaturaOrigenId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public int CarreraDestinoId { get; set; }
    public string CarreraDestino { get; set; } = string.Empty;
    public int AsignaturaDestinoId { get; set; }
    public string AsignaturaDestino { get; set; } = string.Empty;
    public bool Activa { get; set; }
}

public class MemoriaCargaResponse
{
    public int Cargadas { get; set; }
    public bool SinCambios { get; set; }
    public string Huella { get; set; } = string.Empty;
}

public class MemoriaEstadoDto
{
    public string Huella { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
    public int TotalCarreras { get; set; }
    public int TotalAsignaturas { get; set; }
}

public class MemoriaAsignaturaDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Nivel { get; set; }
    public string Area { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public int Horas { get; set; }
    public string ContenidosMinimos { get; set; } = string.Empty;
}

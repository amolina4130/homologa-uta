namespace HomologaUTA.Catalogo.Models.Memoria;

// Memoria del agente (TA-19.1). Los ids de Carrera y Asignatura son los mismos de las mallas
// (Solicitudes) y del plan analítico, por eso no son identity.

public class CarreraMemoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public List<NivelMemoria> Niveles { get; set; } = new();
    public List<AsignaturaMemoria> Asignaturas { get; set; } = new();
}

public class NivelMemoria
{
    public int Id { get; set; }

    public int CarreraId { get; set; }
    public CarreraMemoria Carrera { get; set; } = null!;

    /// <summary>Orden del nivel dentro de la malla (Primero = 1, Segundo = 2, ...).</summary>
    public int Numero { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public List<AsignaturaMemoria> Asignaturas { get; set; } = new();
}

public class AsignaturaMemoria
{
    public int Id { get; set; }

    public int CarreraId { get; set; }
    public CarreraMemoria Carrera { get; set; } = null!;

    public int NivelId { get; set; }
    public NivelMemoria Nivel { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Área de conocimiento (Física, Programación, Matemáticas...) para la preselección de HU-21.</summary>
    public string Area { get; set; } = "General";

    public int Creditos { get; set; }
    public int Horas { get; set; }

    public List<ContenidoMinimo> ContenidosMinimos { get; set; } = new();
}

public class ContenidoMinimo
{
    public int Id { get; set; }

    public int AsignaturaId { get; set; }
    public AsignaturaMemoria Asignatura { get; set; } = null!;

    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

/// <summary>Registro de la última carga: huella de los datos de origen y fecha.</summary>
public class CargaMemoria
{
    public int Id { get; set; }

    /// <summary>SHA-256 (hex) de los datos de origen.</summary>
    public string Huella { get; set; } = string.Empty;

    public DateTime FechaCarga { get; set; }
    public int TotalCarreras { get; set; }
    public int TotalAsignaturas { get; set; }
}

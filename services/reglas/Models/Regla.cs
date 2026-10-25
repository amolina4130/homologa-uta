namespace HomologaUTA.Reglas.Models;

/// <summary>Regla de negocio normativa (RN-01 a RN-11) del catálogo configurable.</summary>
public class Regla
{
    public int Id { get; set; }

    /// <summary>Código único de la regla, por ejemplo "RN-01".</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Normativa de origen, por ejemplo "Instructivo, lit. B y C".</summary>
    public string Fuente { get; set; } = string.Empty;

    /// <summary>Valor numérico configurable (años, porcentaje, matrícula, días). Nulo si la regla no tiene umbral.</summary>
    public decimal? Umbral { get; set; }

    /// <summary>true = regla activa; false = inactiva.</summary>
    public bool Activa { get; set; } = true;
}

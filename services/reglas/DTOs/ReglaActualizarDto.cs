using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Reglas.DTOs;

/// <summary>Datos que el Administrador puede modificar de una regla.</summary>
public class ReglaActualizarDto
{
    /// <summary>Nuevo umbral. Si se omite, se conserva el actual.</summary>
    public decimal? Umbral { get; set; }

    /// <summary>true = activa, false = inactiva. Obligatorio.</summary>
    [Required(ErrorMessage = "El estado (activa) es obligatorio.")]
    public bool? Activa { get; set; }
}

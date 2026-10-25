namespace HomologaUTA.Reglas.DTOs;

public class ReglaDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Fuente { get; set; } = string.Empty;
    public decimal? Umbral { get; set; }
    public bool Activa { get; set; }
}

namespace HomologaUTA.Homologacion.Services;

public class PreseleccionOptions
{
    public const string Seccion = "Preseleccion";

    public int MaxCandidatas { get; set; } = 3;
    public int MargenNivel { get; set; } = 1;

    // Área -> palabras clave (se configura en appsettings.json)
    public Dictionary<string, string[]> Areas { get; set; } = new();
}
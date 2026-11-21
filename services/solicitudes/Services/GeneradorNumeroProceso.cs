namespace HomologaUTA.Solicitudes.Services;

/// <summary>Arma el número de proceso a partir del Id: 4 → SOL-0004.</summary>
public static class GeneradorNumeroProceso
{
    public static string Formatear(int id) => $"SOL-{id:D4}";
}

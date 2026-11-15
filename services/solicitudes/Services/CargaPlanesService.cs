using System.Text;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace HomologaUTA.Solicitudes.Services;

/// <summary>
/// Carga de planes analíticos desde PDF (HU-13). Extrae el texto con PdfPig y lo asocia a una
/// asignatura por el prefijo numérico del archivo o por el nombre de la asignatura dentro del texto.
/// Un PDF que no se puede asociar se reporta y no detiene la carga.
/// </summary>
public class CargaPlanesService
{
    private readonly SolicitudesDbContext _db;

    public CargaPlanesService(SolicitudesDbContext db)
    {
        _db = db;
    }

    public async Task<CargaPlanResponse> CargarAsync(IEnumerable<IFormFile> archivos, CancellationToken ct = default)
    {
        var respuesta = new CargaPlanResponse();
        var asignaturas = await _db.Asignaturas.ToListAsync(ct);

        foreach (var archivo in archivos)
        {
            if (archivo is null || archivo.Length == 0)
                continue;

            if (!archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                respuesta.Omitidos.Add(new PlanOmitidoDto { Archivo = archivo.FileName, Motivo = "El archivo no es un PDF." });
                continue;
            }

            string texto;
            try
            {
                await using var stream = archivo.OpenReadStream();
                texto = ExtraerTexto(stream);
            }
            catch (Exception ex)
            {
                respuesta.Omitidos.Add(new PlanOmitidoDto { Archivo = archivo.FileName, Motivo = $"No se pudo leer el PDF: {ex.Message}" });
                continue;
            }

            if (string.IsNullOrWhiteSpace(texto))
            {
                respuesta.Omitidos.Add(new PlanOmitidoDto { Archivo = archivo.FileName, Motivo = "El PDF no contiene texto (puede ser una imagen escaneada)." });
                continue;
            }

            var asignatura = Asociar(archivo.FileName, texto, asignaturas);
            if (asignatura is null)
            {
                respuesta.Omitidos.Add(new PlanOmitidoDto { Archivo = archivo.FileName, Motivo = "No se pudo asociar el PDF a una asignatura del catálogo." });
                continue;
            }

            var plan = await _db.PlanesAnaliticos.FirstOrDefaultAsync(p => p.AsignaturaId == asignatura.Id, ct);
            if (plan is null)
            {
                plan = new PlanAnalitico { AsignaturaId = asignatura.Id };
                _db.PlanesAnaliticos.Add(plan);
            }

            plan.Contenido = texto.Trim();
            plan.ResultadosAprendizaje = ExtraerResultados(texto);
            plan.FechaCarga = DateTime.UtcNow;
            respuesta.Cargados++;
        }

        await _db.SaveChangesAsync(ct);
        return respuesta;
    }

    public static string ExtraerTexto(Stream stream)
    {
        var sb = new StringBuilder();
        using var documento = PdfDocument.Open(stream);
        foreach (var pagina in documento.GetPages())
            sb.AppendLine(pagina.Text);
        return sb.ToString();
    }

    private static Asignatura? Asociar(string nombreArchivo, string texto, List<Asignatura> asignaturas)
    {
        // 1. Prefijo numérico: "12_plan.pdf" se asocia a la asignatura 12
        var soloNombre = Path.GetFileNameWithoutExtension(nombreArchivo);
        var prefijo = new string(soloNombre.TakeWhile(char.IsDigit).ToArray());
        if (int.TryParse(prefijo, out var id))
        {
            var porId = asignaturas.FirstOrDefault(a => a.Id == id);
            if (porId is not null) return porId;
        }

        // 2. Nombre de la asignatura dentro del texto (el más largo primero)
        var textoNormalizado = AreaAsignatura.Normalizar(texto);
        return asignaturas
            .OrderByDescending(a => a.Nombre.Length)
            .FirstOrDefault(a => textoNormalizado.Contains(
                AreaAsignatura.Normalizar(a.Nombre), StringComparison.Ordinal));
    }

    private static string ExtraerResultados(string texto)
    {
        var indice = texto.IndexOf("resultado", StringComparison.OrdinalIgnoreCase);
        if (indice >= 0)
        {
            var fin = Math.Min(texto.Length, indice + 800);
            return texto[indice..fin].Trim();
        }
        return texto.Length <= 600 ? texto.Trim() : texto[..600].Trim();
    }
}

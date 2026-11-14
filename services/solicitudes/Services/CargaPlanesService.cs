using System.Globalization;
using System.Text;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace HomologaUTA.Solicitudes.Services;

/// <summary>
/// Carga masiva de planes analíticos desde PDF (HU-13). El nombre del archivo (o el texto
/// del PDF) debe contener el nombre de una asignatura del catálogo.
/// </summary>
public class CargaPlanesService
{
    private readonly SolicitudesDbContext _db;

    public CargaPlanesService(SolicitudesDbContext db)
    {
        _db = db;
    }

    public async Task<ResultadoCargaPlanes> CargarAsync(IEnumerable<IFormFile> archivos, CancellationToken ct = default)
    {
        var resultado = new ResultadoCargaPlanes();
        var asignaturas = await _db.Asignaturas.AsNoTracking().ToListAsync(ct);
        var conPlan = (await _db.PlanesAnaliticos.Select(p => p.AsignaturaId).ToListAsync(ct)).ToHashSet();

        foreach (var archivo in archivos)
        {
            if (archivo is null || archivo.Length == 0)
            {
                resultado.Omitidos.Add(new ArchivoOmitido(archivo?.FileName ?? "(vacío)", "El archivo está vacío."));
                continue;
            }

            if (!Path.GetExtension(archivo.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                resultado.Omitidos.Add(new ArchivoOmitido(archivo.FileName, "Solo se aceptan archivos PDF."));
                continue;
            }

            string texto;
            try
            {
                using var memoria = new MemoryStream();
                await archivo.CopyToAsync(memoria, ct);
                memoria.Position = 0;

                using var pdf = PdfDocument.Open(memoria);
                texto = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            }
            catch (Exception ex)
            {
                resultado.Omitidos.Add(new ArchivoOmitido(archivo.FileName, $"No se pudo leer el PDF: {ex.Message}"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(texto))
            {
                resultado.Omitidos.Add(new ArchivoOmitido(archivo.FileName,
                    "El PDF no contiene texto (puede ser una imagen escaneada)."));
                continue;
            }

            var asignatura = CoincidirAsignatura(archivo.FileName, texto, asignaturas);
            if (asignatura is null)
            {
                resultado.Omitidos.Add(new ArchivoOmitido(archivo.FileName,
                    "El PDF no corresponde a ninguna asignatura del catálogo."));
                continue;
            }

            if (conPlan.Contains(asignatura.Id))
            {
                resultado.Omitidos.Add(new ArchivoOmitido(archivo.FileName,
                    $"La asignatura '{asignatura.Nombre}' ya tiene un plan analítico vigente."));
                continue;
            }

            _db.PlanesAnaliticos.Add(new PlanAnalitico
            {
                AsignaturaId = asignatura.Id,
                Contenido = Recortar(texto.Trim(), 20000),
                ResultadosAprendizaje = ExtraerResultados(texto),
                FechaCarga = DateTime.UtcNow
            });
            conPlan.Add(asignatura.Id);
            resultado.Cargados++;
        }

        if (resultado.Cargados > 0)
            await _db.SaveChangesAsync(ct);

        return resultado;
    }

    /// <summary>Busca la asignatura por nombre en el archivo o en las primeras líneas del texto.</summary>
    private static Asignatura? CoincidirAsignatura(
        string nombreArchivo, string texto, IReadOnlyList<Asignatura> asignaturas)
    {
        var archivo = Normalizar(Path.GetFileNameWithoutExtension(nombreArchivo));
        var inicioTexto = Normalizar(Recortar(texto, 4000));

        // 1. Coincidencia exacta con el nombre del archivo
        var porArchivo = asignaturas
            .Where(a => archivo.Contains(Normalizar(a.Nombre), StringComparison.Ordinal))
            .OrderByDescending(a => a.Nombre.Length)
            .FirstOrDefault();
        if (porArchivo is not null)
            return porArchivo;

        // 2. Coincidencia con el contenido del PDF
        return asignaturas
            .Where(a => inicioTexto.Contains(Normalizar(a.Nombre), StringComparison.Ordinal))
            .OrderByDescending(a => a.Nombre.Length)
            .FirstOrDefault();
    }

    private static string ExtraerResultados(string texto)
    {
        var indice = texto.IndexOf("resultado", StringComparison.OrdinalIgnoreCase);
        if (indice < 0)
            indice = texto.IndexOf("aprendizaje", StringComparison.OrdinalIgnoreCase);

        return indice < 0
            ? string.Empty
            : Recortar(texto[indice..].Trim(), 4000);
    }

    private static string Normalizar(string valor)
    {
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Trim();
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}

public class ResultadoCargaPlanes
{
    public int Cargados { get; set; }
    public List<ArchivoOmitido> Omitidos { get; set; } = new();
}

public record ArchivoOmitido(string Archivo, string Motivo);

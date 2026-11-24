using System.Globalization;
using System.Text;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace HomologaUTA.Solicitudes.Services;

/// <summary>
/// Carga masiva de contenido académico desde PDF (planes analíticos y sílabos). El nombre
/// del archivo (o el texto del PDF) debe contener el nombre de una asignatura del catálogo.
/// Los planes de demostración se reemplazan automáticamente; los planes reales solo se
/// sobrescriben cuando se envía <c>reemplazar=true</c>. Los sílabos se anexan al contenido
/// de la asignatura para que el agente interno los vea junto al plan.
/// </summary>
public class CargaPlanesService
{
    private const int MaxContenido = 20000;
    private const string PrefijoDemo = "Plan analítico de ";

    private readonly SolicitudesDbContext _db;

    public CargaPlanesService(SolicitudesDbContext db)
    {
        _db = db;
    }

    public async Task<ResultadoCargaPlanes> CargarAsync(
        IEnumerable<IFormFile> archivos,
        bool reemplazar = false,
        int? carreraId = null,
        CancellationToken ct = default)
    {
        var resultado = new ResultadoCargaPlanes();
        var consulta = _db.Asignaturas.AsNoTracking();
        if (carreraId is > 0)
            consulta = consulta.Where(a => a.CarreraId == carreraId.Value);

        var asignaturas = await consulta.ToListAsync(ct);
        var planes = await _db.PlanesAnaliticos.ToDictionaryAsync(p => p.AsignaturaId, ct);
        var pendientes = new Dictionary<int, Acumulado>();

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

            var esSilabo = EsSilabo(archivo.FileName);
            var existe = planes.TryGetValue(asignatura.Id, out var planExistente);
            var reemplazaDemo = existe && EsDemo(planExistente!.Contenido);

            if (existe && !reemplazaDemo && !reemplazar && !esSilabo)
            {
                resultado.Omitidos.Add(new ArchivoOmitido(archivo.FileName,
                    $"La asignatura '{asignatura.Nombre}' ya tiene un plan real. Envíe reemplazar=true para sobrescribirlo."));
                continue;
            }

            if (!pendientes.TryGetValue(asignatura.Id, out var acumulado))
            {
                acumulado = new Acumulado
                {
                    Plan = existe ? planExistente : null,
                    Contenido = existe && !reemplazaDemo && !reemplazar
                        ? planExistente!.Contenido
                        : string.Empty
                };
                pendientes[asignatura.Id] = acumulado;
            }

            var nombreArchivo = Path.GetFileName(archivo.FileName);
            var bloque = esSilabo
                ? $"--- SÍLABO ({nombreArchivo}) ---\n{Recortar(texto.Trim(), MaxContenido)}"
                : $"--- PLAN ANALÍTICO ({nombreArchivo}) ---\n{Recortar(texto.Trim(), MaxContenido)}";

            if (!acumulado.Contenido.Contains(bloque, StringComparison.Ordinal))
                acumulado.Contenido = Recortar(
                    acumulado.Contenido.Length == 0
                        ? bloque
                        : $"{acumulado.Contenido}\n\n{bloque}",
                    MaxContenido);

            if (esSilabo)
                acumulado.Silabos++;
            else
                acumulado.Planes++;

            if (reemplazaDemo || reemplazar)
                acumulado.Reemplazado = true;
        }

        foreach (var (asignaturaId, acumulado) in pendientes)
        {
            var plan = acumulado.Plan;
            if (plan is null)
            {
                plan = new PlanAnalitico { AsignaturaId = asignaturaId };
                _db.PlanesAnaliticos.Add(plan);
            }

            plan.Contenido = acumulado.Contenido.Trim();
            plan.ResultadosAprendizaje = Recortar(ExtraerResultados(plan.Contenido), 4000);
            plan.FechaCarga = DateTime.UtcNow;
            planes[asignaturaId] = plan;
        }

        resultado.Cargados = pendientes.Count;
        resultado.Planes = pendientes.Values.Sum(v => v.Planes);
        resultado.Silabos = pendientes.Values.Sum(v => v.Silabos);
        resultado.Reemplazados = pendientes.Values.Count(v => v.Reemplazado);

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

    private static bool EsSilabo(string nombreArchivo)
    {
        var nombre = Normalizar(Path.GetFileNameWithoutExtension(nombreArchivo));
        return nombre.Contains("silabo") || nombre.Contains("syllabus");
    }

    private static bool EsDemo(string? contenido) =>
        contenido?.StartsWith(PrefijoDemo, StringComparison.OrdinalIgnoreCase) == true;

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

    private sealed class Acumulado
    {
        public PlanAnalitico? Plan { get; init; }
        public string Contenido { get; set; } = string.Empty;
        public int Planes { get; set; }
        public int Silabos { get; set; }
        public bool Reemplazado { get; set; }
    }
}

public class ResultadoCargaPlanes
{
    public int Cargados { get; set; }
    public int Planes { get; set; }
    public int Silabos { get; set; }
    public int Reemplazados { get; set; }
    public List<ArchivoOmitido> Omitidos { get; set; } = new();
}

public record ArchivoOmitido(string Archivo, string Motivo);

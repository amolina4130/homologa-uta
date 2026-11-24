using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomologaUTA.Documentos.Data;
using HomologaUTA.Documentos.DTOs;
using HomologaUTA.Documentos.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomologaUTA.Documentos.Services;

/// <summary>
/// Procesa todos los PDF de una carpeta indicada por el usuario, sin subirlos uno por uno.
/// </summary>
public class CarpetaService
{
    private const string RaizPorDefecto = "/app/documentos";
    private const int MaxArchivosPorDefecto = 50;
    private const string UsuarioPorDefecto = "comite@uta.edu.ec";
    private const int LongitudTextoRespuesta = 120;

    private static readonly Regex PrefijoUnidadWindows = new(@"^[A-Za-z]:/*", RegexOptions.Compiled);

    private readonly DocumentosDbContext _db;
    private readonly ExtractorPdfService _extractor;
    private readonly EventPublisherService _publisher;
    private readonly IConfiguration _config;
    private readonly ILogger<CarpetaService> _logger;
    private readonly IWebHostEnvironment _env;

    public CarpetaService(
        DocumentosDbContext db,
        ExtractorPdfService extractor,
        EventPublisherService publisher,
        IConfiguration config,
        ILogger<CarpetaService> logger,
        IWebHostEnvironment env)
    {
        _db = db;
        _extractor = extractor;
        _publisher = publisher;
        _config = config;
        _logger = logger;
        _env = env;
    }

    public async Task<ResultadoCarpetaDto> ProcesarAsync(CarpetaRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Ruta))
            throw new InvalidOperationException("Debe indicar la ruta de la carpeta de documentos.");

        var raiz = ResolverRaiz();
        var carpeta = ResolverCarpeta(request.Ruta, raiz);
        var archivos = ObtenerPdfs(carpeta);

        var carpetaDestino = Path.Combine(_env.ContentRootPath, "pdfs");
        Directory.CreateDirectory(carpetaDestino);

        var resultado = new ResultadoCarpetaDto();

        _logger.LogInformation(
            "Procesando carpeta {Carpeta} ({Cantidad} PDF) para la solicitud {SolicitudId}.",
            carpeta, archivos.Count, request.SolicitudId);

        foreach (var archivo in archivos)
        {
            ct.ThrowIfCancellationRequested();

            var tipo = Clasificar(Path.GetFileName(archivo));
            var nombreArchivo = $"{request.SolicitudId}_CARPETA_{tipo}_{Guid.NewGuid()}.pdf";
            var rutaDestino = Path.Combine(carpetaDestino, nombreArchivo);

            await using (var origen = File.OpenRead(archivo))
            await using (var destino = new FileStream(rutaDestino, FileMode.CreateNew, FileAccess.Write))
            {
                await origen.CopyToAsync(destino, ct);
            }

            var textoExtraido = _extractor.ExtraerTexto(rutaDestino) ?? string.Empty;

            var documento = new Documento
            {
                SolicitudId = request.SolicitudId,
                TipoDocumento = tipo,
                NombreArchivo = nombreArchivo,
                TextoExtraido = textoExtraido,
                Estado = "PROCESADO",
                FechaSubida = DateTime.UtcNow,
                EstudianteId = request.Usuario ?? UsuarioPorDefecto
            };

            _db.Documentos.Add(documento);
            await _db.SaveChangesAsync(ct);

            await _publisher.PublicarDocumentoProcesadoAsync(
                documento.Id,
                documento.SolicitudId,
                documento.TextoExtraido);

            resultado.Documentos.Add(new DocumentoCarpetaDto
            {
                Id = documento.Id,
                NombreArchivo = nombreArchivo,
                TipoDocumento = tipo,
                TextoExtraido = textoExtraido[..Math.Min(LongitudTextoRespuesta, textoExtraido.Length)]
            });

            _logger.LogInformation(
                "Documento {DocumentoId} ({Tipo}) procesado desde {Archivo}.",
                documento.Id, tipo, archivo);
        }

        resultado.Total = resultado.Documentos.Count;
        return resultado;
    }

    /// <summary>
    /// Registra la URL de la carpeta que el titular compartió con el Coordinador. No se descarga
    /// contenido arbitrario: la URL queda asociada a la solicitud como referencia del agente externo.
    /// </summary>
    public async Task<DocumentoCarpetaDto> RegistrarUrlExternaAsync(
        UrlExternaRequest request, CancellationToken ct)
    {
        if (request is null || request.SolicitudId <= 0)
            throw new InvalidOperationException("Debe indicar una solicitud válida.");

        var url = (request.Url ?? string.Empty).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "La URL de la carpeta debe ser válida y comenzar con http:// o https://.");
        }

        var documento = new Documento
        {
            SolicitudId = request.SolicitudId,
            TipoDocumento = "URL_EXTERNA",
            NombreArchivo = url,
            TextoExtraido = $"Carpeta externa registrada: {url}",
            Estado = "REGISTRADO",
            FechaSubida = DateTime.UtcNow,
            EstudianteId = request.Usuario ?? UsuarioPorDefecto
        };

        _db.Documentos.Add(documento);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "URL externa registrada para la solicitud {SolicitudId}: {Url}",
            request.SolicitudId, url);

        return new DocumentoCarpetaDto
        {
            Id = documento.Id,
            NombreArchivo = url,
            TipoDocumento = "URL_EXTERNA",
            TextoExtraido = documento.TextoExtraido
        };
    }

    /// <summary>Clasifica un PDF por su nombre, ignorando mayúsculas y tildes.</summary>
    public static string Clasificar(string nombreArchivo)
    {
        var nombre = QuitarTildes(nombreArchivo ?? string.Empty);

        if (Contiene(nombre, "malla"))
            return "MALLA";

        // "récord" y "silabo" se comparan sin tildes, por eso también se aceptan "record"/"silabo".
        if (Contiene(nombre, "record") || Contiene(nombre, "historial") || Contiene(nombre, "notas"))
            return "HISTORIAL";

        if (Contiene(nombre, "silabo"))
            return "SILABO";

        if (Contiene(nombre, "plan"))
            return "PLAN_ANALITICO";

        return "OTRO";
    }

    private string ResolverRaiz()
    {
        var raiz = _config["Documentos:Raiz"];
        if (string.IsNullOrWhiteSpace(raiz))
            raiz = RaizPorDefecto;

        return ResolverRutaAbsoluta(raiz);
    }

    private string ResolverRutaAbsoluta(string ruta) =>
        Path.IsPathRooted(ruta)
            ? Path.GetFullPath(ruta)
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, ruta));

    /// <summary>
    /// Resuelve la ruta escrita por el usuario. Además de la raíz local (./documentos),
    /// acepta prefijos de Windows configurados, p. ej. C:/Users/Usuario/Downloads ->
    /// /app/descargas (montado en solo lectura desde el equipo host).
    /// </summary>
    private string ResolverCarpeta(string ruta, string raiz)
    {
        var normalizada = NormalizarRuta(ruta);
        if (normalizada.Length == 0)
            throw new InvalidOperationException("Debe indicar la ruta de la carpeta de documentos.");

        var partes = normalizada.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Any(parte => parte == ".."))
            throw new InvalidOperationException($"La ruta de la carpeta no es válida: {ruta}");

        var (raizSeleccionada, relativa) = AplicarMapeo(normalizada, raiz);
        var candidatas = ConstruirCandidatas(raizSeleccionada, relativa, partes);

        foreach (var candidata in candidatas)
        {
            if (EstaDentroDeRaiz(candidata, raizSeleccionada) && Directory.Exists(candidata))
                return candidata;
        }

        throw new InvalidOperationException($"No se encontró la carpeta {ruta}");
    }

    private static string NormalizarRuta(string ruta) => ruta
        .Replace('\\', '/')
        .Replace("\"", "")
        .Trim()
        .Trim('/');

    private (string Raiz, string Relativa) AplicarMapeo(string ruta, string raizPorDefecto)
    {
        var comparacion = StringComparison.OrdinalIgnoreCase;

        foreach (var seccion in _config.GetSection("Documentos:Raices").GetChildren())
        {
            var prefijo = NormalizarRuta(seccion["Windows"] ?? string.Empty);
            var destino = seccion["Contenedor"] ?? seccion["Raiz"] ?? string.Empty;
            if (prefijo.Length == 0 || string.IsNullOrWhiteSpace(destino))
                continue;

            if (!ruta.Equals(prefijo, comparacion) &&
                !ruta.StartsWith(prefijo + "/", comparacion))
                continue;

            var resto = ruta[prefijo.Length..].Trim('/');
            return (ResolverRutaAbsoluta(destino), resto);
        }

        // Sin mapeo: se conserva el comportamiento anterior (ruta relativa a ./documentos,
        // con C:\... recortado para encontrar solo el nombre de la carpeta).
        var sinUnidad = PrefijoUnidadWindows.Replace(ruta, string.Empty).Trim('/');
        return (raizPorDefecto, sinUnidad);
    }

    private static IEnumerable<string> ConstruirCandidatas(string raiz, string relativa, string[] partesOriginales)
    {
        var candidatas = new List<string>();
        if (string.IsNullOrWhiteSpace(relativa))
            candidatas.Add(Path.GetFullPath(raiz));

        void Agregar(string? relativaCandidata)
        {
            if (string.IsNullOrWhiteSpace(relativaCandidata))
                return;

            var completa = Path.GetFullPath(Path.Combine(raiz, relativaCandidata.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidatas.Contains(completa, StringComparer.OrdinalIgnoreCase))
                candidatas.Add(completa);
        }

        Agregar(relativa);

        var partes = relativa.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length > 0)
        {
            // Ej.: "C:/Users/Usuario/Downloads/CARPETAESTUDIANTE/Sílabo" cuando dentro de la
            // raíz solo se copió "Sílabo" o "CARPETAESTUDIANTE/Sílabo".
            Agregar(partes.Last());
            if (partes.Length >= 2)
                Agregar(string.Join('/', partes.TakeLast(2)));
        }

        if (partesOriginales.Length >= 2)
            Agregar(string.Join('/', partesOriginales.TakeLast(2)));

        return candidatas;
    }

    private List<string> ObtenerPdfs(string carpeta)
    {
        var maximo = _config.GetValue<int>("Documentos:MaxArchivosCarpeta", MaxArchivosPorDefecto);
        if (maximo <= 0)
            maximo = MaxArchivosPorDefecto;

        var todos = Directory.EnumerateFiles(carpeta, "*", SearchOption.AllDirectories)
            .Where(archivo => string.Equals(Path.GetExtension(archivo), ".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(archivo => archivo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (todos.Count == 0)
            throw new InvalidOperationException($"No se encontraron archivos PDF en la carpeta {carpeta}.");

        if (todos.Count > maximo)
            _logger.LogWarning(
                "La carpeta {Carpeta} contiene {Total} PDF; se procesarán solo {Maximo}.",
                carpeta, todos.Count, maximo);

        return todos.Take(maximo).ToList();
    }

    private static bool EstaDentroDeRaiz(string ruta, string raiz)
    {
        var comparacion = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var raizCompleta = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raiz));
        var rutaCompleta = Path.GetFullPath(ruta);

        if (!rutaCompleta.StartsWith(raizCompleta, comparacion))
            return false;

        if (rutaCompleta.Length == raizCompleta.Length)
            return true;

        var separador = rutaCompleta[raizCompleta.Length];
        return separador == Path.DirectorySeparatorChar || separador == Path.AltDirectorySeparatorChar;
    }

    private static bool Contiene(string texto, string palabra) =>
        texto.Contains(palabra, StringComparison.OrdinalIgnoreCase);

    private static string QuitarTildes(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                resultado.Append(caracter);
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}

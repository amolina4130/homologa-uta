using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Datos del informe final que el Coordinador dirige al Consejo Directivo.</summary>
public class DatosInformeFinal : DatosInformeBase
{
    /// <summary>Nombre de quien firma; debajo se imprime "Coordinador de Homologación".</summary>
    public string Firmante { get; set; } = string.Empty;

    public string? CoordinadorCorreo { get; set; }
}

/// <summary>
/// Plantilla del informe final: afirma la homologación, va "Para: Consejo Directivo" y
/// firma como Coordinador de Homologación. Afirma la decisión en lugar de sugerirla.
/// </summary>
public static class PlantillaInformeFinal
{
    static PlantillaInformeFinal()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public const string SinHomologables =
        "No se homologa ninguna asignatura; en consecuencia, " +
        "el estudiante debe iniciar su plan de estudios desde cero.";

    public const string Destinatario = "Consejo Directivo";
    public const string CargoFirmante = "Coordinador de Homologación";

    public static string GenerarTexto(DatosInformeFinal datos) => GenerarTextoDeDatos(datos);

    public static string GenerarTexto(
        string numeroProceso,
        string numeroMemorando,
        string nombreEstudiante,
        string cedulaEstudiante,
        string carreraOrigen,
        string carreraDestino,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        string? observacionGeneral = null,
        string? firmante = null,
        DateTime? fecha = null)
    {
        return GenerarTextoDeDatos(new DatosInformeFinal
        {
            NumeroProceso = numeroProceso,
            NumeroMemorando = numeroMemorando,
            NombreEstudiante = nombreEstudiante,
            CedulaEstudiante = cedulaEstudiante,
            CarreraOrigen = carreraOrigen,
            CarreraDestino = carreraDestino,
            Asignaturas = asignaturas.ToList(),
            ObservacionGeneral = observacionGeneral,
            Firmante = firmante ?? string.Empty,
            Fecha = fecha ?? DateTime.UtcNow
        });
    }

    private static string GenerarTextoDeDatos(DatosInformeFinal datos)
    {
        var homologables = datos.Asignaturas
            .Select(AfirmarFila)
            .Where(a => !string.IsNullOrWhiteSpace(a.AsignaturaDestino) ||
                        !string.IsNullOrWhiteSpace(a.AsignaturaOrigen))
            .ToList();
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);

        var partes = new List<string>
        {
            $"Para: {Destinatario}",
            EncabezadoFinal(datos) +
            (homologables.Count > 0 ? "se resuelve homologar lo siguiente:" : SinHomologables)
        };

        var tabla = ContenidoInforme.FormarTabla(datos, homologables, afirmar: true);
        if (tabla.Length > 0)
            partes.Add(tabla);

        if (observacion is not null)
        {
            partes.Add($"Observación general: {ContenidoInforme.Afirmar(observacion)}");
        }

        partes.Add($"Atentamente,{Environment.NewLine}{ContenidoInforme.Afirmar(datos.Firmante)}{Environment.NewLine}" +
                   $"{CargoFirmante}{Environment.NewLine}C.I.: {ContenidoInforme.Valor(datos.CedulaFirmante)}");

        return AsegurarAfirmacion(string.Join(Environment.NewLine + Environment.NewLine, partes));
    }

    private static string EncabezadoFinal(DatosInformeFinal datos) =>
        $"De mi consideración:{Environment.NewLine}" +
        $"En atención al memorando {ContenidoInforme.Afirmar(datos.NumeroMemorando)}, " +
        $"el/la estudiante {ContenidoInforme.Afirmar(datos.NombreEstudiante)} con cédula " +
        $"{ContenidoInforme.Afirmar(datos.CedulaEstudiante)} solicita la homologación del cambio " +
        $"de la carrera {ContenidoInforme.Afirmar(datos.CarreraOrigen)} a la carrera " +
        $"{ContenidoInforme.Afirmar(datos.CarreraDestino)}. Revisada la documentación, ";

    private static FilaInformeAsignatura AfirmarFila(FilaInformeAsignatura fila) => new()
    {
        AsignaturaOrigen = ContenidoInforme.Afirmar(fila.AsignaturaOrigen),
        AsignaturaDestino = ContenidoInforme.Afirmar(fila.AsignaturaDestino),
        Justificacion = ContenidoInforme.Afirmar(fila.Justificacion)
    };

    /// <summary>Red de seguridad: ningún texto del informe final puede sugerir en vez de afirmar.</summary>
    private static string AsegurarAfirmacion(string texto) =>
        Regex.Replace(texto, @"se\s+sugiere", "se resuelve", RegexOptions.IgnoreCase);

    public static byte[] GenerarPdf(DatosInformeFinal datos) => GenerarPdfDeDatos(datos);

    public static byte[] GenerarPdf(
        string numeroProceso,
        string numeroMemorando,
        string nombreEstudiante,
        string cedulaEstudiante,
        string carreraOrigen,
        string carreraDestino,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        string? observacionGeneral = null,
        string? firmante = null,
        DateTime? fecha = null)
    {
        return GenerarPdfDeDatos(new DatosInformeFinal
        {
            NumeroProceso = numeroProceso,
            NumeroMemorando = numeroMemorando,
            NombreEstudiante = nombreEstudiante,
            CedulaEstudiante = cedulaEstudiante,
            CarreraOrigen = carreraOrigen,
            CarreraDestino = carreraDestino,
            Asignaturas = asignaturas.ToList(),
            ObservacionGeneral = observacionGeneral,
            Firmante = firmante ?? string.Empty,
            Fecha = fecha ?? DateTime.UtcNow
        });
    }

    private static byte[] GenerarPdfDeDatos(DatosInformeFinal datos)
    {
        var homologables = datos.Asignaturas
            .Select(AfirmarFila)
            .Where(a => !string.IsNullOrWhiteSpace(a.AsignaturaDestino) ||
                        !string.IsNullOrWhiteSpace(a.AsignaturaOrigen))
            .ToList();
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);

        var datosPdf = new DatosInformeBase
        {
            NumeroProceso = datos.NumeroProceso,
            NumeroMemorando = datos.NumeroMemorando,
            NombreEstudiante = datos.NombreEstudiante,
            CedulaEstudiante = datos.CedulaEstudiante,
            CarreraOrigen = datos.CarreraOrigen,
            CarreraDestino = datos.CarreraDestino,
            CedulaFirmante = datos.CedulaFirmante,
            Fecha = datos.Fecha
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Element(header =>
                    ContenidoInforme.EncabezadoPdf(
                        header,
                        datosPdf,
                        "INFORME FINAL DE HOMOLOGACIÓN",
                        Destinatario,
                        "Coordinación de Homologación",
                        "Homologación de asignaturas"));

                page.Content().Element(content => content.Column(column =>
                {
                    column.Item().Text(text =>
                    {
                        text.Span(EncabezadoFinal(datos));
                        text.Span(homologables.Count > 0
                            ? "se resuelve homologar lo siguiente:"
                            : SinHomologables).Bold();
                    });

                    if (homologables.Count > 0)
                    {
                        column.Item().PaddingTop(8).Table(table =>
                            ContenidoInforme.TablaPdf(table, datos, homologables, afirmar: true));
                    }

                    if (observacion is not null)
                    {
                        column.Item().PaddingTop(8).Text(
                            $"Observación general: {ContenidoInforme.Afirmar(observacion)}");
                    }

                    column.Item().PaddingTop(16).Element(firma =>
                        ContenidoInforme.FirmaPdf(
                            firma,
                            ContenidoInforme.Afirmar(datos.Firmante),
                            CargoFirmante,
                            datos.CedulaFirmante));

                    if (!string.IsNullOrWhiteSpace(datos.CoordinadorCorreo))
                        column.Item().PaddingTop(4).Text(datos.CoordinadorCorreo).FontSize(8);
                }));

                page.Footer().Element(ContenidoInforme.PiePdf);
            });
        }).GeneratePdf();
    }
}

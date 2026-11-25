using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Datos del informe final que el Coordinador envía al Comité.</summary>
public class DatosInformeFinal : DatosInformeBase
{
    /// <summary>Nombre de quien firma; debajo se imprime "Coordinador de Homologación".</summary>
    public string Firmante { get; set; } = string.Empty;

    public string? CoordinadorCorreo { get; set; }
}

/// <summary>
/// Plantilla del informe final: va al Comité, lista solo las asignaturas que se homologan y
/// recomienda su homologación (el envío al Comité no es la aprobación oficial). Firma el
/// Coordinador de Homologación.
/// </summary>
public static class PlantillaInformeFinal
{
    static PlantillaInformeFinal()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public const string SinHomologables =
        "no se recomienda homologar ninguna asignatura; en consecuencia, " +
        "el estudiante debe iniciar su plan de estudios desde el primer nivel.";

    public const string Destinatario = "Comité de Homologación";
    public const string CargoFirmante = "Coordinador de Homologación";
    private const string Introduccion = "se recomienda la homologación de las siguientes asignaturas:";

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

    private static List<FilaInformeAsignatura> Homologables(DatosInformeFinal datos) =>
        datos.Asignaturas
            .Where(a => !string.IsNullOrWhiteSpace(a.AsignaturaDestino) ||
                        !string.IsNullOrWhiteSpace(a.AsignaturaOrigen))
            .ToList();

    private static string GenerarTextoDeDatos(DatosInformeFinal datos)
    {
        var homologables = Homologables(datos);
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);

        var partes = new List<string>
        {
            EncabezadoFinal(datos) + (homologables.Count > 0 ? Introduccion : SinHomologables)
        };

        var tabla = ContenidoInforme.FormarTabla(datos, homologables, afirmar: true);
        if (tabla.Length > 0)
            partes.Add(tabla);

        if (observacion is not null)
            partes.Add($"Observación general: {ContenidoInforme.Afirmar(observacion)}");

        partes.Add($"Atentamente,{Environment.NewLine}{ContenidoInforme.Afirmar(datos.Firmante)}{Environment.NewLine}" +
                   $"{CargoFirmante}{Environment.NewLine}C.I.: {ContenidoInforme.Valor(datos.CedulaFirmante)}");

        return AsegurarRecomendacion(string.Join(Environment.NewLine + Environment.NewLine, partes));
    }

    private static string EncabezadoFinal(DatosInformeFinal datos) =>
        $"De mi consideración:{Environment.NewLine}" +
        $"En atención al memorando {ContenidoInforme.Afirmar(datos.NumeroMemorando)}, " +
        $"el/la estudiante {ContenidoInforme.Afirmar(datos.NombreEstudiante)} con cédula " +
        $"{ContenidoInforme.Afirmar(datos.CedulaEstudiante)} solicita la homologación del cambio " +
        $"de la carrera {ContenidoInforme.Afirmar(datos.CarreraOrigen)} a la carrera " +
        $"{ContenidoInforme.Afirmar(datos.CarreraDestino)}. Revisado el informe técnico del docente asignado " +
        "y la documentación presentada, ";

    /// <summary>Red de seguridad: el informe final recomienda; no sugiere ni resuelve.</summary>
    private static string AsegurarRecomendacion(string texto) =>
        Regex.Replace(texto, @"se\s+(sugiere|resuelve)", "se recomienda", RegexOptions.IgnoreCase);

    public static byte[] GenerarPdf(DatosInformeFinal datos) => GenerarPdfDeDatos(datos, null);

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
        }, null);
    }

    /// <summary>
    /// PDF con el texto que editó el Coordinador. La tabla y la firma se dibujan siempre con los
    /// datos reales (no como texto), así el informe conserva su formato aunque se haya editado.
    /// </summary>
    public static byte[] GenerarPdfDesdeTexto(string texto, DatosInformeFinal datos) =>
        GenerarPdfDeDatos(datos, texto);

    private static byte[] GenerarPdfDeDatos(DatosInformeFinal datos, string? textoEditado)
    {
        var homologables = Homologables(datos);
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);

        // Del texto editado se conserva la redacción: sin tabla, sin firma y sin la línea "Para:".
        var narrativo = string.Join(Environment.NewLine,
                ContenidoInforme.QuitarCola(ContenidoInforme.QuitarTabla(textoEditado))
                    .Replace("\r\n", "\n").Split('\n')
                    .Where(l => !l.TrimStart().StartsWith("Para:", StringComparison.OrdinalIgnoreCase)))
            .Trim();
        narrativo = AsegurarRecomendacion(narrativo);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4).LineHeight(1.25f));

                page.Header().Element(h => ContenidoInforme.MembretePdf(h, "INFORME FINAL DE HOMOLOGACIÓN"));

                page.Content().Element(content => content.Column(column =>
                {
                    column.Item().Element(c => ContenidoInforme.BloqueMemorandoPdf(c, datos,
                        Destinatario, "Coordinación de Homologación", "Informe final de homologación de asignaturas"));
                    column.Item().PaddingTop(8).Element(c => ContenidoInforme.DatosSolicitudPdf(c, datos));

                    column.Item().PaddingTop(10).Element(c =>
                    {
                        if (narrativo.Length > 0)
                        {
                            c.Text(narrativo).FontSize(9).Justify();
                            return;
                        }
                        c.Text(text =>
                        {
                            text.Span(EncabezadoFinal(datos));
                            text.Span(homologables.Count > 0 ? Introduccion : SinHomologables).Bold();
                        });
                    });

                    if (homologables.Count > 0)
                    {
                        column.Item().PaddingTop(10).Element(c =>
                            ContenidoInforme.ResumenPdf(c, homologables.Count, homologables.Count, soloHomologadas: true));
                        column.Item().PaddingTop(10).Table(table =>
                            ContenidoInforme.TablaPdf(table, datos, homologables, afirmar: true, conDecision: false));
                        column.Item().PaddingTop(3).Element(ContenidoInforme.LeyendaPorcentajesPdf);
                    }

                    if (narrativo.Length == 0 && observacion is not null)
                        column.Item().PaddingTop(10).Text(t =>
                        {
                            t.Span("Observación general: ").Bold();
                            t.Span(ContenidoInforme.Afirmar(observacion));
                        });

                    // La nota y la firma van juntas: la firma nunca queda sola en otra página.
                    column.Item().PaddingTop(10).ShowEntire().Column(cierre =>
                    {
                        cierre.Item().Text(
                                "Este informe se pone en conocimiento del Comité de Homologación para su trámite; " +
                                "no constituye por sí mismo la aprobación oficial de la homologación.")
                            .FontSize(7.5f).Italic().FontColor(Colors.Grey.Darken1);
                        cierre.Item().PaddingTop(14).Element(firma =>
                            ContenidoInforme.FirmaPdf(
                                firma,
                                ContenidoInforme.Valor(datos.Firmante),
                                CargoFirmante,
                                datos.CedulaFirmante,
                                datos.CoordinadorCorreo));
                    });
                }));

                page.Footer().Element(ContenidoInforme.PiePdf);
            });
        }).GeneratePdf();
    }
}

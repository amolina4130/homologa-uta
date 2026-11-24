using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Una fila de la tabla de siete columnas del memorando de homologación.</summary>
public class FilaInformeAsignatura
{
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;

    /// <summary>Carrera de origen; si se omita se toma de los datos generales.</summary>
    public string CarreraOrigen { get; set; } = string.Empty;
    /// <summary>Carrera de destino; si se omita se toma de los datos generales.</summary>
    public string CarreraDestino { get; set; } = string.Empty;
    /// <summary>Contenido/porcentajes que sustentan la homologación.</summary>
    public string Contenido { get; set; } = string.Empty;
    /// <summary>Respuesta de la columna "¿Se homologa?".</summary>
    public string SeHomologa { get; set; } = string.Empty;
}

/// <summary>Datos comunes del encabezado de los informes del docente y del Coordinador.</summary>
public class DatosInformeBase
{
    public string NumeroProceso { get; set; } = string.Empty;
    public string NumeroMemorando { get; set; } = string.Empty;
    public string NombreEstudiante { get; set; } = string.Empty;
    public string CedulaEstudiante { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string? ObservacionGeneral { get; set; }

    /// <summary>C.I. de quien firma; se imprime en la línea de firma del memorando.</summary>
    public string CedulaFirmante { get; set; } = "-";

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public List<FilaInformeAsignatura> Asignaturas { get; set; } = new();
}

public class DatosInformeDocente : DatosInformeBase
{
    /// <summary>Correo o nombre del docente que firma el informe.</summary>
    public string Docente { get; set; } = string.Empty;

    /// <summary>Correo editable que se imprime junto a la firma.</summary>
    public string CorreoFirmante { get; set; } = string.Empty;

    /// <summary>Párrafo opcional propuesto por la IA y validado por el Docente.</summary>
    public string? OpinionIA { get; set; }

    /// <summary>
    /// Texto ya revisado por el Docente. Si viene, el PDF muestra ese texto en lugar de
    /// reconstruir la plantilla, para que coincida con lo que vio en pantalla.
    /// </summary>
    public string? TextoInforme { get; set; }
}

/// <summary>
/// Utilidades compartidas por las dos plantillas. El texto y el PDF se construyen con la
/// misma información para que no haya diferencias entre lo que se muestra y lo que se imprime.
/// </summary>
internal static class ContenidoInforme
{
    private const string Azul = "#1B4F9C";

    internal static string Valor(string? texto) => string.IsNullOrWhiteSpace(texto) ? "-" : texto.Trim();

    /// <summary>
    /// Descarta el texto automático antiguo "Revisión con apoyo de IA": el Docente escribe su
    /// propia observación y ese rótulo no debe aparecer en ningún informe.
    /// </summary>
    internal static string? Observacion(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Trim();
        return limpio.Contains("Revisión con apoyo de IA", StringComparison.OrdinalIgnoreCase)
            ? null
            : limpio;
    }

    /// <summary>El informe final afirma la decisión: nunca debe contener "se sugiere".</summary>
    internal static string Afirmar(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? "-"
            : Regex.Replace(texto.Trim(), @"se\s+sugiere", "se resuelve", RegexOptions.IgnoreCase);

    internal static string Encabezado(DatosInformeBase datos) =>
        $"De mi consideración:{Environment.NewLine}" +
        $"En atención al memorando {Valor(datos.NumeroMemorando)}, " +
        $"el/la estudiante {Valor(datos.NombreEstudiante)} con cédula {Valor(datos.CedulaEstudiante)} " +
        $"solicita la homologación del cambio de la carrera {Valor(datos.CarreraOrigen)} " +
        $"a la carrera {Valor(datos.CarreraDestino)}. Revisada la documentación, ";

    /// <summary>Completa los campos opcionales de la fila con los datos generales del memorando.</summary>
    internal static FilaInformeAsignatura Normalizar(
        DatosInformeBase datos, FilaInformeAsignatura fila, bool afirmar = false)
    {
        Func<string?, string> limpio = afirmar
            ? texto => Afirmar(texto)
            : texto => Valor(texto);

        var contenido = string.IsNullOrWhiteSpace(fila.Contenido)
            ? limpio(fila.Justificacion)
            : limpio(fila.Contenido);

        return new FilaInformeAsignatura
        {
            CarreraOrigen = string.IsNullOrWhiteSpace(fila.CarreraOrigen)
                ? Valor(datos.CarreraOrigen)
                : limpio(fila.CarreraOrigen),
            CarreraDestino = string.IsNullOrWhiteSpace(fila.CarreraDestino)
                ? Valor(datos.CarreraDestino)
                : limpio(fila.CarreraDestino),
            AsignaturaOrigen = limpio(fila.AsignaturaOrigen),
            AsignaturaDestino = limpio(fila.AsignaturaDestino),
            Contenido = contenido,
            SeHomologa = string.IsNullOrWhiteSpace(fila.SeHomologa) ? "SÍ" : limpio(fila.SeHomologa),
            Justificacion = limpio(fila.Justificacion)
        };
    }

    internal static List<FilaInformeAsignatura> Normalizar(
        DatosInformeBase datos, IEnumerable<FilaInformeAsignatura> filas, bool afirmar = false) =>
        filas.Select(f => Normalizar(datos, f, afirmar)).ToList();

    internal static string FormarTabla(
        DatosInformeBase datos, IEnumerable<FilaInformeAsignatura> asignaturas, bool afirmar = false)
    {
        var filas = Normalizar(datos, asignaturas, afirmar);
        if (filas.Count == 0)
            return string.Empty;

        var lineas = new List<string>
        {
            "N.º | Carrera de origen | Carrera de destino | Materia a homologar (origen) | Contenido | Materia equivalente (destino) | ¿Se homologa?"
        };

        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            var contenido = f.Contenido == "-" && f.Justificacion != "-"
                ? f.Justificacion
                : f.Contenido;
            lineas.Add($"{i + 1} | {f.CarreraOrigen} | {f.CarreraDestino} | {f.AsignaturaOrigen} | " +
                       $"{contenido} | {f.AsignaturaDestino} | {f.SeHomologa}");
        }

        return string.Join(Environment.NewLine, lineas);
    }

    /// <summary>
    /// Quita del texto guardado la tabla en formato de pipes: el PDF la vuelve a dibujar como
    /// tabla real con el encabezado explícito, sin duplicarla.
    /// </summary>
    internal static string QuitarTabla(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var resultado = new List<string>();
        var dentroDeTabla = false;
        foreach (var linea in texto.Replace("\r\n", "\n").Split('\n'))
        {
            if (linea.StartsWith("N.º |", StringComparison.Ordinal))
            {
                dentroDeTabla = true;
                continue;
            }

            if (dentroDeTabla && Regex.IsMatch(linea.TrimStart(), @"^\d+\s*\|"))
                continue;

            dentroDeTabla = false;
            resultado.Add(linea);
        }

        return string.Join(Environment.NewLine, resultado).Trim();
    }

    /// <summary>
    /// La firma se dibuja al final del PDF con los datos ya editados por el Docente; se quita
    /// del texto guardado para no repetirla.
    /// </summary>
    internal static string QuitarCola(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var resultado = texto.Replace("\r\n", "\n");
        var cortes = new[] { "Atentamente," };
        var posicion = cortes
            .Select(c => resultado.IndexOf(c, StringComparison.OrdinalIgnoreCase))
            .Where(p => p >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        return (posicion < 0 ? resultado : resultado[..posicion]).Trim();
    }

    /// <summary>Membrete con los dos logos y el bloque PARA / DE / ASUNTO / FECHA.</summary>
    internal static void EncabezadoPdf(
        IContainer container,
        DatosInformeBase datos,
        string titulo,
        string para,
        string de,
        string asunto)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(62).Height(48).Element(Logo("logo-uta.png"));
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text("UNIVERSIDAD TÉCNICA DE AMBATO")
                        .Bold().FontSize(11).FontColor(Azul);
                    col.Item().AlignCenter().Text("FACULTAD DE INGENIERÍA EN SISTEMAS, ELECTRÓNICA E INDUSTRIAL")
                        .FontSize(8).FontColor(Azul);
                    col.Item().AlignCenter().Text(titulo).Bold().FontSize(10);
                });
                row.ConstantItem(62).Height(48).Element(Logo("logo-fisei.png"));
            });

            column.Item().PaddingTop(6).Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.ConstantItem(46).Text("PARA:").Bold().FontSize(8);
                    r.RelativeItem().Text(para).FontSize(8);
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(46).Text("DE:").Bold().FontSize(8);
                    r.RelativeItem().Text(de).FontSize(8);
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(46).Text("ASUNTO:").Bold().FontSize(8);
                    r.RelativeItem().Text(asunto).FontSize(8);
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(46).Text("FECHA:").Bold().FontSize(8);
                    r.RelativeItem().Text($"{datos.Fecha:dd/MM/yyyy}").FontSize(8);
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(46).Text("N.º:").Bold().FontSize(8);
                    r.RelativeItem().Text(Valor(datos.NumeroMemorando)).FontSize(8);
                });
            });

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Azul);
            column.Item().PaddingTop(6).Text(
                $"Estudiante: {Valor(datos.NombreEstudiante)}, C.I.: {Valor(datos.CedulaEstudiante)}").FontSize(8);
            column.Item().PaddingTop(8);
        });
    }

    private static Action<IContainer> Logo(string nombreArchivo) => container =>
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", nombreArchivo);
        if (File.Exists(ruta))
            container.Image(File.ReadAllBytes(ruta)).FitArea();
        else
            container.Border(0.5f).BorderColor(Azul).AlignCenter().AlignMiddle()
                .Text(nombreArchivo.Contains("fisei") ? "FISEI" : "UTA")
                .Bold().FontSize(9).FontColor(Azul);
    };

    /// <summary>Tabla de siete columnas, igual que la maqueta del memorando.</summary>
    internal static void TablaPdf(
        TableDescriptor table,
        DatosInformeBase datos,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        bool afirmar = false)
    {
        var filas = Normalizar(datos, asignaturas, afirmar);

        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(26);
            columns.RelativeColumn(16);
            columns.RelativeColumn(16);
            columns.RelativeColumn(20);
            columns.RelativeColumn(18);
            columns.RelativeColumn(20);
            columns.ConstantColumn(42);
        });

        table.Header(header =>
        {
            Cabecera(header, "N.º");
            Cabecera(header, "Carrera de origen");
            Cabecera(header, "Carrera de destino");
            Cabecera(header, "Materia a homologar (origen)");
            Cabecera(header, "Contenido");
            Cabecera(header, "Materia equivalente (destino)");
            Cabecera(header, "¿Se homologa?");
        });

        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            Celda(table, (i + 1).ToString());
            Celda(table, f.CarreraOrigen);
            Celda(table, f.CarreraDestino);
            Celda(table, f.AsignaturaOrigen);
            Celda(table, f.Contenido);
            Celda(table, f.AsignaturaDestino);
            Celda(table, f.SeHomologa);
        }
    }

    /// <summary>Firma con línea, nombre, cargo, C.I. y correo opcional.</summary>
    internal static void FirmaPdf(
        IContainer container, string nombre, string cargo, string cedula, string? correo = null) =>
        container.Column(column =>
        {
            column.Item().Text("Atentamente,").FontSize(9);
            column.Item().PaddingTop(26).Row(row =>
                row.ConstantItem(220).PaddingBottom(2).LineHorizontal(0.8f).LineColor(Colors.Grey.Darken3));
            column.Item().Text(nombre).Bold().FontSize(9);
            column.Item().Text(cargo).FontSize(9);
            column.Item().Text($"C.I.: {Valor(cedula)}").FontSize(8);
            if (!string.IsNullOrWhiteSpace(correo))
                column.Item().Text($"Correo: {Valor(correo)}").FontSize(8);
        });

    internal static void PiePdf(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("HomologaUTA · Universidad Técnica de Ambato")
                .FontSize(8).FontColor(Colors.Grey.Medium);

            row.ConstantItem(120).AlignRight().Text(text =>
            {
                text.Span("Página ").FontSize(8);
                text.CurrentPageNumber().FontSize(8);
                text.Span(" de ").FontSize(8);
                text.TotalPages().FontSize(8);
            });
        });
    }

    internal static void Cabecera(TableCellDescriptor header, string texto)
    {
        header.Cell().Background(Azul).Padding(3)
            .Text(texto).FontColor(Colors.White).Bold().FontSize(7);
    }

    internal static void Celda(TableDescriptor table, string texto)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(3).Text(texto).FontSize(7);
    }
}

/// <summary>Plantilla del informe del docente asignado: usa "se sugiere" y firma como docente.</summary>
public static class PlantillaInformeDocente
{
    static PlantillaInformeDocente()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public const string SinHomologables =
        "No se identifica ninguna asignatura homologable; en consecuencia, " +
        "el estudiante debe iniciar su plan de estudios desde cero.";

    /// <summary>Encabezado visible de la tabla con la decisión SÍ/NO de cada materia.</summary>
    public const string TituloTablaHomologables =
        "Tabla de asignaturas evaluadas (materias que se pueden homologar y materias que no):";

    /// <summary>Rótulo del párrafo opcional generado con IA; también se usa para extraerlo del texto guardado.</summary>
    public const string EtiquetaOpinionIA = "Opinión técnica asistida por IA:";

    public static string GenerarTexto(DatosInformeDocente datos) => GenerarTextoDeDatos(datos);

    public static string GenerarTexto(
        string numeroProceso,
        string numeroMemorando,
        string nombreEstudiante,
        string cedulaEstudiante,
        string carreraOrigen,
        string carreraDestino,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        string? observacionGeneral = null,
        string? docente = null,
        DateTime? fecha = null,
        string? opinionIA = null)
    {
        return GenerarTextoDeDatos(new DatosInformeDocente
        {
            NumeroProceso = numeroProceso,
            NumeroMemorando = numeroMemorando,
            NombreEstudiante = nombreEstudiante,
            CedulaEstudiante = cedulaEstudiante,
            CarreraOrigen = carreraOrigen,
            CarreraDestino = carreraDestino,
            Asignaturas = asignaturas.ToList(),
            ObservacionGeneral = observacionGeneral,
            Docente = docente ?? string.Empty,
            Fecha = fecha ?? DateTime.UtcNow,
            OpinionIA = opinionIA
        });
    }

    private static string GenerarTextoDeDatos(DatosInformeDocente datos)
    {
        var asignaturas = datos.Asignaturas.Where(a =>
            !string.IsNullOrWhiteSpace(a.AsignaturaDestino) ||
            !string.IsNullOrWhiteSpace(a.AsignaturaOrigen)).ToList();
        var hayHomologables = asignaturas.Any(a =>
            a.SeHomologa?.StartsWith("S", StringComparison.OrdinalIgnoreCase) == true);
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);

        var introduccion = asignaturas.Count == 0
            ? SinHomologables
            : hayHomologables
                ? "se sugiere la siguiente decisión por asignatura:"
                : "se sugiere no homologar las asignaturas evaluadas:";

        var partes = new List<string>
        {
            ContenidoInforme.Encabezado(datos) + introduccion
        };

        var tabla = ContenidoInforme.FormarTabla(datos, asignaturas);
        if (tabla.Length > 0)
        {
            partes.Add(TituloTablaHomologables);
            partes.Add(tabla);
        }

        if (observacion is not null)
            partes.Add($"Observación general: {observacion}");

        if (!string.IsNullOrWhiteSpace(datos.OpinionIA))
            partes.Add($"{EtiquetaOpinionIA} {datos.OpinionIA.Trim()}");

        var firma = $"Atentamente,{Environment.NewLine}{ContenidoInforme.Valor(datos.Docente)}{Environment.NewLine}" +
                    $"Docente asignado{Environment.NewLine}C.I.: {ContenidoInforme.Valor(datos.CedulaFirmante)}";
        if (!string.IsNullOrWhiteSpace(datos.CorreoFirmante))
            firma += $"{Environment.NewLine}Correo: {datos.CorreoFirmante.Trim()}";
        partes.Add(firma);

        return string.Join(Environment.NewLine + Environment.NewLine, partes);
    }

    public static byte[] GenerarPdf(DatosInformeDocente datos) => GenerarPdfDeDatos(datos);

    public static byte[] GenerarPdf(
        string numeroProceso,
        string numeroMemorando,
        string nombreEstudiante,
        string cedulaEstudiante,
        string carreraOrigen,
        string carreraDestino,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        string? observacionGeneral = null,
        string? docente = null,
        DateTime? fecha = null,
        string? opinionIA = null)
    {
        return GenerarPdfDeDatos(new DatosInformeDocente
        {
            NumeroProceso = numeroProceso,
            NumeroMemorando = numeroMemorando,
            NombreEstudiante = nombreEstudiante,
            CedulaEstudiante = cedulaEstudiante,
            CarreraOrigen = carreraOrigen,
            CarreraDestino = carreraDestino,
            Asignaturas = asignaturas.ToList(),
            ObservacionGeneral = observacionGeneral,
            Docente = docente ?? string.Empty,
            Fecha = fecha ?? DateTime.UtcNow,
            OpinionIA = opinionIA
        });
    }

    private static byte[] GenerarPdfDeDatos(DatosInformeDocente datos)
    {
        var asignaturas = datos.Asignaturas.Where(a =>
            !string.IsNullOrWhiteSpace(a.AsignaturaDestino) ||
            !string.IsNullOrWhiteSpace(a.AsignaturaOrigen)).ToList();
        var hayHomologables = asignaturas.Any(a =>
            a.SeHomologa?.StartsWith("S", StringComparison.OrdinalIgnoreCase) == true);
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);
        var introduccion = asignaturas.Count == 0
            ? SinHomologables
            : hayHomologables
                ? "se sugiere la siguiente decisión por asignatura:"
                : "se sugiere no homologar las asignaturas evaluadas:";

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
                        datos,
                        "INFORME DEL DOCENTE ASIGNADO",
                        "Consejo Directivo",
                        $"Docente asignado · {ContenidoInforme.Valor(datos.Docente)}",
                        "Homologación de asignaturas"));

                var textoNarrativo = ContenidoInforme.QuitarCola(
                    ContenidoInforme.QuitarTabla(datos.TextoInforme));
                var mostrarTituloTabla = asignaturas.Count > 0 &&
                    (string.IsNullOrWhiteSpace(datos.TextoInforme) ||
                     !datos.TextoInforme.Contains(TituloTablaHomologables, StringComparison.OrdinalIgnoreCase));

                page.Content().Element(content => content.Column(column =>
                {
                    if (!string.IsNullOrWhiteSpace(textoNarrativo))
                    {
                        column.Item().Text(textoNarrativo).FontSize(9);
                    }
                    else
                    {
                        column.Item().Text(text =>
                        {
                            text.Span(ContenidoInforme.Encabezado(datos));
                            text.Span(introduccion).Bold();
                        });

                        if (observacion is not null)
                        {
                            column.Item().PaddingTop(8).Text(
                                $"Observación general: {observacion}");
                        }

                        if (!string.IsNullOrWhiteSpace(datos.OpinionIA))
                        {
                            column.Item().PaddingTop(8).Text(
                                $"{EtiquetaOpinionIA} {datos.OpinionIA.Trim()}");
                        }
                    }

                    if (asignaturas.Count > 0)
                    {
                        if (mostrarTituloTabla)
                            column.Item().PaddingTop(8).Text(TituloTablaHomologables).Bold().FontSize(9);

                        column.Item().PaddingTop(4).Table(table =>
                            ContenidoInforme.TablaPdf(table, datos, asignaturas));
                    }

                    column.Item().PaddingTop(16).Element(firma =>
                        ContenidoInforme.FirmaPdf(
                            firma,
                            ContenidoInforme.Valor(datos.Docente),
                            "Docente asignado",
                            datos.CedulaFirmante,
                            datos.CorreoFirmante));
                }));

                page.Footer().Element(ContenidoInforme.PiePdf);
            });
        }).GeneratePdf();
    }
}

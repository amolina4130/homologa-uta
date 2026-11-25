using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Datos que se imprimen en el informe PDF de homologación (HU-18).</summary>
public class InformeData
{
    public int SolicitudId { get; set; }
    public string NumeroProceso { get; set; } = string.Empty;
    public string Estudiante { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime? FechaAnalisis { get; set; }
    public DateTime? FechaInforme { get; set; }
    public string? CoordinadorCorreo { get; set; }
    public string Estado { get; set; } = string.Empty;

    public List<InformeAsignatura> Asignaturas { get; set; } = new();
    public InformeResolucion? Resolucion { get; set; }
}

public class InformeAsignatura
{
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public bool Combinada { get; set; }
    public List<string> MateriasOrigen { get; set; } = new();
    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;
    public string? DecisionCoordinador { get; set; }
    public string? ObservacionCoordinador { get; set; }
    public string? DecisionFinal { get; set; }
}

public class InformeResolucion
{
    public string NumeroResolucion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Observacion { get; set; }
    public string RegistradoPor { get; set; } = string.Empty;
    public List<InformeDecisionFinal> Decisiones { get; set; } = new();
}

public class InformeDecisionFinal
{
    public string Asignatura { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
}

/// <summary>Plantilla del informe: encabezado institucional, tabla por asignatura y pie de página.</summary>
public static class PlantillaInforme
{
    public static byte[] Generar(InformeData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Element(header => Encabezado(header, data));
                page.Content().Element(content => Contenido(content, data));
                page.Footer().Element(footer => Pie(footer));
            });
        }).GeneratePdf();
    }

    private static void Encabezado(IContainer container, InformeData data)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("UNIVERSIDAD TÉCNICA DE AMBATO").Bold().FontSize(13);
                    col.Item().Text("Sistema de Homologación de Asignaturas").FontSize(10);
                    col.Item().Text("Informe de Homologación Académica").Bold().FontSize(11);
                });

                row.ConstantItem(140).AlignRight().Column(col =>
                {
                    col.Item().Text($"N° {data.NumeroProceso}").Bold();
                    col.Item().Text($"Emitido: {DateTime.Now:dd/MM/yyyy}");
                });
            });

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Blue.Darken3);

            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Estudiante: {data.Estudiante}").Bold();
                    col.Item().Text($"Tipo: {data.Tipo} · Módulo: {data.Modulo}");
                    col.Item().Text($"Carrera de origen: {data.CarreraOrigen}");
                    col.Item().Text($"Carrera de destino: {data.CarreraDestino}");
                });

                row.ConstantItem(220).Column(col =>
                {
                    col.Item().Text($"Fecha de registro: {data.FechaRegistro:dd/MM/yyyy}");
                    if (data.FechaAnalisis.HasValue)
                        col.Item().Text($"Fecha de análisis: {data.FechaAnalisis:dd/MM/yyyy HH:mm}");
                    if (data.FechaInforme.HasValue)
                        col.Item().Text($"Fecha del informe: {data.FechaInforme:dd/MM/yyyy HH:mm}");
                    if (!string.IsNullOrWhiteSpace(data.CoordinadorCorreo))
                        col.Item().Text($"Coordinador: {data.CoordinadorCorreo}");
                });
            });

            if (!string.IsNullOrWhiteSpace(data.UniversidadOrigen) ||
                !string.IsNullOrWhiteSpace(data.UniversidadDestino))
            {
                column.Item().PaddingTop(4).Text(
                    $"Universidad de origen: {data.UniversidadOrigen ?? "-"} · " +
                    $"Universidad de destino: {data.UniversidadDestino ?? "-"}");
            }

            column.Item().PaddingTop(10);
        });
    }

    private static void Contenido(IContainer container, InformeData data)
    {
        container.Column(column =>
        {
            column.Item().Text("1. Resultado por asignatura").Bold().FontSize(11);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(2.4f);
                    columns.ConstantColumn(36);
                    columns.ConstantColumn(36);
                    columns.ConstantColumn(36);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(4.2f);
                });

                table.Header(header =>
                {
                    Cabecera(header, "Origen");
                    Cabecera(header, "Destino");
                    Cabecera(header, "Cont.");
                    Cabecera(header, "Prof.");
                    Cabecera(header, "Horas");
                    Cabecera(header, "Decisión");
                    Cabecera(header, "Justificación");
                });

                foreach (var a in data.Asignaturas)
                {
                    var origen = a.Combinada && a.MateriasOrigen.Count > 0
                        ? string.Join(" + ", a.MateriasOrigen)
                        : a.AsignaturaOrigen;

                    Celda(table, origen);
                    Celda(table, a.AsignaturaDestino);
                    Celda(table, $"{a.PorcentajeContenido}%");
                    Celda(table, $"{a.PorcentajeProfundidad}%");
                    Celda(table, $"{a.PorcentajeHoras}%");
                    Celda(table, a.Decision);
                    Celda(table, a.Justificacion);
                }
            });

            column.Item().PaddingTop(12).Text("2. Decisión del Coordinador").Bold().FontSize(11);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(5);
                });

                table.Header(header =>
                {
                    Cabecera(header, "Asignatura");
                    Cabecera(header, "Decisión");
                    Cabecera(header, "Observación");
                });

                foreach (var a in data.Asignaturas)
                {
                    Celda(table, a.AsignaturaDestino);
                    Celda(table, a.DecisionCoordinador ?? "Pendiente");
                    Celda(table, a.ObservacionCoordinador ?? string.Empty);
                }
            });

            if (data.Resolucion is not null)
            {
                column.Item().PaddingTop(12).Text("3. Resolución del Consejo Directivo").Bold().FontSize(11);
                column.Item().PaddingTop(4).Text(
                    $"Resolución N° {data.Resolucion.NumeroResolucion} del " +
                    $"{data.Resolucion.Fecha:dd/MM/yyyy}. Registrada por {data.Resolucion.RegistradoPor}.");

                if (!string.IsNullOrWhiteSpace(data.Resolucion.Observacion))
                    column.Item().PaddingTop(2).Text($"Observación: {data.Resolucion.Observacion}");

                column.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        Cabecera(header, "Asignatura");
                        Cabecera(header, "Decisión final");
                    });

                    foreach (var d in data.Resolucion.Decisiones)
                    {
                        Celda(table, d.Asignatura);
                        Celda(table, d.Decision);
                    }
                });
            }

            column.Item().PaddingTop(14).Background(Colors.Grey.Lighten4)
                .Border(1).BorderColor(Colors.Grey.Lighten1)
                .Padding(8)
                .Text("Aviso: este informe deja constancia del análisis técnico y de lo resuelto por el " +
                      "Consejo Directivo. El sistema no reemplaza el trámite oficial de homologación.")
                .FontSize(8).Italic();
        });
    }

    private static void Pie(IContainer container)
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

    private static void Cabecera(TableCellDescriptor header, string texto)
    {
        header.Cell().Background(Colors.Blue.Darken3).Padding(3)
            .Text(texto).FontColor(Colors.White).Bold().FontSize(8);
    }

    private static void Celda(TableDescriptor table, string texto)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(3).Text(string.IsNullOrWhiteSpace(texto) ? "-" : texto).FontSize(8);
    }
}

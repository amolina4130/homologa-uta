using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Plantilla del informe: encabezado institucional, secciones y pie de página.</summary>
public static class PlantillaInforme
{
    static PlantillaInforme()
    {
        // Licencia Community de QuestPDF: gratuita para proyectos académicos y organizaciones pequeñas.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generar(InformeDatos datos)
    {
        var generado = DateTime.UtcNow;

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.8f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9.5f));

                pagina.Header().Element(c => Encabezado(c, datos));

                pagina.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Element(c => DatosDeLaSolicitud(c, datos));
                    col.Item().Element(c => TablaDeAnalisis(c, datos));
                    col.Item().Element(c => InformeDelCoordinador(c, datos));

                    // Sin resolución del Consejo, esta sección simplemente no aparece.
                    if (datos.Resolucion is not null)
                        col.Item().Element(c => ResolucionDelConsejo(c, datos.Resolucion));

                    col.Item().Element(AvisoOficial);
                });

                pagina.Footer().Element(c => PiePagina(c, datos, generado));
            });
        }).GeneratePdf();
    }

    // ---------------------------------------------------------------
    // Secciones
    // ---------------------------------------------------------------

    private static void Encabezado(IContainer contenedor, InformeDatos d)
    {
        contenedor.Column(col =>
        {
            col.Item().Text("UNIVERSIDAD TÉCNICA DE AMBATO").FontSize(15).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().Text("Facultad de Ingeniería en Sistemas, Electrónica e Industrial")
                .FontSize(9).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(3).LineHorizontal(1.2f).LineColor(Colors.Blue.Darken3);
            col.Item().PaddingTop(5).Row(fila =>
            {
                fila.RelativeItem().Text("Informe de homologación de asignaturas").FontSize(12).SemiBold();
                fila.AutoItem().Text(d.NumeroProceso).FontSize(12).SemiBold().FontColor(Colors.Blue.Darken3);
            });
        });
    }

    private static void DatosDeLaSolicitud(IContainer contenedor, InformeDatos d)
    {
        contenedor.Column(col =>
        {
            Titulo(col, "1. Datos de la solicitud");

            col.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(130);
                    c.RelativeColumn();
                });

                void Fila(string etiqueta, string valor)
                {
                    tabla.Cell().Element(CeldaDato).Text(etiqueta).SemiBold();
                    tabla.Cell().Element(CeldaDato).Text(valor);
                }

                Fila("Número de proceso", d.NumeroProceso);
                Fila("Estudiante", d.EstudianteCorreo);
                Fila("Carrera de origen", d.CarreraOrigen);
                Fila("Carrera de destino", d.CarreraDestino);

                if (EsExterno(d))
                {
                    Fila("Universidad de origen", d.UniversidadOrigen ?? "—");
                    Fila("Universidad de destino", d.UniversidadDestino ?? "—");
                }

                Fila("Módulo de análisis", EsExterno(d) ? "Agente externo" : "Agente interno");
                Fila("Fecha de registro", Fecha(d.FechaRegistro));
                Fila("Fecha del análisis", Fecha(d.FechaAnalisis));
            });
        });
    }

    private static void TablaDeAnalisis(IContainer contenedor, InformeDatos d)
    {
        contenedor.Column(col =>
        {
            Titulo(col, "2. Análisis por asignatura (propuesta del sistema)");

            if (d.Asignaturas.Count == 0)
            {
                col.Item().Text("La solicitud no tiene asignaturas analizadas.").Italic();
                return;
            }

            col.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.4f);   // asignatura de origen
                    c.RelativeColumn(2.4f);   // asignatura de destino
                    c.ConstantColumn(36);     // % contenido
                    c.ConstantColumn(36);     // % profundidad
                    c.ConstantColumn(36);     // % horas
                    c.RelativeColumn(1.7f);   // decisión
                    c.RelativeColumn(4f);     // justificación
                });

                tabla.Header(h =>
                {
                    h.Cell().Element(CeldaCabecera).Text("Asignatura de origen");
                    h.Cell().Element(CeldaCabecera).Text("Asignatura de destino");
                    h.Cell().Element(CeldaCabecera).Text("% Cont.");
                    h.Cell().Element(CeldaCabecera).Text("% Prof.");
                    h.Cell().Element(CeldaCabecera).Text("% Horas");
                    h.Cell().Element(CeldaCabecera).Text("Decisión");
                    h.Cell().Element(CeldaCabecera).Text("Justificación");
                });

                foreach (var a in d.Asignaturas)
                {
                    tabla.Cell().Element(CeldaCuerpo).Text(a.AsignaturaOrigen);
                    tabla.Cell().Element(CeldaCuerpo).Text(a.AsignaturaDestino);
                    tabla.Cell().Element(CeldaCuerpo).Text($"{a.PorcentajeContenido} %");
                    tabla.Cell().Element(CeldaCuerpo).Text($"{a.PorcentajeProfundidad} %");
                    tabla.Cell().Element(CeldaCuerpo).Text($"{a.PorcentajeHoras} %");
                    tabla.Cell().Element(CeldaCuerpo).Text(Etiqueta(a.Decision)).SemiBold().FontColor(ColorDecision(a.Decision));
                    tabla.Cell().Element(CeldaCuerpo).Text(a.Justificacion);
                }
            });
        });
    }

    private static void InformeDelCoordinador(IContainer contenedor, InformeDatos d)
    {
        contenedor.Column(col =>
        {
            Titulo(col, "3. Decisión y observación del Coordinador");

            if (d.FechaInforme is null)
            {
                col.Item().Text("El Coordinador todavía no ha emitido su informe técnico.").Italic();
                return;
            }

            col.Item().PaddingBottom(4).Text(t =>
            {
                t.Span("Informe emitido por: ").SemiBold();
                t.Span(d.CoordinadorCorreo ?? "—");
                t.Span("     Fecha: ").SemiBold();
                t.Span(Fecha(d.FechaInforme.Value));
            });

            col.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3.2f);   // asignaturas
                    c.RelativeColumn(1.7f);   // decisión
                    c.RelativeColumn(5f);     // observación
                });

                tabla.Header(h =>
                {
                    h.Cell().Element(CeldaCabecera).Text("Asignatura");
                    h.Cell().Element(CeldaCabecera).Text("Decisión del Coordinador");
                    h.Cell().Element(CeldaCabecera).Text("Observación");
                });

                foreach (var a in d.Asignaturas)
                {
                    tabla.Cell().Element(CeldaCuerpo).Text($"{a.AsignaturaOrigen} → {a.AsignaturaDestino}");
                    tabla.Cell().Element(CeldaCuerpo).Text(Etiqueta(a.DecisionCoordinador)).SemiBold().FontColor(ColorDecision(a.DecisionCoordinador));
                    tabla.Cell().Element(CeldaCuerpo).Text(string.IsNullOrWhiteSpace(a.ObservacionCoordinador) ? "—" : a.ObservacionCoordinador);
                }
            });
        });
    }

    private static void ResolucionDelConsejo(IContainer contenedor, InformeResolucion r)
    {
        contenedor.Column(col =>
        {
            Titulo(col, "4. Resolución del Consejo Directivo");

            col.Item().Text(t =>
            {
                t.Span("Resolución N.º ").SemiBold();
                t.Span(r.NumeroResolucion);
                t.Span("     Fecha: ").SemiBold();
                t.Span(Fecha(r.Fecha));
            });

            if (r.Decisiones.Count > 0)
            {
                col.Item().PaddingTop(4).Table(tabla =>
                {
                    tabla.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5f);
                        c.RelativeColumn(2f);
                    });

                    tabla.Header(h =>
                    {
                        h.Cell().Element(CeldaCabecera).Text("Asignatura");
                        h.Cell().Element(CeldaCabecera).Text("Decisión final");
                    });

                    foreach (var decision in r.Decisiones)
                    {
                        tabla.Cell().Element(CeldaCuerpo).Text(decision.Asignatura);
                        tabla.Cell().Element(CeldaCuerpo).Text(Etiqueta(decision.DecisionFinal)).SemiBold().FontColor(ColorDecision(decision.DecisionFinal));
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(r.Observacion))
            {
                col.Item().PaddingTop(4).Text(t =>
                {
                    t.Span("Observación: ").SemiBold();
                    t.Span(r.Observacion);
                });
            }
        });
    }

    private static void AvisoOficial(IContainer contenedor)
    {
        contenedor
            .Border(0.8f).BorderColor(Colors.Orange.Darken2)
            .Background(Colors.Orange.Lighten5)
            .Padding(8)
            .Text("Aviso: este informe lo genera el sistema HomologaUTA a partir del análisis registrado. " +
                  "No reemplaza el trámite oficial de homologación: la decisión final corresponde a los " +
                  "órganos competentes de la Universidad Técnica de Ambato.")
            .FontSize(9);
    }

    private static void PiePagina(IContainer contenedor, InformeDatos d, DateTime generado)
    {
        contenedor.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(3).Row(fila =>
            {
                fila.RelativeItem()
                    .Text($"HomologaUTA · {d.NumeroProceso} · Generado el {generado.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} UTC")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);

                fila.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });
    }

    // ---------------------------------------------------------------
    // Ayudas de estilo y formato
    // ---------------------------------------------------------------

    private static void Titulo(ColumnDescriptor col, string texto)
    {
        col.Item().PaddingBottom(3).Text(texto).FontSize(11).SemiBold().FontColor(Colors.Blue.Darken3);
    }

    private static IContainer CeldaDato(IContainer c) => c.PaddingVertical(2);

    private static IContainer CeldaCabecera(IContainer c) =>
        c.Background(Colors.Blue.Darken3).Padding(4)
         .DefaultTextStyle(x => x.FontColor(Colors.White).SemiBold().FontSize(8));

    private static IContainer CeldaCuerpo(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);

    private static bool EsExterno(InformeDatos d) =>
        string.Equals(d.Modulo, "EXTERNO", StringComparison.OrdinalIgnoreCase);

    private static string Fecha(DateTime fecha) =>
        fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Etiqueta(string? decision) => decision switch
    {
        "HOMOLOGABLE" => "Homologable",
        "NO_HOMOLOGABLE" => "No homologable",
        "REQUIERE_REVISION" => "Requiere revisión",
        null or "" => "—",
        _ => decision
    };

    private static Color ColorDecision(string? decision) => decision switch
    {
        "HOMOLOGABLE" => Colors.Green.Darken2,
        "NO_HOMOLOGABLE" => Colors.Red.Darken2,
        _ => Colors.Orange.Darken2
    };
}

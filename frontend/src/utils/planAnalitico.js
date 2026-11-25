// Lee un plan analítico o un sílabo de la UTA a partir de su texto con columnas (pdftotext -layout)
// y devuelve lo que usa la IA: el objetivo y los contenidos por unidad, y los resultados de
// aprendizaje. Es el mismo método con el que se cargaron los planes de MA-Proyecto.

const limpiar = (t) => t.replace(/\s+/g, " ").replace(/\s+([.,;:])/g, "$1").trim();
const columnaIzq = (linea, desde = 0) => linea.slice(desde).split(/\s{3,}/).filter(Boolean)[0] || "";

export function leerPlanAnalitico(texto) {
  const lineas = String(texto || "")
    .split(/\r?\n/)
    .filter((l) => !/P[áa]gina \d+\/\d+/.test(l) || l.trim().length > 20);

  const carrera = limpiar((texto.match(/CARRERA DE:\s*(.+)/) || [])[1] || "");
  const iNombre = lineas.findIndex((l) => /Nombre de la asignatura/.test(l));
  const asignatura = iNombre >= 0 ? limpiar(columnaIzq(lineas[iNombre + 1] || "")) : "";

  const partes = [];
  const objGeneral = texto.match(/Objetivo general de la asignatura\s*([\s\S]*?)(?:Objetivos espec[ií]ficos|$)/);
  if (objGeneral && limpiar(objGeneral[1]).length > 10 && !/NOTA:|P[áa]gina/.test(limpiar(objGeneral[1]).slice(0, 30)))
    partes.push(`Objetivo general: ${limpiar(objGeneral[1])}`);

  const resultados = [];
  const iTabla = lineas.findIndex((l) => /^\s*III CONTENIDO\s*$/.test(l));

  if (iTabla >= 0) {
    // Plan analítico: tabla Unidades curriculares | temáticas | contenidos | horas | resultados
    const cab = lineas[iTabla + 2] || "";
    const colTem = cab.indexOf("Unidades tem");
    const colHoras = cab.indexOf("No horas");
    let unidad = null;
    const unidades = [];
    for (let i = iTabla + 3; i < lineas.length; i++) {
      const l = lineas[i];
      if (/Resultados del aprendizaje de la asignatura|^\s*IV METODOLOG/.test(l)) break;
      const m = l.match(/^\s*(U\.\s*\d+)/);
      if (m) {
        unidad = { nombre: m[1].replace(/\s/g, ""), tema: [], res: [] };
        unidades.push(unidad);
      }
      if (!unidad) continue;
      const centro = l.slice(colTem > 0 ? colTem : 0, colHoras > 0 ? colHoras : undefined);
      const derecha = colHoras > 0 ? l.slice(colHoras).replace(/^\s*\d+\s*/, "") : "";
      if (centro.trim()) unidad.tema.push(centro.trim());
      if (derecha.trim() && !/^aprendizaje$/.test(derecha.trim())) unidad.res.push(derecha.trim());
    }
    for (const u of unidades) {
      const tema = limpiar(u.tema.join(" ")).replace(/\s+-\s*/g, "; ").replace(/^-\s*/, "");
      partes.push(`${u.nombre} ${tema}`);
      if (u.res.length) resultados.push(limpiar(u.res.join(" ")).replace(/^aprendizaje\s+/, ""));
    }
  } else {
    // Sílabo: por unidad, el objetivo (línea U.n), los temas "n.m.- ..." y el resultado de aprendizaje
    let unidad = null;
    const unidades = [];
    const lHoras = lineas.find((l) => /Horas Clase/.test(l)) || "";
    const colHorasClase = lHoras.indexOf("Horas Clase") > 0 ? lHoras.indexOf("Horas Clase") : 200;
    for (const l of lineas) {
      const u = l.match(/^U\.(\d+)\s{2,}(.+)$/);
      if (u) {
        unidad = { nombre: `U.${u[1]}`, objetivo: limpiar(u[2]), temas: [] };
        unidades.push(unidad);
        continue;
      }
      const t = l.match(/^\s*(\d+\.\d+)\.?\s*-?\s*(.+)$/);
      if (unidad && t) {
        const tema = limpiar(columnaIzq(l.slice(0, colHorasClase).replace(/^\s*\d+\.\d+\.?\s*-?\s*/, "")));
        if (tema.length >= 3 && !/^\d+$/.test(tema)) unidad.temas.push(tema);
        continue;
      }
      const r = l.match(/^\s*Resultados de aprendizaje:\s*(.+)$/);
      if (r) resultados.push(limpiar(r[1]));
    }
    for (const u of unidades) partes.push(`${u.nombre} ${u.objetivo}. Temas: ${u.temas.join("; ")}`);
  }

  const unidades = partes.filter((p) => /^U\.\d+/.test(p)).length;
  return {
    carrera,
    asignatura,
    contenido: partes.join("\n"),
    resultados: resultados.join("\n"),
    unidades,
    // Plantilla sin llenar ("Borrador" sin unidades): no sirve para comparar.
    vacio: unidades === 0,
  };
}

/**
 * Tamaño legible del PDF. El backend devuelve TamanoBytes leyendo el archivo físico;
 * cuando no está disponible se conserva el texto que ya traiga el documento.
 */
export function formatearTamano(documento) {
  const bytes = Number(documento?.tamanoBytes ?? documento?.TamanoBytes);

  if (Number.isFinite(bytes) && bytes >= 0) {
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  return documento?.tamano || documento?.Tamano || "-";
}

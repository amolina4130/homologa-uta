# Medición de tiempos del agente — Sprint 3 (TE-06)

**Issue:** #163 — TE-06
**Historia de usuario:** HU-23
**Responsable:** Katherine Moyolema
**Fecha:** 19/11/2026
**Entorno de prueba:** Docker Compose local (Windows), agente con memoria cargada, preselección activa y planes analíticos reales.

## 1. Objetivo
Medir el tiempo de respuesta del análisis completo del agente (incluyendo memoria, preselección y planes reales) para cada asignatura, verificando que cumpla el RNF-01 (<60 s por asignatura), incluso en casos con combinación de dos materias.

## 2. Casos de prueba (mínimo 5, al menos uno combinado)

| # | Caso | Asignatura(s) | Tiempo de respuesta (s) | ¿Cumple <60 s? |
|---|------|---------------|------------------------|----------------|
| 1 | Individual | Cálculo Diferencial | 42 | ✅ |
| 2 | Individual | Programación Orientada a Objetos | 38 | ✅ |
| 3 | Individual | Base de Datos | 51 | ✅ |
| 4 | Individual | Física | 47 | ✅ |
| 5 | **Combinado** | **Cálculo Integral + Física** | **58** | ✅ |

> Los tiempos fueron tomados con el agente ya cargado en memoria (sin contar el arranque del contenedor) y usando los planes analíticos reales de las carreras de la demostración.

## 3. Tiempo de extracción del récord (medido por separado)

| Paso | Tiempo (s) |
|------|------------|
| Extracción del récord académico (OCR + parseo) | 12 |
| Carga de memoria y preselección | 4 |
| **Total extracción + preparación** | **16** |

## 4. Resultados y conclusiones
- Todos los casos individuales respondieron en menos de 60 s.
- El caso combinado (Cálculo Integral + Física) respondió en 58 s, cumpliendo justo con el límite.
- La extracción del récord representa ~16 s del total, por lo que el análisis del agente en sí toma entre 25 y 40 s adicionales.
- **Decisión:** Se acepta el rendimiento como satisfactorio para el Sprint 3. Se recomienda para futuras iteraciones cachear los planes analíticos para reducir el tiempo en casos combinados.

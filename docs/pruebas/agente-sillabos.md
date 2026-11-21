# Pruebas del Agente con Planes Analíticos - TA-14.4

## Información del entorno
- **Modelo utilizado:** llama3
- **Fecha de prueba:** 14/11/2026
- **Responsable:** Katherine Moyolema
- **Tiempo máximo permitido (RNF-01):** 60 segundos

## Resultados de las pruebas

### Caso 1: Álgebra Lineal (3 vs 2 créditos) - NO HOMOLOGABLE
- **Descripción:** Comparación de la asignatura "Álgebra Lineal" de origen (3 créditos) con la de destino (2 créditos).
- **Porcentajes devueltos por el agente:**
  - Contenido: 90 %
  - Profundidad: 85 %
  - Carga Horaria: 66 % (cercano al 66 % esperado por la diferencia de créditos)
- **Decisión esperada:** NO_HOMOLOGABLE (porque el porcentaje de carga horaria es menor al 80 %).
- **Tiempo de respuesta:** 12.4 segundos.
- **¿Cumple el RNF-01 (< 60s)?:** Sí.
- **Evidencia visual:** 
  ![Captura Caso 1](capturas/TA-14.4-caso1.png)

### Caso 2: Programación (4 vs 4 créditos) - HOMOLOGABLE
- **Descripción:** Comparación de la asignatura "Programación" de origen (4 créditos) con la de destino (4 créditos).
- **Porcentajes devueltos por el agente:**
  - Contenido: 95 %
  - Profundidad: 90 %
  - Carga Horaria: 100 %
- **Decisión esperada:** HOMOLOGABLE (porque los tres porcentajes son iguales o mayores al 80 %).
- **Tiempo de respuesta:** 14.1 segundos.
- **¿Cumple el RNF-01 (< 60s)?:** Sí.
- **Evidencia visual:** 
  ![Captura Caso 2](capturas/TA-14.4-caso2.png)

## Conclusión
El agente respondió a los 2 casos de prueba con un **tiempo promedio de 13.25 segundos**, cumpliendo satisfactoriamente con el RNF-01 (menos de 60 segundos). Además, se verificó que la lógica del código C# aplica correctamente el umbral del 80 % (RN-03) para rechazar el caso de Álgebra Lineal (carga horaria 66 %) y aceptar el caso de Programación.
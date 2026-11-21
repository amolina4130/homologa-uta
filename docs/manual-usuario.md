# Manual de Usuario — HomologaUTA

Sistema de Homologación de la Universidad Técnica de Ambato (UTA).
Este manual describe los pasos principales para cada rol. Las capturas de pantalla (evidencias) se integran en el **documento final del proyecto**, no en este repositorio.

---

## 1. Introducción
HomologaUTA permite a los estudiantes registrar solicitudes de homologación, a los coordinadores revisarlas y aprobarlas, y a los administradores gestionar carreras, mallas, usuarios y la memoria del agente.

## 2. Requisitos previos
- Credenciales institucionales.
- Navegador actualizado (Chrome, Edge o Firefox).
- El sistema debe estar levantado (ver `docs/manual-despliegue.md`).

## 3. Acceso al sistema
1. Abrir el navegador en `http://localhost:3000`.
2. Ingresar correo institucional y contraseña.
3. El sistema redirige al panel según el rol.

> Evidencia: se adjunta en el documento final (HU-02).

---

## 4. Rol Estudiante

### 4.1 Registrar una solicitud de homologación (HU-04)
1. Ir a **Nueva solicitud**.
2. Seleccionar carrera y malla.
3. Agregar las materias aprobadas que se desean homologar.
4. Adjuntar documentos de respaldo.
5. Enviar la solicitud.

> Evidencia: se adjunta en el documento final (HU-04).

### 4.2 Revisar materias aprobadas
1. Ir a **Mis materias**.
2. Ver el listado de materias aprobadas y su estado.
3. Verificar que coincidan con el récord académico.

> Evidencia: se adjunta en el documento final (HU-12).

### 4.3 Consultar el estado de una solicitud
1. Ir a **Mis solicitudes**.
2. Seleccionar la solicitud.
3. Ver el estado (Pendiente, En revisión, Aprobada, Rechazada) y el informe generado.

> Evidencia: se adjunta en el documento final (HU-13).

---

## 5. Rol Coordinador

### 5.1 Revisar solicitudes pendientes (HU-14)
1. Ir a **Solicitudes pendientes**.
2. Filtrar por carrera o malla.
3. Abrir una solicitud para revisar el detalle y los documentos.

> Evidencia: se adjunta en el documento final (HU-14).

### 5.2 Validar reglas normativas (HU-15)
1. Abrir la solicitud.
2. Revisar las reglas aplicadas por el sistema (créditos, prerrequisitos, etc.).
3. Aprobar o rechazar con observaciones.

> Evidencia: se adjunta en el documento final (HU-15).

### 5.3 Consultar el informe de homologación (HU-16)
1. Ir a **Informes**.
2. Seleccionar la solicitud aprobada.
3. Descargar o visualizar el informe generado por el agente.

> Evidencia: se adjunta en el documento final (HU-16).

---

## 6. Rol Administrador

### 6.1 Gestionar carreras y mallas (HU-03)
1. Ir a **Carreras y mallas**.
2. Crear, editar o desactivar carreras y mallas.
3. Asignar materias a cada malla.

> Evidencia: se adjunta en el documento final (HU-03).

### 6.2 Gestionar usuarios y roles (HU-02)
1. Ir a **Usuarios**.
2. Crear usuarios y asignar roles (Estudiante, Coordinador, Administrador).
3. Restablecer contraseñas si es necesario.

> Evidencia: se adjunta en el documento final (HU-02).

### 6.3 Cargar y actualizar la memoria del agente (HU-19)
1. Ir a **Memoria del agente**.
2. Ejecutar **Carga inicial** (`POST /memoria/carga`).
3. Verificar el estado con **Consultar estado** (`GET /memoria/estado`).
4. Para actualizar, repetir la carga; el sistema detecta cambios por huella (hash).

> Evidencia: se adjunta en el documento final (HU-19).

### 6.4 Ver el estado del sistema (HU-01)
1. Ir a **Estado del sistema** (`/estado`).
2. Verificar que todos los microservicios estén en verde.
3. Revisar Swagger y `/health` de cada servicio si hay alertas.

> Evidencia: se adjunta en el documento final (HU-01).

---

## 7. Evidencias de los tres sprints

Las evidencias se organizan en el **documento final** con la numeración "Evidencia 19.x.y", según lo acordado por el equipo.

| Sprint | Historias con evidencia |
|---|---|
| Sprint 1 | HU-01, HU-02, HU-03, HU-04 |
| Sprint 2 | HU-12 a HU-18 |
| Sprint 3 | HU-19 a HU-22 |

---

## 8. Glosario
- **Homologación:** reconocimiento de materias aprobadas en otra malla o institución.
- **Malla:** plan de estudios de una carrera.
- **Memoria del agente:** base de conocimiento que usa el agente para analizar solicitudes.
- **Huella (hash):** resumen que permite detectar si la memoria cambió.

---

## 9. Contacto
Equipo HomologaUTA — FISEI, Universidad Técnica de Ambato.
Docente: Ing. Hernan Naranjo, Mg.
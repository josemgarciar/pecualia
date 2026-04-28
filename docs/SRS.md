# Software Requirements Specification (SRS)
## Plataforma SaaS para la Gestión Digital de Libros de Registro Ganadero

**Versión:** 1.0  
**Autor:** José Manuel García Rosa  
**Fecha:** 2026-04-17  

---

## 1. Introducción

### 1.1 Propósito
Este documento define los requisitos del software para una **plataforma SaaS** orientada a la **digitalización integral del libro de registro ganadero**, que permite la gestión centralizada de explotaciones, animales, movimientos y demás obligaciones oficiales exigidas por la normativa vigente.

### 1.2 Alcance
El sistema, denominado en adelante **"la plataforma"**, es una aplicación web accesible vía navegador que permite:
- Gestionar perfiles de usuarios (gestores y ganaderos).
- Administrar explotaciones ganaderas (ovino-caprino y porcino).
- Registrar y consultar animales con identificación individual.
- Importar altas y bajas masivas de animales mediante archivos TXT generados por lectores de crotales.
- Gestionar guías de movimiento de animales entre explotaciones.
- Registrar nacimientos, muertes y autorreposiciones.
- Generar libros de explotación digitales con todas las hojas oficiales.
- Gestionar suscripciones al servicio (modelo B2B/B2C).

### 1.3 Definiciones y Acrónimos

| Término | Definición |
|---|---|
| **REGA** | Registro General de Explotaciones Ganaderas |
| **RIIA** | Registro de Identificación Individual de Animales |
| **REMO** | Registro de Movimientos de Ganado |
| **SITRAN** | Sistema Integral de Trazabilidad Animal |
| **OVZ** | Oficina Veterinaria de Zona |
| **SaaS** | Software as a Service |
| **B2B** | Business to Business |
| **RF** | Requisito Funcional |
| **RNF** | Requisito No Funcional |
| **TXT de crotales** | Archivo de texto generado por lectores de crotales, con una identificación animal por línea |

### 1.4 Referencias
- Real Decreto 479/2004 (trazabilidad animal)
- Ley 8/2003 (Ley de Sanidad Animal)
- Normativa de la Junta de Extremadura sobre libro de registro electrónico
- Documento TFG: "Diseño y desarrollo de una plataforma SaaS para la gestión digital de libros de registro ganadero" (José Manuel García Rosa, curso 2025/2026)

---

## 2. Descripción General

### 2.1 Perspectiva del Producto
La plataforma se enmarca dentro del ecosistema normativo español de trazabilidad animal (SITRAN, REGA, RIIA, REMO). No pretende sustituir los sistemas oficiales de la administración, sino complementarlos ofreciendo una herramienta digital que **simplifica, unifica y automatiza** la gestión del registro ganadero.

### 2.2 Actores del Sistema

| Actor | Descripción |
|---|---|
| **Gestor** | Profesional (asesoría/técnico) que gestiona múltiples ganaderos y sus explotaciones. Accede al sistema para administrar clientes, configurar explotaciones y generar documentación. |
| **Ganadero** | Titular de una o más explotaciones ganaderas. Puede gestionar sus propias explotaciones de forma autónoma o a través de un gestor. |

### 2.3 Restricciones
- La plataforma debe ser accesible vía navegador web moderno (Chrome, Firefox, Safari, Edge).
- Debe soportar un modelo de suscripción mensual.
- Debe cumplir con la normativa de protección de datos (RGPD/LOPDGDD).
- El libro de registro digital debe cumplir con los datos mínimos establecidos por la normativa para cada especie.

### 2.4 Supuestos y Dependencias
- Los usuarios disponen de conexión a Internet.
- La información oficial de razas, causas de alta/baja y clasificaciones zootécnicas se mantiene según tablas oficiales vigentes.
- El sistema soporta inicialmente las especies **ovino-caprino** y **porcino**.
- Los lectores de crotales proporcionan archivos TXT con una identificación animal por línea para operaciones masivas.

---

## 3. Requisitos Funcionales

### RF-01: Inicio de Sesión / Registro
- **Actor:** Gestor, Ganadero
- **Descripción:** El sistema debe permitir a los usuarios registrarse con email, nombre, apellidos, nombre de usuario y contraseña. Debe permitir iniciar sesión con las credenciales registradas.
- **Entidades:** `User`
- **Prioridad:** Alta

### RF-02: Gestionar Suscripción al Sistema
- **Actor:** Gestor
- **Descripción:** El sistema debe permitir gestionar planes de suscripción (tipo de plan, estado, fechas, renovación automática).
- **Entidades:** `Subscription`, `User`
- **Prioridad:** Alta

### RF-03: Registrar Perfil Ganadero
- **Actor:** Gestor
- **Descripción:** El sistema debe permitir registrar el perfil de un ganadero con sus datos de titular (NIF/CIF, domicilio, localidad, código postal, provincia, teléfono).
- **Entidades:** `Farmer`
- **Prioridad:** Alta
- **Relación:** Extiende RF-01

### RF-04: Gestionar Perfil de Gestor
- **Actor:** Gestor
- **Descripción:** El sistema debe permitir al gestor administrar su propio perfil y gestionar la relación con los ganaderos que administra.
- **Entidades:** `Manager`
- **Prioridad:** Alta
- **Relación:** Extiende RF-01

### RF-05: Gestionar Explotación Ganadera
- **Actor:** Ganadero
- **Descripción:** El sistema debe permitir crear, editar y consultar explotaciones ganaderas con todos sus datos oficiales (código REGA, dirección, clasificación zootécnica, régimen, capacidad, coordenadas UTM, estado activa/inactiva).
- **Entidades:** `Livestock_farm`
- **Prioridad:** Alta

### RF-06: Registrar Movimiento
- **Actor:** Ganadero
- **Descripción:** El sistema debe permitir registrar guías de movimiento de animales entre explotaciones, incluyendo datos de transporte, visado y animales asociados. Dentro de este flujo debe permitir altas y bajas masivas de animales mediante TXT de crotales. Debe impactar automáticamente el balance de la explotación.
- **Entidades:** `MovementCertificate`, `Animal`, `Balance`
- **Prioridad:** Alta
- **Relación:** Extiende RF-05
- **Criterios:** El sistema debe permitir subir un archivo `.txt` con un crotal por línea, elegir operación de alta masiva o baja masiva, previsualizar los animales encontrados, marcar duplicados, identificaciones ya existentes, crotales no encontrados o formatos inválidos, confirmar antes de aplicar cambios, actualizar animales y balance, y mostrar un informe de registros procesados y rechazados.

### RF-07: Registrar Muertes
- **Actor:** Ganadero
- **Descripción:** El sistema debe permitir registrar la muerte de animales, actualizando su estado de baja con causa "M" (Muerte) y el motivo específico. Debe impactar el balance.
- **Entidades:** `Animal`, `Balance`
- **Prioridad:** Alta
- **Relación:** Extiende RF-05

### RF-08: Registrar Autoreposición
- **Actor:** Ganadero
- **Descripción:** El sistema debe permitir registrar autorreposiciones (cambio de categoría de animales dentro de la misma explotación). Debe actualizar alta/baja y el balance.
- **Entidades:** `Animal`, `Balance`
- **Prioridad:** Alta
- **Relación:** Extiende RF-05

### RF-09: Registrar Nacimientos
- **Actor:** Ganadero
- **Descripción:** El sistema debe permitir registrar nacimientos vinculados al animal madre (y opcionalmente al padre), con fecha, número de crías y peso. Las crías deben darse de alta como animales nuevos con causa "N".
- **Entidades:** `AnimalBirth`, `Animal`, `Balance`
- **Prioridad:** Alta
- **Relación:** Extiende RF-05

### RF-10: Generar Libro de Explotación
- **Actor:** Ganadero
- **Descripción:** El sistema debe generar el libro de registro oficial de explotación incluyendo: portada (datos titular y explotación), hoja de identificación individual, hoja de censo total, hoja de balance, hoja de incidencias y hoja de control de inspecciones.
- **Entidades:** `ExploitationBook`, `Census`, `Balance`, `Incident`, `Inspection`
- **Prioridad:** Alta
- **Relación:** Extiende RF-05

### RF-11: Consulta de Animales
- **Actor:** Ganadero
- **Descripción:** El sistema debe permitir consultar la información completa de cualquier animal, incluyendo historial de vacunaciones, movimientos, nacimientos e incidencias.
- **Entidades:** `Animal`, `OvinoCaprino`, `Porcino`, `Vaccination`
- **Prioridad:** Alta

---

## 4. Requisitos No Funcionales

### RNF-01: Seguridad
- Las contraseñas deben almacenarse como hash (bcrypt o similar).
- Las comunicaciones deben usar HTTPS/TLS.
- Cumplimiento con RGPD/LOPDGDD.

### RNF-02: Disponibilidad
- El sistema debe estar disponible un 99.5% del tiempo (excluyendo mantenimientos programados).

### RNF-03: Escalabilidad
- La arquitectura debe soportar crecimiento horizontal (múltiples instancias).
- Modelo SaaS multi-tenant.

### RNF-04: Usabilidad
- Interfaz responsive adaptada a dispositivos móviles, tablets y escritorio.
- Flujos de trabajo intuitivos para usuarios no técnicos del sector ganadero.

### RNF-05: Rendimiento
- Tiempo de respuesta < 2 segundos para operaciones de consulta.
- Generación de libro de explotación < 10 segundos.
- Previsualización de importaciones TXT de hasta 1.000 crotales < 5 segundos.

### RNF-06: Mantenibilidad
- Código fuente versionado con Git.
- Cobertura de tests unitarios ≥ 80% en backend.

---

## 5. Modelo de Datos

El modelo de datos completo se encuentra documentado en el archivo `diagrama_entidades.md`, que incluye:

- **20 entidades** organizadas en 4 jerarquías de herencia
- Diagrama de clases Mermaid con todas las relaciones
- Detalle de cada entidad con tipos, descripciones y origen de cada atributo
- Enumeraciones de referencia

### Resumen de entidades:

| # | Entidad | Tipo | Descripción |
|---|---|---|---|
| 1 | `User` | Abstracta | Clase base de usuarios |
| 2 | `Manager` | Hereda User | Gestor del sistema |
| 3 | `Subscription` | Nueva | Suscripción al servicio |
| 4 | `Farmer` | Hereda User | Titular de explotaciones |
| 5 | `Livestock_farm` | Entidad | Explotación ganadera |
| 6 | `ExploitationBook` | Nueva | Libro de registro oficial |
| 7 | `Animal` | Abstracta | Datos comunes de animal |
| 8 | `OvinoCaprino` | Hereda Animal | Ovino-caprino |
| 9 | `Porcino` | Hereda Animal | Porcino |
| 10 | `AnimalBirth` | Entidad | Registro de nacimientos |
| 11 | `Vaccination` | Entidad | Vacunaciones |
| 12 | `MovementCertificate` | Entidad | Guía de movimiento |
| 13 | `Census` | Abstracta | Censo de animales |
| 14 | `CensusOvinoCaprino` | Hereda Census | Censo ovino-caprino |
| 15 | `CensusPorcino` | Hereda Census | Censo porcino |
| 16 | `Balance` | Abstracta | Balance de animales |
| 17 | `BalanceOvinoCaprino` | Hereda Balance | Balance ovino-caprino |
| 18 | `BalancePorcino` | Hereda Balance | Balance porcino |
| 19 | `Incident` | Entidad | Incidencias |
| 20 | `Inspection` | Entidad | Inspecciones |

---

## 6. Stack Tecnológico

| Capa | Tecnología |
|---|---|
| **Frontend** | Por definir (React, Angular, etc.) |
| **Backend** | Por definir (Spring Boot, Node.js, etc.) |
| **Base de datos** | PostgreSQL |
| **Autenticación** | JWT / OAuth2 |
| **Despliegue** | Docker / Cloud (por definir) |
| **Testing** | JUnit / Jest (según stack) |
| **CI/CD** | GitHub Actions (por definir) |
| **Modelado** | Enterprise Architect + SofIA |

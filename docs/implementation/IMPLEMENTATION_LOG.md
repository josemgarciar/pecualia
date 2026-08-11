# Pecualia Implementation Log

## Contexto acordado

- Se implementa el plan incremental aprobado para Pecualia.
- El backend actual parte de un listener TCP minimo y debe convertirse en ASP.NET Core Web API.
- PostgreSQL es la fuente de verdad; el esquema inicial esta en `db/init/001_schema.sql`.
- Si un gestor crea un ganadero, ese ganadero debe ser una cuenta real accesible tras activacion por correo.
- Un email no puede representar una cuenta fantasma ni duplicar otra cuenta existente.
- El enlace disponible es un prototipo publicado en Figma Sites; sirve para inspeccion visual con Playwright, no para MCP de nodos Figma.
- Adicionalmente se han aportado dos artefactos locales para paridad visual:
  - `src.zip`: export del codigo de la interfaz generada en Figma
  - `guidelines.zip`: directrices visuales utilizadas en esa generacion

## Prioridad de implementacion

1. Base backend real: API, configuracion, PostgreSQL y estructura por capas.
2. Identidad: login, registro, creacion de ganadero por gestor, token de activacion y reenvio.
3. Frontend base: rutas, shell, auth, ganaderos, explotaciones y activacion.
4. Operaciones ganaderas: animales, movimientos, TXT, nacimientos, muertes, censos, balances.
5. Libro de explotacion, incidencias, inspecciones, suscripcion y ajustes.

## Estado actual

- Inicio de implementacion: 2026-04-23.
- Ultima actualizacion de continuidad: 2026-04-24.

## Cambios aplicados

- Backend convertido a ASP.NET Core Web API real sobre `net8.0`.
- `NuGet.config` corregido para permitir restaurar desde `nuget.org`.
- `Pecualia.sln` corregida para apuntar a `src/backend/Pecualia.Api/Pecualia.Api.csproj`.
- Añadidas configuraciones de:
  - PostgreSQL
  - JWT
  - activacion de cuentas
  - envio de correo en modo fichero local
  - CORS para Vite
- Implementadas entidades y mapeos EF Core para:
  - `app_user`
  - `manager`
  - `farmer`
  - `subscription`
  - `livestock_farm`
  - `account_activation_token`
  - `animal` minimo para contadores
- Actualizado `db/init/001_schema.sql` para soportar:
  - cuentas con activacion pendiente
  - rol en usuario
  - datos profesionales de gestor
  - estado y tipo de persona en ganadero
  - tokens de activacion
- Implementados servicios backend:
  - hashing bcrypt
  - emision de JWT
  - generacion y validacion de tokens de activacion
  - envio de correo a outbox en fichero
  - registro/login
  - alta de ganadero por gestor
  - activacion de cuenta
  - reenvio de activacion
  - alta y listado de explotaciones
- Endpoints implementados:
  - `POST /api/auth/register/manager`
  - `POST /api/auth/register/farmer`
  - `POST /api/auth/login`
  - `POST /api/auth/activate-account`
  - `POST /api/auth/resend-activation`
  - `GET /api/auth/me`
  - `GET/POST /api/farmers`
  - `PUT /api/farmers/{id}`
  - `POST /api/farmers/{id}/send-activation`
  - `GET/POST /api/farms`
  - `GET /api/farms/{id}/summary`
  - `GET /api/dashboard/summary`
- Frontend reestructurado en modulos:
  - `app`
  - `shared/api`
  - `shared/auth`
  - `features/auth`
  - `features/dashboard`
  - `features/farmers`
  - `features/farms`
  - `features/profile`
- Frontend implementado con:
  - React Router
  - contexto de autenticacion con `localStorage`
  - login
  - registro de gestor
  - registro autonomo de ganadero
  - activacion de cuenta
  - shell privada
  - dashboard
  - gestion de ganaderos
  - gestion de explotaciones
  - perfil
- Seccion de ganaderos ampliada para ajustarse al prototipo publicado:
  - filtros por busqueda, provincia y estado
  - tabla de ganaderos con conteo de explotaciones
  - panel lateral de detalle
  - alta en wizard de 3 pasos
  - edicion basica desde la ficha
  - accion de reenvio de invitacion
  - enlace a explotaciones con filtro por ganadero
- Modelo de ganadero ampliado con:
  - `second_surname`
  - `company_name`
  - `legal_representative`
- Contratos API ampliados:
  - `GET /api/farmers` soporta `search`, `province`, `status`
  - `GET /api/farmers/{id}` devuelve ficha completa y explotaciones asociadas
- Listado de explotaciones ampliado con `farmerId` para soportar filtros reales desde la ficha de ganadero.
- Iteracion visual de paridad sobre frontend actual:
  - shell lateral rediseñado con estilo del export de Figma
  - login rediseñado con hero fotografico, chips y tarjetas de acceso
  - dashboard rediseñado con KPIs, grafica y acciones rapidas
  - ganaderos ajustado para alinearse visualmente con el export
  - explotaciones rehecho para pasar de formulario lateral a filtros + cards + modal de alta
  - incorporadas dependencias `lucide-react` y `recharts`
- Referencia visual usada para la ultima iteracion:
  - codigo exportado extraido en `.tmp/figma-src/`
  - guidelines extraidas en `.tmp/figma-guidelines/`
  - capturas locales generadas para comparar:
    - `output-dashboard.png`
    - `output-farmers-fixed.png`
    - `output-farms-new.png`

## Validaciones realizadas

- `dotnet build Pecualia.sln` correcto.
- `npm run build` correcto.
- `docker compose up -d` correcto.
- Flujo validado manualmente:
  1. Registro de gestor.
  2. Login de gestor.
  3. Creacion de ganadero pendiente por gestor.
  4. Escritura del correo de activacion en `src/backend/Pecualia.Api/App_Data/outbox/`.
  5. Activacion de cuenta de ganadero.
  6. Login de ganadero.
  7. Creacion de explotacion por ganadero.
- UI validada en navegador local con DevTools:
  - `/login`
  - `/app/dashboard`
- UI validada en navegador local para ganaderos:
  1. Login de gestor.
  2. Apertura de `/app/farmers`.
  3. Carga de tabla y detalle lateral.
  4. Alta de persona juridica con wizard de 3 pasos.
  5. Seleccion automatica de la nueva ficha creada.
  6. Navegacion a `/app/farms?farmerId=...` con filtro aplicado.
- UI validada visualmente con capturas locales tras aplicar la referencia de `src.zip`:
  - `/login`
  - `/app/dashboard`
  - `/app/farmers`
  - `/app/farms`

## Archivos clave para retomar

- Shell y navegacion:
  - `src/frontend/src/app/AppRouter.jsx`
  - `src/frontend/src/styles.css`
- Auth:
  - `src/frontend/src/features/auth/AuthLayout.jsx`
  - `src/frontend/src/features/auth/LoginPage.jsx`
  - `src/frontend/src/features/auth/RegisterManagerPage.jsx`
  - `src/frontend/src/features/auth/RegisterFarmerPage.jsx`
  - `src/frontend/src/features/auth/ActivateAccountPage.jsx`
- Pantallas internas:
  - `src/frontend/src/features/dashboard/DashboardPage.jsx`
  - `src/frontend/src/features/farmers/FarmersPage.jsx`
  - `src/frontend/src/features/farms/FarmsPage.jsx`
  - `src/frontend/src/features/profile/ProfilePage.jsx`
- Backend relacionado con esta iteracion:
  - `src/backend/Pecualia.Api/Services/FarmerService.cs`
  - `src/backend/Pecualia.Api/Services/FarmService.cs`
  - `src/backend/Pecualia.Api/Contracts/Farmers/FarmerContracts.cs`
  - `src/backend/Pecualia.Api/Contracts/Farms/FarmContracts.cs`
  - `src/backend/Pecualia.Api/Endpoints/EndpointExtensions.cs`
  - `db/init/001_schema.sql`

## Decisiones tecnicas relevantes

- El correo se implementa en modo fichero local para desarrollo. Cada invitacion se guarda en `App_Data/outbox`.
- Las especies soportadas en frontend y backend quedan limitadas a `Ovine`, `Caprine` y `Porcine`.
- En base de datos `livestock_species`, `status` y `regime` de explotacion se persisten en minusculas para respetar el esquema SQL actual.
- Las cuentas creadas por gestor nacen con:
  - `username = null`
  - `password_hash = null`
  - `is_active = false`
  - `farmer.status = PendingActivation`
- En la iteracion actual de ganaderos solo se manejan dos estados reales:
  - `PendingActivation`
  - `Active`
- No se persisten en esta iteracion:
  - telefono alternativo
- El filtro cruzado desde ganaderos a explotaciones se resuelve por `farmerId`, no por nombre visible.
- El frontend ahora mezcla logica real de negocio con paridad visual tomada del export de Figma; si se continua con nuevas pantallas, conviene mantener este criterio:
  - preservar el dominio y endpoints reales ya implementados
  - tomar del export solo estructura visual, densidad, espaciados y componentes aparentes
- `src.zip` es referencia visual, no fuente de verdad funcional.

## Estado visual pendiente

- La base visual ya esta bastante mas cerca del export en:
  - login
  - dashboard
  - ganaderos
  - explotaciones
- Sigue pendiente una pasada fina de paridad en:
  - `register manager`
  - `register farmer`
  - `activate account`
  - `profile`
- Tambien falta revisar consistencia de copy, espaciados y responsive con la referencia completa, no solo en viewport desktop.

## Pendiente siguiente iteracion

- Ajuste fino de paridad visual en auth y perfil.
- CRUD completo de explotaciones.
- Ficha detalle de explotacion.
- Animales, movimientos, nacimientos, muertes.
- Importacion TXT con preview y confirmacion.
- Censos y balances.
- Libro de explotacion, incidencias, inspecciones y suscripcion.

## 2026-04-24 - Ajuste puntual sidebar en Ganaderos

- Corregido un bug visual del shell en la vista `Ganaderos`: el bloque inferior de perfil no quedaba anclado al fondo del sidebar y podia desaparecer del viewport en paginas largas.
- Causa: el contenedor principal usaba `min-height: 100vh`, lo que permitia que el sidebar creciera con la altura del contenido central en lugar de fijarse al viewport.
- Fix aplicado en `src/frontend/src/styles.css`:
  - `.app-shell` ahora usa `height: 100vh` y `overflow: hidden`
  - `.sidebar` ahora usa `height: 100vh` y `overflow: hidden`
  - `.app-main` y `.page-content` ahora incluyen `min-height: 0` para que el scroll quede contenido en la zona principal
- Resultado esperado: el footer con usuario queda visible abajo, como en la referencia del menu lateral de Figma, y el scroll ocurre en el contenido principal.
- Captura de validacion: `output/sidebar-farmers-fixed.png`

## 2026-04-24 - Politica de datos frontend y seeding SQL

- Se fija el criterio de proyecto: no se deben introducir datos mockeados o hardcodeados en vistas de frontend.
- Los datos de demo y validacion visual deben entrar por base de datos mediante scripts de seeding.
- Se ha creado el script idempotente `db/init/002_seed_demo.sql`.
- El script siembra:
  - cuentas demo base (`lucia@asesoria.com`, `miguel@ganaderia.com`)
  - ganadero pendiente (`contacto.sierra.norte@example.com`)
  - suscripciones demo
  - explotacion `Dehesa El Robledal`
  - actividad real para dashboard: animales, nacimientos, vacunaciones, movimientos e inspecciones
- El script se ha ejecutado y reejecutado sobre la base actual para validar idempotencia.
- Conteos validados tras el seed:
  - `animal`: 8
  - `animal_birth`: 3
  - `vaccination`: 3
  - `movement_certificate`: 4
  - `inspection`: 2

## 2026-07-29 - Importación de animales pertenecientes

- Añadido lector seguro del `.xls` HTML/XML de “Animales pertenecientes”, limitado a 5 MB y 1.000 filas.
- Añadidos preview y confirmación para explotaciones ovinas/caprinas existentes, además de alta atómica de
  explotación con importación inicial.
- Implementada importación parcial con estados por fila y control estricto del REGA destino, duplicados y
  conflictos entre explotaciones.
- Persistidas las fechas exactas de nacimiento e identificación; nueva migración
  `023_ovine_caprine_identification_date.sql`.
- Añadido paso opcional al asistente de alta y pestaña en ajustes solo para ovino/caprino. La interfaz porcina
  no incorpora controles de importación.
- El libro ovino/caprino utiliza la fecha real de identificación con compatibilidad hacia registros antiguos.
- Incorporadas pruebas del servicio, incluida la lectura de los 499 animales del documento aportado.

## 2026-07-29 - Tolerancia de suscripción e idempotencia del alta

- Corregida la resolución del plan efectivo: una suscripción activa con renovación automática conserva su plan
  aunque la fecha de expiración local todavía no se haya sincronizado.
- El periodo ya abonado conserva sus prestaciones hasta su fecha de fin aunque el cobro posterior esté pendiente
  o la renovación se haya cancelado.
- El código REGA se usa como clave natural idempotente en el alta: repetir exactamente la misma petición devuelve
  la explotación existente sin consumir de nuevo el límite del plan.
- Un REGA existente con distinto titular o datos diferentes continúa rechazándose como conflicto, respaldado por
  la restricción única de base de datos.
- Los reintentos de importación tratan los animales ya incorporados a la misma explotación como éxito sin cambios;
  también se reconcilian las carreras entre dos peticiones concurrentes y se evita devolver errores técnicos.
- Añadidas pruebas de regresión para cuenta Max con expiración local atrasada, unicidad de REGA e idempotencia del
  alta y de la importación.

## 2026-07-29 - Normalización del sexo en la importación

- Corregida la conversión de `Hembra` y `Macho` del informe `.xls` a los valores canónicos de la aplicación:
  `Female` y `Male`.
- Se aceptan también mayúsculas, espacios, las abreviaturas `H`/`M` y los valores ingleses para tolerar variantes
  del documento.
- La migración `024_normalize_animal_sex.sql` repara de forma idempotente los animales que ya se importaron con
  valores en minúsculas.
- El preview mantiene compatibilidad visual con respuestas antiguas y nuevas.

## 2026-07-29 - Modificación masiva de animales

- Añadida selección tipo correo en la tabla de animales: selección por fila/página, persistencia entre páginas,
  selección de todos los resultados filtrados, exclusiones y límite de 10.000 animales.
- Incorporado un asistente obligatorio de configuración, previsualización y resultado para modificar causas y
  fechas de alta/baja con semántica `Sin cambios`, `Establecer` o `Borrar`.
- Las parejas causa/fecha se validan sobre el estado resultante. Borrar causa y fecha de baja reactiva al animal.
- Añadida gestión histórica de una guía oficial por operación: crear/reutilizar entrada o salida y desvincular la
  última guía de cada dirección, sin mover de explotación ni generar balances o censos.
- La creación/reutilización exige coherencia entre guía, causa y fecha; una guía pendiente idéntica se confirma y
  una guía con el mismo REMO/serie pero datos distintos se rechaza.
- La operación usa preview con huella de estado, commit atómico y UUID idempotente. Los reintentos completados
  devuelven el mismo resultado y una previsualización obsoleta no escribe cambios.
- La migración `025_animal_bulk_update.sql` añade el registro de operaciones y los índices únicos normalizados de
  REMO/serie por explotación y dirección.
- Añadidas pruebas de preview sin escritura, conflictos, filtros/exclusiones, idempotencia, guía nueva/reutilizada,
  desvinculación de la última guía y ausencia de efectos sobre censos/balances.

## 2026-08-11 - Endurecimiento E2E de la modificación masiva

- Validado con Playwright MCP el flujo real de selección explícita y filtrada, exclusiones, previsualización,
  confirmación, restauración, guías, autorización, conflictos y estado obsoleto.
- Corregido un error 500 ante selecciones o listas de identificadores nulas: ahora se devuelve un error de dominio
  JSON controlado sin exponer la traza del servidor.
- La interfaz valida antes de llamar a la API las operaciones vacías, valores obligatorios y datos/fechas de guía,
  y traduce los errores de conexión y las respuestas inesperadas a mensajes accionables.
- Se bloquean el cierre y la cancelación mientras una petición está en curso para evitar que el resultado quede
  oculto al usuario.
- Los modos de causa y fecha de alta/baja quedan acoplados en la interfaz: establecer, borrar o conservar uno
  aplica automáticamente el mismo modo a su pareja y evita configuraciones incompletas.
- Verificada la concurrencia idempotente: dos commits simultáneos con el mismo UUID producen una sola escritura y
  el segundo resultado se devuelve como repetición.

## 2026-08-11 - Eliminación segura de explotaciones

- Añadido `DELETE /api/farms/{farmId}` con autorización por titular o gestor vinculado, transacción serializable y
  respuesta idempotente para reintentos sobre una explotación ya eliminada.
- El borrado elimina todos los animales de la explotación junto con sus subtipos, vacunaciones y vínculos a guías;
  los registros propios de la explotación se eliminan mediante las relaciones en cascada del esquema.
- Las guías que no implican otra explotación interna se eliminan. Si existe otra explotación interna, la guía se
  conserva y el REGA/nombre de la explotación eliminada queda como referencia externa histórica.
- La interfaz incorpora una zona de peligro en Ajustes y exige escribir el REGA exacto antes de habilitar la
  confirmación definitiva.
- Añadidas pruebas de borrado en cascada, preservación de guías compartidas, autorización e idempotencia.

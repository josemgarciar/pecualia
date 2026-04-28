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
  - notas internas
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

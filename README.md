# Pecualia

Aplicación web para gestión ganadera con frontend en React/Vite, backend en ASP.NET Core y PostgreSQL como base de datos.

## Requisitos

- `Docker` y `docker compose`
- `.NET 8 SDK`
- `Node.js 20+`
- `npm`

## Estructura

- `src/frontend`: aplicación cliente en React
- `src/backend/Pecualia.Api`: API backend en ASP.NET Core
- `db/init`: esquema SQL inicial y datos de ejemplo
- `db/migrations`: cambios incrementales sobre el esquema

## Configuración

1. Copia `.env.example` a `.env`.
2. Ajusta como mínimo estas variables:

```env
POSTGRES_DB=livestock
POSTGRES_USER=livestock
POSTGRES_PASSWORD=change_me
POSTGRES_PORT=5432

ConnectionStrings__Postgres=Host=127.0.0.1;Port=5432;Database=livestock;Username=livestock;Password=change_me
Jwt__Issuer=Pecualia.Api
Jwt__Audience=Pecualia.Frontend
Jwt__SigningKey=replace_with_a_long_random_secret
Activation__BaseUrl=http://127.0.0.1:5173/activate-account
Frontend__Origin=http://127.0.0.1:5173
```

### Email

Para desarrollo local sin proveedor real:

```env
Email__Mode=File
```

Los correos se guardarán en `src/backend/Pecualia.Api/App_Data/outbox`.

Para envío real con Resend:

```env
Email__Mode=Resend
Email__From=no-reply@pecualia.es
Email__ReplyTo=soporte@pecualia.es
Email__ApiKey=tu_api_key
```

### Stripe

En este proyecto la integración correcta de Stripe se divide entre frontend y backend:

```env
VITE_STRIPE_PUBLISHABLE_KEY=pk_test_xxx
Stripe__SecretKey=sk_test_xxx
Stripe__WebhookSecret=whsec_xxx
Stripe__ManagerProfessionalMonthlyPriceId=price_xxx
Stripe__ManagerEnterpriseMonthlyPriceId=price_xxx
Stripe__FarmerProfessionalMonthlyPriceId=price_xxx
```

Mapeo actual de planes de la aplicación:

- `Manager / Pro` -> `Stripe__ManagerProfessionalMonthlyPriceId`
- `Manager / Max` -> `Stripe__ManagerEnterpriseMonthlyPriceId`
- `Farmer / Pro` -> `Stripe__FarmerProfessionalMonthlyPriceId`

Los planes `Free` no necesitan `Price ID`.

## Despliegue local

### 1. Levantar la base de datos

```bash
docker compose up -d
```

### 2. Arrancar el backend

```bash
dotnet run --project src/backend/Pecualia.Api
```

La API queda disponible en `http://localhost:5044` y el healthcheck en `http://localhost:5044/health`.

### 3. Instalar dependencias del frontend

```bash
cd src/frontend
npm install
```

### 4. Arrancar el frontend

```bash
cd src/frontend
npm run dev
```

La aplicación queda disponible en `http://127.0.0.1:5173`.

## Flujo básico de trabajo

1. Levanta PostgreSQL con `docker compose up -d`.
2. Arranca el backend.
3. Arranca el frontend.
4. Entra en la aplicación y prueba los flujos funcionales:
   - login
   - alta de gestor
   - alta de ganadero
   - invitación y activación de cuenta
   - explotaciones, animales y movimientos

## Comandos útiles

Validación de backend:

```bash
dotnet build Pecualia.sln
```

Validación de frontend:

```bash
cd src/frontend
npm run build
```

Apagar la base de datos:

```bash
docker compose down
```

## Despliegue en Render

La opción preparada en este repositorio despliega la SPA y la API en un único servicio Docker de Render, con PostgreSQL gestionado por Render y seed de demo opcional en el arranque. La configuración actual está orientada al plan `free` tanto para el servicio web como para la base de datos.

### Archivos preparados

- `render.yaml`: define el servicio `pecualia-app` y la base `pecualia-db`
- `Dockerfile`: construye frontend y backend en una sola imagen
- `.github/workflows/deploy-render.yml`: despliega en Render en cada push a `main`

### Flujo recomendado

1. Sube este repositorio a GitHub.
2. En Render, crea un nuevo Blueprint apuntando a este repositorio y a `render.yaml`.
3. Lanza el primer despliegue desde Render para que se creen:
   - el servicio web `pecualia-app`
   - la base de datos `pecualia-db`
4. En el servicio web, abre `Settings > Deploy Hook` y copia la URL del hook.
5. En GitHub, crea el secreto del repositorio `RENDER_DEPLOY_HOOK_URL` con esa URL.
6. En Render, deja `Auto-Deploy` del servicio web en `Off` para evitar despliegues duplicados, porque el workflow de GitHub ya se encargará del despliegue por cada push a `main`.

### Variables de entorno en Render

El `render.yaml` ya deja configuradas las claves principales:

- `ConnectionStrings__Postgres` desde la base de datos gestionada por Render
- `Database__BootstrapOnStartup=true`
- `Database__SeedDemoData=true`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey` generado automáticamente
- `Email__Mode=File`

Si más adelante usas dominio propio, puedes sobrescribir manualmente:

- `Frontend__Origin`
- `Activation__BaseUrl`

Si no las defines, la aplicación deriva ambos valores automáticamente a partir del hostname público de Render.

### Seed y validación con cliente

Con la configuración actual de Render:

- la base de datos se inicializa automáticamente si está vacía
- se aplican los scripts SQL estructurales
- se cargan los datos demo para validación (`Database__SeedDemoData=true`)

Si quieres un entorno limpio sin demo, cambia `Database__SeedDemoData` a `false` y reprovisiona la base de datos.

### Limitaciones del plan free

En Render Free debes contar con estas restricciones:

- el servicio web entra en reposo tras 15 minutos sin tráfico y tarda alrededor de 1 minuto en despertar
- la base de datos Postgres `free` expira 30 días después de su creación si no se sube a un plan de pago
- no hay backups en la base de datos `free`

Para una demo con cliente sirve, pero no es una configuración válida como entorno estable.

## Notas de despliegue

- `Activation__BaseUrl` debe apuntar al frontend público cuando la aplicación se publique.
- `Frontend__Origin` debe coincidir con el dominio desde el que cargará la SPA.
- `VITE_STRIPE_PUBLISHABLE_KEY` debe configurarse en el entorno del frontend con la clave pública de Stripe.
- `Stripe__SecretKey` y `Stripe__WebhookSecret` deben configurarse solo en el entorno del backend.
- Para Cloudflare Pages, usa la variable `VITE_STRIPE_PUBLISHABLE_KEY` en cada entorno de build que publiques.
- Para Azure App Service, usa App Settings con los nombres `Stripe__...` y marca como secretos los valores sensibles.
- Si usas Resend, el dominio del remitente debe estar verificado.
- Si cambias `.env`, reinicia el backend para que recargue la configuración.

## Estado actual

- Backend y frontend compilan correctamente en local.
- El flujo de invitación por email ya soporta envío real con Resend.
- Las preferencias de notificaciones del usuario todavía no están persistidas en backend.

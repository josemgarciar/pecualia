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

Pruebas de rendimiento de backend:

```bash
dotnet test src/backend/Pecualia.Test/Pecualia.Test.csproj --filter "Category=Performance"
```

Solo módulo ovino/caprino:

```bash
dotnet test src/backend/Pecualia.Test/Pecualia.Test.csproj --filter "PerformanceModule=OvineCaprine"
```

Solo módulo porcino:

```bash
dotnet test src/backend/Pecualia.Test/Pecualia.Test.csproj --filter "PerformanceModule=Porcine"
```

Solo módulo transversal:

```bash
dotnet test src/backend/Pecualia.Test/Pecualia.Test.csproj --filter "PerformanceModule=Transversal"
```

Estas pruebas miden regresiones de rendimiento a nivel de servicio sobre la base de datos en memoria del proyecto. Sirven para detectar degradaciones en consultas, agregaciones y cargas masivas del dominio sin depender de latencia externa.

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

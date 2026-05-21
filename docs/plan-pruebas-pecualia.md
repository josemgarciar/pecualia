# Plan de pruebas vigente de Pecualia

## 1. Objetivo
Este documento describe el plan de pruebas que existe actualmente en el repositorio. Sustituye la matriz aspiracional anterior por un inventario real de las suites automatizadas disponibles a fecha de hoy, para que el equipo sepa:

- qué se está validando de verdad;
- con qué nivel de automatización;
- qué áreas tienen cobertura parcial;
- qué huecos siguen pendientes.

## 2. Estado actual de la automatización
La automatización actual está concentrada en backend:

- proyecto de pruebas: `src/backend/Pecualia.Test`;
- framework: `xUnit`;
- aserciones: `FluentAssertions`;
- persistencia de pruebas: `EF Core InMemory`;
- integración HTTP: `WebApplicationFactory<Program>`;
- rendimiento: pruebas etiquetadas con `Trait("Category", "Performance")`.

Resumen de cobertura detectada en el repositorio:

- `189` tests automatizados en `Pecualia.Test`;
- `11` tests de rendimiento;
- `0` suites frontend unitarias detectadas (`Vitest`, `Jest`, etc.);
- `0` suites E2E/browser detectadas (`Playwright`, `Cypress`, etc.).

## 3. Estrategia actual
Hoy la validación se apoya en cuatro capas:

1. Pruebas unitarias puras de reglas y helpers.
   Validan normalización, validadores, códigos oficiales, límites de suscripción y reglas de clasificación.

2. Pruebas de servicios con base en memoria.
   Validan lógica de negocio, persistencia, permisos de acceso y efectos de dominio.

3. Pruebas de integración HTTP.
   Validan el cableado real de endpoints, mapeo de controladores y flujos operativos completos contra la API.

4. Pruebas de rendimiento.
   Validan que escenarios volumétricos clave cierren dentro del presupuesto esperado.

## 4. Comandos de ejecución
Ejecución completa:

```bash
dotnet test src/backend/Pecualia.Test/Pecualia.Test.csproj
```

Solo rendimiento:

```bash
dotnet test src/backend/Pecualia.Test/Pecualia.Test.csproj --filter Category=Performance
```

## 5. Inventario real de suites

### 5.1 Autenticación, cuenta y seguridad

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/AuthServiceTests.cs` | 15 | Registro de gestor, alta de ganadero vinculado, login válido e inválido, bloqueo por inactividad, activación de cuenta, recuperación y reset de contraseña, reenvío de activación, `/me`, edición de ajustes y cambio de contraseña. |
| `Infrastructure/SecurityServicesTests.cs` | 3 | Hash de contraseñas, generación de token de activación y JWT con claims. |
| `Controllers/ApiIntegrationFlowTests.cs` | 1 flujo principal | Registro de manager, consulta de perfil, ajustes, recordatorios, login, reenvío de activación y forgot password a través de HTTP real. |

Observaciones:

- Hay cobertura funcional sólida de autenticación en backend.
- No hay suite UI que valide formularios de login/registro/reset en frontend.

### 5.2 Ganaderos y explotaciones

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/FarmerServiceTests.cs` | 7 | Detalle de ganadero gestionado, restricción de acceso a terceros, límites de plan, filtros por búsqueda/provincia/estado, alta, reenvío de activación, edición y desvinculación. |
| `Services/FarmServiceTests.cs` | 7 | Listado accesible, detalle, restricción por permisos, límite de plan, alta de explotación, edición de capacidades porcinas y resumen. |
| `Controllers/ApiIntegrationFlowTests.cs` | 1 flujo CRUD | Alta/listado/detalle/edición de ganadero, reenvío de activación, alta/listado/detalle/resumen de explotación y desvinculación de manager. |

Observaciones:

- La cobertura actual está orientada a permisos, límites de plan y CRUD principal.
- No hay pruebas automatizadas de frontend para filtros, tablas o formularios.

### 5.3 Animales y operaciones de explotación

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/AnimalServiceTests.cs` | 11 | Filtros por movimiento, paginación y contadores, detalle, alta porcina, autorreposición, edición ovina, baja por muerte, borrado con y sin movimientos vinculados y restricción de autorreposición por especie. |
| `Services/FarmOperationServiceTests.cs` | 17 | Altas de nacimientos, restricciones temporales porcinas, transiciones pendientes, resolución de transición porcina, muertes agregadas e individuales, disponibilidad de autorreposición, borrado de nacimientos, vacunaciones, incidencias, inspecciones, balances y censos. |
| `Services/FarmCensusProjectionServiceTests.cs` | 2 | Evolución de nacimientos porcinos resueltos entre los hitos de 3 y 6 meses. |
| `Services/FarmCensusProjectionSupportTests.cs` | 5 | Reglas auxiliares de fechas y umbrales etarios. |
| `Services/BalanceSnapshotSupportTests.cs` | 4 | Persistencia y actualización de snapshots de balance y censo. |

Observaciones:

- Es una de las áreas mejor cubiertas del backend.
- La lógica temporal porcina está validada con reloj controlado.

### 5.4 Movimientos y guías

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/MovementServiceTests.cs` | 15 | Catálogo de razas, preview de importación, validación de contraparte externa y causas, rechazo de no identificados en porcino, altas externas, movimientos internos, confirmación de guía, salidas porcinas agregadas, movimientos caprinos no identificados, normalización de líneas de lector y validaciones de entrada manual. |
| `Services/PorcineMovementSupportTests.cs` | 2 | Desglose por tipo porcino y disponibilidad por bucket. |
| `Services/PorcineCapacitySupportTests.cs` | 3 | Límites de capacidad por madres/cebo y clasificación de buckets porcinos. |
| `Services/MerCodeSupportTests.cs` | 5 | Normalización y validación de códigos MER/SANDACH. |
| `Controllers/ApiIntegrationFlowTests.cs` | 1 flujo operativo | Alta de animal, movimiento manual, confirmación, consulta de dashboard y acceso a endpoints operativos vía HTTP. |

Observaciones:

- La importación y validación de movimientos está bien representada.
- No hay una suite browser que cubra el flujo visual de importación TXT.

### 5.5 Dashboard y recordatorios

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/DashboardServiceTests.cs` | 8 | Visibilidad temporal de confirmaciones pendientes, construcción de tareas de guía y tareas de transición porcina con niveles `warning` y `danger`. |
| `Services/PendingTaskQueryServiceTests.cs` | 2 | Agregación de tareas combinando vacunaciones, inspecciones, confirmaciones y transiciones para fincas accesibles. |
| `Services/TaskReminderSettingsServiceTests.cs` | 5 | Lectura y actualización de ajustes de recordatorios, validación de email y reseteo de programación. |
| `Services/TaskReminderProcessorTests.cs` | 3 | Envío de emails cuando hay tareas, ciclos sin tareas y procesamiento del último ciclo vencido. |
| `Services/TaskReminderWorkerTests.cs` | 2 | Ejecución periódica y tolerancia a excepciones del worker. |

Observaciones:

- La funcionalidad de recordatorios existe y está cubierta en backend.
- La experiencia de configuración en frontend no tiene pruebas automatizadas dedicadas.

### 5.6 Libro oficial, balances y PDF

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/BookServiceTests.cs` | 5 | Preview accesible, bloqueo por permisos, carga de balances y censos, generación PDF y ensamblado de datos ovinos y porcinos. |
| `Services/BookDocumentComposerTests.cs` | 6 | Resolución de secciones, filtros de entrada y generación real de PDF por sección/especie. |
| `Services/BookDocumentSupportTests.cs` | 2 | Códigos oficiales de `TIPO (9)` para porcino y fallback cuando una fila mezcla categorías. |
| `Services/BookBalanceSupportTests.cs` | 4 | Resolución de movimientos, contrapartes y fallback a datos persistidos. |

Observaciones:

- La cobertura actual se centra en composición documental y reglas de datos del libro.
- No existe validación automatizada visual del PDF generado.

### 5.7 Suscripción, facturación y límites de plan

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/BillingServiceTests.cs` | 8 | Rechazos por Stripe no configurado, usuario inexistente, cuenta sin email, plan no permitido, plan Free, suscripción Stripe existente, portal sin cliente y `session_id` ausente. |
| `Services/SubscriptionPlanSupportTests.cs` | 10 | Resolución de plan efectivo, límites de explotaciones/ganaderos y mensajes de error asociados. |

Observaciones:

- La cobertura de facturación es principalmente defensiva y de validación.
- No hay pruebas automatizadas del camino feliz contra Stripe real o mock de sesión completada.

### 5.8 Validadores, bootstrap e integración API

| Archivo | Nº tests | Cobertura real |
| --- | ---: | --- |
| `Services/DomainValidatorsTests.cs` | 8 | REGA, identificación animal, NIF/CIF y normalización de identificaciones. |
| `Services/DatabaseBootstrapperTests.cs` | 5 | Activación/desactivación de bootstrap, resolución de carpeta `db`, ordenación de scripts y exclusión de seeds. |
| `Controllers/ControllerResultsTests.cs` | 5 | Traducción de excepciones a respuestas HTTP y lectura de claims. |
| `Controllers/ControllerMappingTests.cs` | 1 | Registro del conjunto esperado de endpoints. |
| `Controllers/ApiIntegrationFlowTests.cs` | 4 | Salud, Swagger y tres recorridos HTTP de autenticación, CRUD y operación funcional. |

## 6. Rendimiento
Las pruebas de rendimiento actuales son:

| Archivo | Nº tests | Escenarios cubiertos |
| --- | ---: | --- |
| `Services/OvineCaprinePerformanceTests.cs` | 4 | Carga de animales, snapshot de censo y previsualización de importaciones masivas ovinas/caprinas. |
| `Services/PorcinePerformanceTests.cs` | 3 | Snapshot porcina, histórico de movimientos y detalle de guía con alta densidad de datos. |
| `Services/TransversalPerformanceTests.cs` | 4 | Dashboard de manager, cartera mixta de explotaciones y cartera amplia de ganaderos. |

Uso recomendado:

- ejecutar siempre antes de cambios de consultas complejas;
- revisar especialmente estas suites al tocar dashboard, censos, movimientos o listados agregados.

## 7. Cobertura no automatizada a día de hoy
Las siguientes áreas no tienen suite automatizada específica en el repositorio:

- pruebas unitarias de frontend React;
- pruebas E2E de navegador;
- validación visual de pantallas;
- validación visual de PDFs;
- integración real con proveedores externos como Stripe o correo;
- matriz completa de autorización por endpoint basada en combinaciones de rol y recurso ajeno.

## 8. Criterio de aceptación vigente
Mientras no se amplíe la estrategia, se considerará aceptable que un cambio quede cubierto si:

- mantiene verdes las `189` pruebas actuales;
- añade o actualiza tests en `Pecualia.Test` cuando modifica reglas de negocio existentes;
- añade test de integración HTTP cuando introduce o altera contratos de endpoint;
- añade test de rendimiento si cambia consultas o agregaciones en rutas ya sensibles.

## 9. Próximos incrementos recomendados
Prioridad alta:

- introducir suite de frontend para validadores compartidos y formularios críticos;
- introducir una suite E2E mínima para login, alta de ganadero, alta de explotación y flujo básico de movimientos;
- cubrir el camino feliz de facturación con dobles más realistas o sandbox controlado;
- ampliar la cobertura de permisos negativos por endpoint.

Prioridad media:

- automatizar comprobaciones estructurales del PDF;
- medir cobertura de código y fijar umbral mínimo;
- separar claramente pruebas rápidas de pruebas volumétricas en el pipeline.

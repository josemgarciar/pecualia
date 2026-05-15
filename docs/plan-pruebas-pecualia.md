# Plan de pruebas integral de Pecualia

## 1. Objetivo del documento
Este documento define el plan maestro de pruebas de Pecualia y sirve como guía de validación funcional y técnica antes y durante la automatización. La idea es usarlo como checklist compartido para revisar cada bloque de pruebas y confirmar juntos:

- qué lógica concreta se está validando;
- qué datos previos necesita la prueba;
- qué resultado se espera;
- qué fallo se consideraría regresión.

El alcance cubre toda la aplicación:

- backend .NET y API;
- lógica de dominio;
- permisos por rol;
- flujos de frontend;
- integraciones funcionales;
- recorridos completos E2E.

## 2. Estrategia de validación
La validación se divide en dos capas:

1. `Pecualia.Test`
   Proyecto .NET para probar lógica de dominio, servicios, contratos, autorización y API.

## 3. Criterios globales de aceptación
Cada módulo se considerará correctamente cubierto cuando existan pruebas para:

- caso feliz;
- validaciones de entrada;
- permisos y acceso por rol;
- errores de negocio;
- persistencia o efecto observable en UI;
- cronología y reglas temporales cuando apliquen.

## 4. Datos y convenciones de prueba
- Se usarán usuarios de prueba de tipo `Manager` y `Farmer`.
- Se prepararán explotaciones ovinas y porcinas para cubrir reglas específicas por especie.
- Las pruebas sensibles al tiempo usarán reloj controlado o datos semilla con fechas explícitas.
- En porcino se prepararán nacimientos y animales alrededor de los hitos de `3 meses` y `6 meses` para validar reclasificación, censo y libro.
- Los nombres de las pruebas deben reflejar claramente la lógica validada.

## 5. Matriz de pruebas por módulo

## 5.1 Autenticación y cuenta

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| AUTH-01 | Registro de gestor | Verifica que un gestor se puede registrar con datos válidos, se crea el usuario, se asigna rol `Manager`, se genera la estructura de manager y se devuelve token válido. | Backend + E2E | Crítica |
| AUTH-02 | Registro público de ganadero | Verifica que un ganadero se registra con datos válidos, se persisten usuario y farmer, y se aplica el estado inicial correcto según activación/verificación. | Backend + E2E | Crítica |
| AUTH-03 | Registro de ganadero con NIF inválido | Verifica que el alta falla cuando el identificador fiscal de persona física no supera la validación de control. | Backend + E2E | Alta |
| AUTH-04 | Registro de ganadero con CIF inválido | Verifica que el alta de persona jurídica rechaza CIF con formato o control erróneo. | Backend + E2E | Alta |
| AUTH-05 | Registro con email duplicado | Verifica que no se pueden crear dos cuentas con el mismo email. | Backend | Crítica |
| AUTH-06 | Login con credenciales correctas | Verifica autenticación correcta, emisión de token y carga del perfil devuelto al frontend. | Backend + E2E | Crítica |
| AUTH-07 | Login con credenciales incorrectas | Verifica rechazo cuando usuario o contraseña no coinciden. | Backend + E2E | Crítica |
| AUTH-08 | Activación de cuenta válida | Verifica consumo de token, activación del usuario, marcado de `EmailVerifiedAt` y posibilidad de login posterior. | Backend + E2E | Crítica |
| AUTH-09 | Activación con token caducado o usado | Verifica que un token inválido no puede reutilizarse ni activar cuentas fuera de plazo. | Backend + E2E | Alta |
| AUTH-10 | Reenvío de activación | Verifica que se genera un nuevo flujo de activación solo para cuentas pendientes. | Backend + E2E | Alta |
| AUTH-11 | Consulta de perfil actual `/me` | Verifica que el endpoint devuelve identidad, rol, plan y datos visibles del usuario autenticado. | Backend | Alta |
| AUTH-12 | Actualización de ajustes del usuario | Verifica edición de nombre, email, credenciales y datos de cuenta sin alterar campos no autorizados. | Backend + E2E | Alta |
| AUTH-13 | Recuperación de contraseña por email | Verifica que el sistema responde con mensaje genérico, crea token para cuentas activas y no filtra si el correo existe. | Backend + E2E | Alta |
| AUTH-14 | Reset de contraseña con token válido | Verifica consumo de token, actualización del hash y bloqueo de reutilización posterior. | Backend + E2E | Alta |
| AUTH-15 | Reset con token inválido o caducado | Verifica rechazo cuando el enlace de recuperación ya no es válido. | Backend | Alta |
| AUTH-16 | Registro de ganadero vinculado a gestor sin capacidad | Verifica rechazo cuando el gestor invitante supera el límite de ganaderos permitido por su plan. | Backend | Alta |

## 5.2 Gestión de ganaderos

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| FARMER-01 | Listado de ganaderos gestionados | Verifica que un gestor solo ve los ganaderos de su cartera. | Backend + E2E | Crítica |
| FARMER-02 | Filtro por texto | Verifica búsqueda por nombre, NIF/CIF o localidad. | Backend + E2E | Alta |
| FARMER-03 | Filtro por provincia | Verifica que el listado responde correctamente al filtro geográfico. | Backend + E2E | Media |
| FARMER-04 | Filtro por estado | Verifica filtrado por `PendingActivation` y `Active`. | Backend + E2E | Media |
| FARMER-05 | Apertura de ficha de ganadero | Verifica carga del detalle completo del ganadero seleccionado. | Backend + E2E | Alta |
| FARMER-06 | Alta de persona física | Verifica wizard de alta con nombre, apellidos, NIF y contacto, y persistencia final correcta. | Backend + E2E | Crítica |
| FARMER-07 | Alta de persona jurídica | Verifica alta de empresa con razón social, representante legal y CIF. | Backend + E2E | Crítica |
| FARMER-08 | Edición de ganadero | Verifica actualización de datos permitidos sin perder vinculación con el gestor. | Backend + E2E | Alta |
| FARMER-09 | Reenvío de activación desde ficha | Verifica la acción disponible solo si el ganadero está pendiente de activación. | Backend + E2E | Media |
| FARMER-10 | Desvinculación de gestor | Verifica que el gestor puede desvincular al ganadero sin eliminar la cuenta ni sus explotaciones. | Backend + E2E | Alta |
| FARMER-11 | Restricción de acceso a ganadero ajeno | Verifica que un gestor no puede consultar ni editar un ganadero no gestionado por él. | Backend | Crítica |
| FARMER-12 | Alta o edición con NIF/CIF duplicado | Verifica rechazo cuando otro ganadero ya usa el mismo identificador fiscal. | Backend | Alta |
| FARMER-13 | Alta o edición con email duplicado | Verifica rechazo cuando otro usuario ya usa el mismo correo. | Backend | Alta |

## 5.3 Gestión de explotaciones

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| FARM-01 | Listado accesible por rol | Verifica que gestor y ganadero ven solo las explotaciones permitidas por su perfil. | Backend + E2E | Crítica |
| FARM-02 | Filtro por texto en explotaciones | Verifica búsqueda por nombre o código REGA. | Backend + E2E | Media |
| FARM-03 | Filtro por especie | Verifica que el listado responde correctamente a `Ovine`, `Caprine` y `Porcine`. | Backend + E2E | Media |
| FARM-04 | Filtro por estado | Verifica filtrado por `Active`, `Pending` e `Inactive`. | Backend + E2E | Media |
| FARM-05 | Alta de explotación con REGA válido | Verifica creación de explotación con datos mínimos correctos y vinculación al titular. | Backend + E2E | Crítica |
| FARM-06 | Alta con REGA inválido | Verifica rechazo cuando el código REGA no cumple formato de dominio. | Backend + E2E | Alta |
| FARM-07 | Alta porcina con campos obligatorios | Verifica que porcino exige capacidad autorizada y/o registro específico cuando la lógica lo requiera. | Backend + E2E | Alta |
| FARM-08 | Edición de explotación | Verifica actualización de datos editables desde el detalle. | Backend + E2E | Alta |
| FARM-09 | Resumen de explotación | Verifica métricas agregadas mostradas en cabecera o resumen. | Backend | Alta |
| FARM-10 | Detalle completo de explotación | Verifica carga de snapshot integral consumido por la pantalla de detalle. | Backend + E2E | Alta |
| FARM-11 | Restricción de acceso a explotación ajena | Verifica rechazo al consultar o editar explotaciones no accesibles por el usuario autenticado. | Backend | Crítica |
| FARM-12 | Límite de explotaciones por plan | Verifica rechazo al crear una explotación cuando el usuario o gestor supera el cupo permitido por su suscripción. | Backend | Alta |
| FARM-13 | Huso y capacidades con valores inválidos | Verifica rechazo cuando el huso es no positivo o las capacidades porcinas son negativas. | Backend + E2E | Media |

## 5.4 Gestión de animales

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| ANIMAL-01 | Listado global de animales | Verifica carga de animales accesibles y filtros básicos del módulo global. | Backend + E2E | Alta |
| ANIMAL-02 | Paginación por explotación | Verifica respuesta paginada del listado de animales dentro del detalle de explotación. | Backend + E2E | Alta |
| ANIMAL-03 | Alta de animal con identificación válida | Verifica persistencia del animal cuando crotal/identificación y especie son coherentes. | Backend + E2E | Crítica |
| ANIMAL-04 | Alta con identificación inválida | Verifica rechazo de crotales o identificaciones que no cumplen reglas del dominio. | Backend + E2E | Alta |
| ANIMAL-05 | Alta con REGA origen inválido | Verifica rechazo cuando el REGA de origen no supera validación. | Backend + E2E | Alta |
| ANIMAL-06 | Alta de porcino con campos específicos | Verifica obligación y persistencia de `animalType`, `tag`, `pigRegistrationNumber` o `identificationDate` cuando aplique. | Backend + E2E | Alta |
| ANIMAL-07 | Autorreposición múltiple desde censo agregado | Verifica creación masiva de animales consecutivos a partir del stock agregado elegible del censo, sin seleccionar nacimientos concretos en UI. | Backend + E2E | Crítica |
| ANIMAL-08 | Autorreposición con lechones o lotes pendientes de reclasificación | Verifica rechazo cuando se intentan identificar animales porcinos que siguen en `Lechones (0-3 meses)` o en `Pendientes reclasificación`, ya que solo son elegibles las ramas intermedias. | Backend | Crítica |
| ANIMAL-09 | Autorreposición con stock insuficiente | Verifica rechazo cuando se intentan convertir más animales que los disponibles o más de los elegibles por edad y rama. | Backend + E2E | Alta |
| ANIMAL-10 | Edición de animal | Verifica actualización de datos editables sin romper restricciones por especie o estado. | Backend + E2E | Alta |
| ANIMAL-11 | Baja de animal | Verifica persistencia de causa, fecha y destino, y coherencia cronológica. | Backend + E2E | Crítica |
| ANIMAL-12 | Eliminación de animal | Verifica borrado permitido y protección frente a estados no eliminables si la lógica lo impide. | Backend + E2E | Alta |
| ANIMAL-13 | Consulta de detalle de animal | Verifica recuperación de todos los datos del animal, incluidos específicos de especie e histórico. | Backend + E2E | Alta |
| ANIMAL-14 | Autorreposición con identificación inicial no consecutiva | Verifica rechazo cuando la identificación inicial no termina en tramo numérico válido o el rango desborda su longitud. | Backend | Alta |
| ANIMAL-15 | Alta o edición con datos de especie cruzados | Verifica rechazo si se intentan guardar campos porcinos en ovino/caprino o viceversa. | Backend | Alta |
| ANIMAL-16 | Eliminación de animal vinculado a movimientos | Verifica rechazo al borrar un animal ya enlazado a guías registradas. | Backend | Alta |
| ANIMAL-17 | Baja duplicada de animal | Verifica rechazo cuando se intenta dar de baja un animal ya descargado previamente. | Backend | Alta |

## 5.5 Operaciones de explotación

### Nacimientos

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| BIRTH-01 | Registro de nacimiento válido | Verifica creación de nacimiento con fecha, número de crías, peso y observaciones correctas, sin alta individual automática de animales. | Backend + E2E | Alta |
| BIRTH-02 | Edición de nacimiento válido | Verifica actualización de fecha, número de crías, peso y observaciones, junto con la actualización del asiento de balance asociado. | Backend + E2E | Alta |
| BIRTH-03 | Edición de nacimiento por debajo de lo ya autorepuesto | Verifica rechazo cuando se intenta reducir `offspringNumber` por debajo de las unidades ya consumidas por autoreposición. | Backend | Crítica |
| BIRTH-04 | Eliminación de nacimiento no consumido | Verifica borrado del nacimiento y del asiento de balance asociado cuando no ha sido usado en autoreposición. | Backend + E2E | Alta |
| BIRTH-05 | Eliminación de nacimiento consumido | Verifica rechazo al eliminar un nacimiento que ya soporta animales dados de alta por autoreposición. | Backend | Crítica |
| BIRTH-06 | Nacimiento sobre explotación ajena | Verifica rechazo por permisos. | Backend | Alta |
| BIRTH-07 | Nacimiento con cronología inválida | Verifica rechazo si la fecha no encaja con reglas del dominio. | Backend | Media |
| BIRTH-08 | Disponibilidad agregada para autoreposición | Verifica que el endpoint auxiliar devuelve únicamente el total disponible y el total elegible por edad, sin exponer contadores derivados por nacimiento. | Backend + E2E | Alta |
| BIRTH-09 | Nacimiento porcino con más de 3 meses | Verifica rechazo en backend y frontend cuando se intenta registrar un parto porcino con antigüedad superior a 3 meses. | Backend + E2E | Crítica |
| BIRTH-10 | Edición de nacimiento porcino fuera de ventana | Verifica rechazo cuando una edición desplaza el parto porcino fuera de la ventana permitida de 3 meses. | Backend + E2E | Alta |
| BIRTH-11 | Generación de tarea de reclasificación a los 3 meses | Verifica que un nacimiento porcino pasa a pendiente de reclasificación al cumplir exactamente 3 meses. | Backend + E2E | Crítica |
| BIRTH-12 | Resolución de reclasificación por lote | Verifica que el lote se reparte entre `Recría`, `Hembras reposición` y `Machos reposición`, que la suma debe cuadrar y que se crea/actualiza el balance asociado. | Backend + E2E | Crítica |
| BIRTH-13 | Evolución automática a los 6 meses | Verifica que `Recría -> Cebo`, `Hembras reposición -> Cerdas vida` y `Machos reposición -> Verracos` se reflejan automáticamente en el censo por fecha. | Backend | Crítica |

### Muertes

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| DEATH-01 | Registro de muerte válido | Verifica persistencia de la muerte y efecto sobre el animal. | Backend + E2E | Crítica |
| DEATH-02 | Muerte de animal inexistente | Verifica rechazo si el animal no pertenece a la explotación o no existe. | Backend | Alta |
| DEATH-03 | Fecha de muerte inválida | Verifica coherencia temporal respecto al alta y otros eventos. | Backend | Alta |
| DEATH-04 | Muerte porcina o caprina solo con MER | Verifica que porcino y caprino no admiten `SANDACH` y solo aceptan un código `MER` válido como destino de baja. | Backend + E2E | Crítica |
| DEATH-05 | Destino bloqueado en frontend para porcino y caprino | Verifica que el selector de destino aparece bloqueado y fijado a `MER` en los formularios de baja de esas especies. | E2E | Alta |

### Vacunaciones

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| VACC-01 | Alta de vacunación | Verifica persistencia de tipo, fecha, observaciones y próxima dosis. | Backend + E2E | Alta |
| VACC-02 | Edición de vacunación | Verifica actualización del calendario sin romper referencias. | Backend + E2E | Alta |
| VACC-03 | Borrado de vacunación | Verifica eliminación desde API y refresco de UI. | Backend + E2E | Media |
| VACC-04 | Próxima dosis en dashboard | Verifica que las vacunaciones próximas alimentan tareas pendientes. | Backend | Alta |

### Censo y balance

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| CENSUS-01 | Consulta de censo anual calculado | Verifica lectura del censo proyectado por año y especie a partir de nacimientos, animales activos, movimientos, muertes y autoreposición. | Backend + E2E | Alta |
| CENSUS-02 | Reclasificación porcina automática por edad | Verifica la lógica porcina `Lechones 0-3 meses`, `Recría/Hembras reposición/Machos reposición 3-6 meses` y `Cebo/Cerdas vida/Verracos 6+` únicamente por efecto de la fecha de consulta. | Backend | Alta |
| CENSUS-03 | Impacto automático de la autoreposición | Verifica que la autoreposición reduce stock no identificado y aumenta reproductores macho o hembra sin edición manual del censo. | Backend + E2E | Crítica |
| CENSUS-04 | Impacto automático de guías | Verifica que las guías de entrada y salida recalculan correctamente reproductores y categorías no identificadas según edad, especie y tipo. | Backend + E2E | Alta |
| CENSUS-05 | Impacto automático de nacimientos y muertes | Verifica que un nacimiento incrementa la categoría juvenil correspondiente y que una muerte reduce la categoría reproductiva correcta. | Backend + E2E | Alta |
| CENSUS-06 | Censo de solo lectura | Verifica que no existe edición manual operativa del censo y que cualquier intento de actualización manual se rechaza o queda fuera del flujo funcional. | Backend + E2E | Alta |
| CENSUS-07 | Bucket de pendientes de reclasificación porcina | Verifica que los nacimientos porcinos de 3+ meses sin decisión desaparecen de `Lechones` y aparecen en `Pendientes reclasificación`. | Backend + E2E | Alta |
| BALANCE-01 | Cálculo de balance anual | Verifica agregación de entradas, salidas, muertes, nacimientos y autoreposición en el balance anual. | Backend + E2E | Alta |
| BALANCE-02 | Coherencia censo-balance | Verifica que las operaciones de explotación se reflejan de forma consistente en censo proyectado, resumen y balance. | Backend + E2E | Media |
| BALANCE-03 | Balance de reclasificación porcina | Verifica que la reclasificación a los 3 meses genera un asiento `Autorreposición` con el delta porcino correcto y reversible en libro/histórico. | Backend | Alta |

### Incidencias e inspecciones

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| INCIDENT-01 | Alta de incidencia | Verifica persistencia de incidencia con sus campos y referencias. | Backend + E2E | Alta |
| INCIDENT-02 | Validación de identificación anterior/nueva | Verifica reglas de consistencia en incidencias de identificación. | Backend | Media |
| INCIDENT-03 | Incidencia sin datos descriptivos | Verifica rechazo cuando no se informa ni animal, ni motivo, ni descripción, ni identificaciones relacionadas. | Backend | Media |
| INSPECTION-01 | Alta de inspección | Verifica persistencia de inspección con fecha, motivo y observaciones. | Backend + E2E | Alta |
| INSPECTION-02 | Listado de inspecciones | Verifica orden, visibilidad y carga en el detalle de la explotación. | Backend + E2E | Media |
| INSPECTION-03 | Inspecciones en dashboard | Verifica que inspecciones próximas aparecen en tareas pendientes. | Backend | Alta |
| INSPECTION-04 | Inspección sin contenido o con animales revisados negativos | Verifica rechazo si faltan motivo y observaciones o si `taggedAnimals` es negativo. | Backend | Media |

## 5.6 Movimientos y guías

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| MOVE-01 | Listado de guías por explotación | Verifica carga de movimientos de una explotación con origen/destino y estados. | Backend + E2E | Crítica |
| MOVE-02 | Detalle de guía | Verifica datos completos de la guía, transporte, animales y contrapartes. | Backend + E2E | Alta |
| MOVE-03 | Alta manual de guía | Verifica creación manual con fechas, dirección, especie y datos de transporte. | Backend + E2E | Alta |
| MOVE-04 | Alta manual con cronología inválida | Verifica rechazo cuando llegada, salida o solicitud no respetan orden temporal. | Backend | Alta |
| MOVE-05 | Preview de importación TXT | Verifica parseo de líneas, detección de duplicados, conflictos, inexistentes y errores de formato. | Backend + E2E | Crítica |
| MOVE-06 | Commit de importación | Verifica creación final del movimiento y efectos sobre los animales en una transacción consistente. | Backend + E2E | Crítica |
| MOVE-07 | Confirmación de guía | Verifica que la acción de confirmar cambia solo el estado a `Confirmed`. | Backend + E2E | Crítica |
| MOVE-08 | Confirmación duplicada | Verifica rechazo al intentar confirmar una guía ya confirmada. | Backend | Alta |
| MOVE-09 | Confirmación sin alterar `ArrivalDate` | Verifica explícitamente que la confirmación no modifica la fecha de llegada almacenada. | Backend | Alta |
| MOVE-10 | Visibilidad de guías pendientes en dashboard dentro de 10 días | Verifica que una guía `Pending` con `ArrivalDate <= ahora` se mantiene visible durante 10 días para su confirmación. | Backend + E2E | Crítica |
| MOVE-11 | Expiración de visibilidad de guía pendiente | Verifica que la tarea desaparece una vez superado el margen de 10 días desde la llegada. | Backend | Alta |
| MOVE-12 | Catálogo de razas por especie | Verifica que el endpoint auxiliar devuelve opciones coherentes para cada especie. | Backend | Media |
| MOVE-13 | Movimiento porcino con catálogo completo de tipos | Verifica que el selector y la API admiten `Verracos`, `Cerdas vida`, `Machos reposición`, `Hembras reposición`, `Cebo`, `Recría` y `Lechones` sin depender de la clasificación zootécnica. | Backend + E2E | Alta |
| MOVE-14 | Contraparte externa sin REGA o con REGA inválido | Verifica rechazo cuando una guía con contraparte externa no informa código REGA válido. | Backend + E2E | Alta |
| MOVE-15 | Validación de causas por dirección e importación | Verifica que solo se aceptan `Entrada/Autorreposición` en altas y `Salida/Muerte` en bajas, tanto en flujo manual como importado. | Backend | Alta |
| MOVE-16 | Entrada externa con animales nuevos sin datos comunes | Verifica rechazo cuando faltan raza, sexo o tipo porcino compartido para dar de alta animales inexistentes durante una importación. | Backend + E2E | Alta |
| MOVE-17 | Movimiento sin identificación solo para ovino/caprino | Verifica rechazo si se intenta usar el flujo de animales no identificados en especies no permitidas. | Backend | Alta |
| MOVE-18 | Movimiento porcino agregado con stock o capacidad insuficiente | Verifica rechazo cuando el tipo porcino solicitado supera el stock disponible de origen o la capacidad de destino. | Backend | Alta |

## 5.7 Dashboard

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| DASH-01 | Métricas del gestor | Verifica cálculo de ganaderos gestionados, explotaciones activas, movimientos y activaciones pendientes. | Backend + E2E | Alta |
| DASH-02 | Métricas del ganadero | Verifica cálculo de explotaciones, animales registrados y actuaciones próximas. | Backend + E2E | Alta |
| DASH-03 | Actividad mensual | Verifica agregación mensual de altas, bajas, nacimientos y movimientos. | Backend | Alta |
| DASH-04 | Tendencia vs mes anterior | Verifica cálculo del porcentaje mensual y manejo del caso sin base comparativa. | Backend | Media |
| DASH-05 | Tareas pendientes vacías | Verifica comportamiento cuando no hay vacunas, inspecciones ni guías pendientes. | Backend + E2E | Media |
| DASH-06 | Tareas pendientes mixtas | Verifica combinación ordenada de vacunas, inspecciones, guías pendientes y reclasificaciones porcinas. | Backend + E2E | Alta |
| DASH-07 | Orden por fecha de vencimiento | Verifica que las tareas se ordenan por fecha y prioridad temporal. | Backend | Media |
| DASH-08 | Tarea de reclasificación porcina vencida | Verifica que el dashboard marca como `danger` los lotes porcinos que superan la fecha de transición final sin resolución. | Backend | Alta |

## 5.8 Perfil, ajustes y suscripción

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| PROFILE-01 | Vista de perfil | Verifica carga de datos del usuario actual y accesos a acciones asociadas. | E2E | Media |
| SETTINGS-01 | Edición de datos básicos | Verifica actualización desde pantalla de ajustes y reflejo posterior en perfil. | Backend + E2E | Alta |
| SETTINGS-02 | Cambio de contraseña | Verifica requisito de contraseña actual y persistencia del nuevo hash. | Backend + E2E | Alta |
| SUBS-01 | Creación de checkout | Verifica creación de sesión Stripe para el plan seleccionado. | Backend + E2E | Alta |
| SUBS-02 | Consulta de estado de checkout | Verifica mapeo del resultado de Stripe a estado visible de suscripción. | Backend + E2E | Alta |
| SUBS-03 | Sesión de portal de cliente | Verifica devolución de URL del portal de gestión de suscripción. | Backend + E2E | Media |
| SUBS-04 | Webhook de Stripe | Verifica actualización idempotente del estado de suscripción tras eventos externos. | Backend | Alta |
| SUBS-05 | Checkout con plan no permitido por rol | Verifica rechazo cuando un ganadero intenta contratar un plan distinto de `Professional` o cuando se intenta contratar `Basic` vía Stripe. | Backend | Alta |
| SUBS-06 | Checkout y portal sin configuración o sin cliente Stripe | Verifica rechazo controlado si Stripe no está configurado o la cuenta aún no tiene cliente asociado para abrir el portal. | Backend | Media |
| SUBS-07 | Estado de checkout de otra cuenta | Verifica rechazo cuando un usuario consulta una `session_id` de Stripe que no le pertenece. | Backend | Alta |

## 5.9 Libro oficial y PDF

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| BOOK-01 | Preview del libro ovino/caprino | Verifica que el preview refleja secciones y estructura de especie ovina/caprina. | Backend + E2E | Media |
| BOOK-02 | Preview del libro porcino | Verifica secciones propias del libro porcino. | Backend + E2E | Media |
| BOOK-03 | Generación PDF ovino/caprino | Verifica que el PDF se genera correctamente con hojas esperadas y contenido base. | Backend | Alta |
| BOOK-04 | Generación PDF porcino | Verifica generación del PDF con estructura oficial porcina. | Backend | Alta |
| BOOK-05 | Acceso no autorizado al libro | Verifica rechazo cuando un usuario intenta exportar el libro de una explotación ajena. | Backend | Crítica |
| BOOK-06 | Columna `TIPO (9)` con código por `animal_type` | Verifica que el libro porcino muestra `V`, `CV`, `MR`, `HR`, `C`, `Rec` o `L` según el `animal_type` registrado. | Backend | Alta |
| BOOK-07 | Columna `TIPO (9)` con un único código en filas mixtas | Verifica que si una fila de balance mezcla categorías, el libro sigue mostrando un solo código y prioriza `L` cuando existan lechones. | Backend | Alta |

## 5.10 Seguridad, permisos e infraestructura

| ID | Prueba | Lógica validada | Cobertura | Prioridad |
| --- | --- | --- | --- | --- |
| SEC-01 | Acceso anónimo a endpoints protegidos | Verifica respuesta `401` en endpoints autenticados. | Backend | Crítica |
| SEC-02 | Acceso por rol incorrecto | Verifica rechazo cuando un rol no autorizado intenta ejecutar acciones de otro ámbito. | Backend + E2E | Crítica |
| SEC-03 | Recurso ajeno por URL directa | Verifica que no se puede acceder navegando manualmente a recursos no propios. | Backend + E2E | Alta |
| INFRA-01 | Bootstrap de base de datos | Verifica creación de esquema y aplicación ordenada de scripts SQL. | Backend | Alta |
| INFRA-02 | Validadores de dominio compartidos | Verifica REGA, crotales y DNI/NIF/CIF desde utilidades comunes. | Backend | Crítica |
| INFRA-03 | Mapeo uniforme de errores | Verifica que `DomainException` y otros errores de negocio se transforman en respuestas HTTP coherentes. | Backend | Alta |

## 6. Orden recomendado de validación conjunta
Para revisar estas pruebas conmigo de forma progresiva:

1. Autenticación y cuenta
2. Ganaderos
3. Explotaciones
4. Animales
5. Operaciones de explotación
6. Movimientos
7. Dashboard
8. Perfil y suscripción
9. Libro oficial y PDF
10. Seguridad e infraestructura

## 7. Resultado esperado del trabajo de automatización
Cuando la implementación esté completa, el repositorio debe disponer de:

- un proyecto `Pecualia.Test` integrado en `src/backend` y en la solución;
- una suite E2E Playwright para flujos completos;
- datos de prueba reutilizables;
- este documento como referencia oficial de qué prueba qué lógica.

Este documento es el contrato funcional para revisar las pruebas contigo antes y después de automatizarlas.

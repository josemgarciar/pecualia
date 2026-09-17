# Salida mediante TXT: diagnóstico confirmado

## Evidencia de producción (17 de septiembre de 2026)

Los logs de Azure del backend registran varios intentos fallidos entre las 12:10 y las 12:19 UTC (14:10–14:19 en Madrid). PostgreSQL devuelve el código 23514 porque el censo incumple la restricción census_ovino_caprino_non_negative_chk.

La pila sitúa el fallo en MovementService.RecordMovementSnapshotsAsync, al persistir el censo dentro de la transacción de CommitImportAsync. En la explotación seleccionada por el usuario se confirmó, mediante lectura de «Censos y balances», que «Menores de 4 meses» ya tenía un valor negativo. No fue necesario confirmar otra salida ni acceder al TXT privado.

## Por qué validaba el TXT y después fallaba

1. Seleccionar el TXT lo lee en el navegador y rellena el campo de texto editable.
2. «Validar archivo» envía el texto a /api/movements/imports/preview y comprueba las identificaciones y el estado de los animales.
3. «Confirmar importación» envía los datos a /api/movements/imports/commit. Registra la guía, los vínculos, las bajas, el balance y el censo en una transacción.
4. El cálculo del censo suma animales y nacimientos por edad y aplica los movimientos históricos de animales sin identificar. Las salidas pueden dejar negativo el grupo de menores de cuatro meses. El grupo de 4–12 meses ya tenía un mínimo de cero, pero el de menores de cuatro meses no.
5. PostgreSQL rechaza el censo negativo y se revierte toda la transacción: no se registra la guía ni quedan bajas parciales.
6. La excepción sin manejar podía producir una respuesta 500 que el navegador mostraba como un error de red/CORS.

## Corrección local

- Aplicar el mínimo de cero al grupo de menores de cuatro meses, después de sumar todos los movimientos, igual que en el grupo de 4–12 meses. Se conservan los saldos positivos. Esto afecta a las respuestas del censo, al censo que se guarda y a los censos del libro.
- Mantener la restricción de PostgreSQL y la transacción.
- Devolver los errores inesperados como JSON, con mensaje en español y referencia. CORS sigue limitado al frontend autorizado.
- Añadir una línea de diagnóstico «API failure» con referencia, ruta, tipo de excepción, código SQL y restricción/columna, sin TXT ni detalle de los animales. Los logs del framework son independientes y no deben compartirse completos sin revisión.

La corrección elimina el valor negativo que bloquea el guardado. No reconstruye las altas, salidas ni edades históricas de los lotes sin identificar. Si faltan registros o la evolución de edades produce un saldo incoherente, la conciliación histórica debe revisarse con el titular: limitar el resultado a cero no permite deducir cuántos animales faltaban en el histórico.

## Verificación manual después de desplegar el backend

1. Abrir la explotación afectada y «Censos y balances», año 2026. «Menores de 4 meses» debe mostrar cero en lugar del valor negativo. El total y los porcentajes se recalculan; este aumento del total mostrado no crea animales.
2. Revisar que la salida pendiente no figure ya registrada.
3. Seleccionar el TXT privado desde el equipo del cliente, validar sus 17 identificaciones y comprobar los datos de la guía.
4. Confirmar una sola vez cuando corresponda registrar esa salida real. /api/movements/imports/commit debe devolver HTTP 200.
5. Verificar una guía con 17 animales, las 17 bajas y el balance/censo de la fecha de la guía. No repetir pruebas de escritura sobre esos animales.
6. Si aparece otro error, anotar la referencia y buscar su línea «API failure». Compartir únicamente el tipo de excepción, SqlState, Constraint y Column; no compartir cookies, credenciales, cuerpos de peticiones ni TXT.

La comprobación realizada en producción fue de lectura. La corrección sigue local hasta su despliegue.

## Pruebas automatizadas

Se reprodujo la misma excepción 23514 en PostgreSQL local con 17 animales ficticios activos y una salida histórica de animales sin identificar. La prueba fallaba antes de la corrección y pasa después, verificando guía, bajas, vínculos, balance y censo no negativo.

También se comprueban el guardado normal de 17 animales, las proyecciones ovina/caprina con saldos negativos y positivos, los censos del libro y las respuestas HTTP/CORS ante errores inesperados.

MovementPostgresTests usa un esquema temporal independiente por prueba y elimina únicamente ese esquema al finalizar. Para ejecutarlo, definir PECUALIA_TEST_POSTGRES con una conexión PostgreSQL de pruebas y ejecutar:

    dotnet test src/backend/Pecualia.Test/Pecualia.Test.csproj

Sin PECUALIA_TEST_POSTGRES, las pruebas PostgreSQL se omiten. Las demás funcionan sin PostgreSQL. No usar una conexión de producción.

El manejo de errores utiliza IExceptionHandler: [documentación oficial de ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling?view=aspnetcore-8.0#iexceptionhandler).

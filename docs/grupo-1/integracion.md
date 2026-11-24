# Contrato HTTP y puesta en marcha

## Bases online · Atlas y Neon · 22/11/2026

La rama `correcciones-grupo-1` prepara dos conexiones remotas. Aún faltan crear los proyectos
y configurar sus credenciales; no se ha comprobado una conexión cloud real ni se han trasladado datos locales.

| Microservicio | Motor y destino | Consulta desde el navegador |
| --- | --- | --- |
| Clientes/Vehículos | PostgreSQL en Neon | Tables y SQL Editor |
| Mecánicos | MongoDB Atlas | Data Explorer, colección `mecanicos` |
| Categorías/Servicios | MySQL local, por ahora | Pendiente de elegir un alojamiento MySQL si también se quiere trasladar |

Con esta configuración no hace falta instalar Compass ni pgAdmin. Los motores PostgreSQL de Clientes
y MongoDB de Mecánicos podrán detenerse después de verificar la nube. MySQL de Servicios, MySQL de Usuarios,
PostgreSQL de Keycloak, las aplicaciones y Keycloak siguen consumiendo RAM local.
El Frontend mantiene su acceso mediante Gateway y las reglas/arquitecturas no cambian por alojar las bases fuera.

### 1. Crear MongoDB Atlas desde cero

1. Crear una cuenta en [MongoDB Atlas](https://cloud.mongodb.com/) y un proyecto para el taller.
2. Crear un cluster eligiendo la opción Free para esta demostración, si está disponible en la región elegida.
3. En Database Access crear el usuario de base de datos `mecanicos_app`, con contraseña propia y acceso
   `readWrite` únicamente sobre la base `mecanicos`. La cuenta del panel y el usuario de base son distintos.
4. En Network Access autorizar las IP públicas desde las que se ejecutarán las aplicaciones del equipo.
   Actualizar esas IP cuando cambie la red de la laptop.
5. En Data Explorer crear la base `mecanicos` y la colección `mecanicos`, inicialmente vacía.
6. En Validation pegar el objeto de `$jsonSchema` contenido en el campo `validator` del
   [esquema existente](../../microservices/mecanicos/AutoTallerPro.Mecanicos.Infrastructure/Esquema/001_mecanicos.js).
   Pegar el objeto `{ $jsonSchema: ... }`, sin `db.createCollection` ni los índices del script.
   Usar acción Error y nivel Strict.
7. En Indexes crear `ux_mecanicos_documento`, clave `{ "documento": 1 }`, con Unique habilitado,
   y `ix_mecanicos_nombre`, claves `{ "apellidos": 1, "nombres": 1 }`.
   El índice único es necesario para proteger los duplicados simultáneos, además de Result.
8. En Connect → Drivers seleccionar C# y copiar la URI `mongodb+srv://...`.
   Reemplazar la contraseña y aplicar codificación porcentual si tiene caracteres reservados en URI.
   Conservar las opciones de Atlas; no desactivar TLS ni la validación de certificados.

La selección de la base en la API usa `MONGO_MECANICOS_DB=mecanicos`, aunque la URI de Atlas no incluya una base.
Documentación: [conexión de drivers](https://www.mongodb.com/docs/atlas/driver-connection/),
[Data Explorer](https://www.mongodb.com/docs/atlas/atlas-ui/collections/) y
[validación desde el panel](https://www.mongodb.com/docs/atlas/atlas-ui/validation/).

### 2. Crear PostgreSQL en Neon desde cero

1. Crear una cuenta en [Neon](https://console.neon.tech/) y un proyecto para Clientes/Vehículos.
   Elegir el plan Free para la demostración y PostgreSQL 17 si el formulario ofrece esa versión.
2. Crear o seleccionar la base `clientes_vehiculos` y el rol que usará exclusivamente este microservicio.
3. En SQL Editor seleccionar esa base y ejecutar el contenido del
   [script PostgreSQL](../../microservices/clientes-vehiculos/AutoTallerPro.ClientesVehiculos.Infrastructure/Esquema/001_clientes_vehiculos.sql).
   Hacerlo sobre la base nueva del proyecto; el script crea tablas, índices y relación, sin insertar datos ficticios.
4. En Connect obtener Host, Database, Username y Password. La API espera el formato Npgsql de .NET,
   separado por punto y coma. Una URI `postgresql://...` o el comando `psql` no se pega directamente.
5. Configurar `SSL Mode=VerifyFull`: se verifican el certificado y el nombre del servidor.
   Si Connect muestra `SSL Mode=Require`, cambiar solo ese modo a `VerifyFull`.
6. En Tables comprobar `clientes` y `vehiculos`; el SQL Editor permite consultar las filas sin instalar un cliente.

Documentación: [conexión desde aplicaciones](https://neon.com/docs/connect/connect-from-any-app),
[SQL Editor](https://neon.com/docs/get-started/query-with-neon-sql-editor) y
[TLS de Npgsql](https://www.npgsql.org/doc/security.html).

### 3. Guardar las conexiones localmente

Agregar estas dos variables al `.env` local, que está excluido de Git. Sustituir los marcadores
por los datos de los proyectos recién creados. No pegar las credenciales en documentación, capturas o chats.

```dotenv
POSTGRES_CLIENTES_CONNECTION_STRING='Host=HOST_DE_NEON;Port=5432;Database=clientes_vehiculos;Username=USUARIO;Password=CLAVE;SSL Mode=VerifyFull'
MONGO_MECANICOS_CONNECTION_STRING='mongodb+srv://mecanicos_app:CLAVE_CODIFICADA@HOST_DE_ATLAS/?retryWrites=true&w=majority'
```

Estas líneas muestran el formato; no son conexiones operativas. Conservar las variables locales anteriores:
Compose todavía las evalúa y permiten volver al modo local sin perder los volúmenes existentes.
Las comillas simples exteriores de `.env` evitan que Compose interprete un `$` de una contraseña.
En Npgsql, si una contraseña contiene `;` u otros delimitadores, usar el escape propio de Npgsql
dentro del valor. La plantilla `.env.example` conserva los campos nuevos vacíos.

Las APIs dan prioridad a `Database:ConnectionString` cuando está configurada. Si falta, mantienen
Host/Port/Name/User/Password para el modo local. El override cloud exige ambas cadenas para evitar
que un campo vacío conecte por accidente a las bases locales.

### 4. Activar y comprobar el modo nube

Desde la raíz, después de crear los esquemas y completar el `.env`:

```sh
docker compose -f docker-compose.yml -f docker-compose.nube.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.nube.yml up -d --build --no-deps ms-clientes ms-mecanicos
```

Entrar por `http://127.0.0.1:5105/` con una cuenta de negocio habilitada. Crear un cliente y su vehículo,
y un mecánico; comprobar las filas/documento en los paneles web, editar, refrescar y comprobar el cambio.
Verificar que un documento duplicado devuelve conflicto. Retirar únicamente esos registros de prueba
desde la aplicación, borrando el vehículo antes del cliente.
`/salud` indica que la API responde; no prueba por sí sola el acceso a una base.

Solo después de confirmar el CRUD remoto, detener los dos motores locales para liberar su RAM:

```sh
docker compose stop postgres-clientes mongo-mecanicos
```

Los volúmenes se conservan. A partir de entonces arrancar el conjunto con ambos archivos:

```sh
docker compose -f docker-compose.yml -f docker-compose.nube.yml up -d
```

El override asigna esas dos bases al perfil `bases-locales`, inactivo de forma predeterminada.
Un `docker compose up -d` sin el archivo cloud vuelve al modo local y puede arrancarlas de nuevo.
Si esos contenedores ya estaban ejecutándose, el perfil no los detiene automáticamente.

Para volver al modo local, usar solo `docker-compose.yml` y recrear las dos APIs:

```sh
docker compose up -d postgres-clientes mongo-mecanicos
docker compose up -d --build --no-deps ms-clientes ms-mecanicos
```

El modo nube parte de bases nuevas vacías: no copia los datos que existan en los volúmenes locales.
La conexión requiere internet; no se garantiza una demostración offline con Atlas/Neon.
El respaldo anterior de correcciones permanece en Git stash y no fue aplicado sobre el login del equipo.

## Rutas

Gateway conserva los prefijos existentes y elimina el prefijo al reenviar.

| Recurso | Ruta pública | Ruta interna | Razor |
| --- | --- | --- | --- |
| Clientes | `/api/clientes-vehiculos/clientes` | `/clientes` | `/Clientes/Index` |
| Vehículos | `/api/clientes-vehiculos/vehiculos` | `/vehiculos` | `/Vehiculos/Index` |
| Categorías | `/api/servicios/categorias` | `/categorias` | `/Categorias/Index` |
| Servicios | `/api/servicios/servicios` | `/servicios` | `/Servicios/Index` |
| Mecánicos | `/api/mecanicos/mecanicos` | `/mecanicos` | `/Mecanicos/Index` |

Cada recurso ofrece `GET /`, `GET /{id}`, `POST /`, `PUT /{id}` y `DELETE /{id}` relativos a su ruta.
GET del listado acepta `buscar`, `ordenar` y `descendente` (booleano). Ejemplo:
`GET /api/servicios/servicios?buscar=lavado&ordenar=preciobase&descendente=true`.
Los criterios de la interfaz están implementados en cada servicio; `fecha` corresponde a la fecha de registro y el desempate usa Id.
P1 filtra clientes por documento/nombre y vehículos por placa; P2 por nombre; P3 por nombre, apellido, documento o especialidad.
Actualizar listado vuelve a consultar la API; no existe una lista ficticia en el Frontend.

HTTP: 200 consulta/actualización/eliminación; 201 creación con Location público; 400 validación;
401 token ausente/inválido; 403 rol insuficiente; 404 ausente; 409 duplicado o relación en uso; 500 error inesperado.
Los errores de negocio incluyen `title` en español y `codigo` en un ProblemDetails.
Las tres rutas `/salud` siguen anónimas y solo verifican el proceso HTTP, no la base.

## Autenticación de las APIs

Cada API valida firma, emisor exacto, audiencia `autotaller-api`, vencimiento y `typ=Bearer` del payload de Keycloak.
Solo acepta `Administrador` o `Recepcionista` leídos de `realm_access.roles`; elimina roles directos ajenos a ese contrato.
Ambos roles pueden ejecutar CRUD en los módulos del Grupo 1, coherente con la política actual del Gateway.
No se confía en encabezados de identidad ni se admite una llamada directa sin JWT.

Configuración de cada API, ya implementada dentro del módulo:

- `Database:Host`, `Port`, `Name`, `User`, `Password`, y `AuthSource` en MongoDB: se consumen los nombres de Compose.
- `Authentication:Authority`: emisor público de Keycloak; valor local inicial `http://127.0.0.1:8180/realms/autotaller`.
- `Authentication:Audience`: `autotaller-api`.
- `Authentication:MetadataAddress`: discovery de Keycloak. Fuera de Docker deriva del emisor.
- `Authentication:PermitirMetadataHttpLocal`: false por defecto; HTTP de metadatos se admite en Development o con esta opción local explícita.

Para el despliegue actual de Compose, P6 debe agregar en **cada API del Grupo 1**, bajo `environment`:

```yaml
Authentication__Authority: http://127.0.0.1:${KEYCLOAK_PORT:-8180}/realms/autotaller
Authentication__MetadataAddress: http://keycloak:8080/realms/autotaller/.well-known/openid-configuration
Authentication__PermitirMetadataHttpLocal: "true"
```

El emisor debe coincidir con el de los tokens; la URL interna de discovery no sustituye el issuer.
Esta propuesta usa HTTP exclusivamente para la infraestructura local existente; producción requiere HTTPS.
El cambio de Compose no se aplicó porque pertenece a P6 y requiere autorización según el alcance recibido.

## Integración de sesión y Frontend · P4/P5

`GrupoUno/ClienteGateway` llama exclusivamente al Gateway y obtiene `access_token` con `HttpContext.GetTokenAsync`.
Usa un esquema de autenticación predeterminado registrado por el flujo de sesión del equipo.
No toma tokens de parámetros, HTML, archivos del proyecto o un formulario propio.
No sigue redirecciones HTTP del Gateway, y traduce 401/403 e indisponibilidad a mensajes de usuario.
El antiforgery de Razor se conserva en POST, PUT lógico y confirmación de eliminación.

La sesión real está integrada desde el PR #7: `frontend-app/Seguridad/SesionKeycloak.cs` guarda el access token
en la cookie, lo renueva con el refresh token y atiende login/logout. Sin sesión, las páginas redirigen al login de Keycloak.
No se crea un login alternativo ni se usa un token técnico para actuar como usuario.

`GrupoUno:GatewayUrl` permite configurar la URL del proxy (debe terminar en `/`).
Por defecto usa `http://127.0.0.1:5100/` con SDK y `http://gateway:8080/` dentro del contenedor .NET.
No es necesario exponer puertos de bases en el navegador.

P5 debe sustituir únicamente los botones pendientes del menú global por enlaces a:
`/Clientes/Index`, `/Vehiculos/Index`, `/Servicios/Index` y `/Mecanicos/Index`.
Categorías se accede desde la navegación interna del catálogo. Los cinco módulos incluyen navegación mutua.
No se modificó `_Layout.cshtml`, `site.css`, la página de inicio ni Usuarios.

## Compilar y verificar

Desde la raíz del repositorio, con SDK .NET 10:

```sh
dotnet restore tests/AutoTallerPro.GrupoUno.Tests
dotnet build tests/AutoTallerPro.GrupoUno.Tests --no-restore --disable-build-servers -m:1 -nr:false
dotnet test tests/AutoTallerPro.GrupoUno.Tests --no-restore
python3 scripts/grupo-1/verificar_persistencia.py
```

El proyecto de pruebas referencia las tres APIs, sus capas, Razor y un ejecutable de verificación.
No se modificó la solución global; ejecute expresamente este proyecto para incluir sus pruebas.
La ejecución normal omite tres pruebas que requieren bases. El script crea tres contenedores propios,
esquemas idempotentes, credenciales aleatorias y recursos tmpfs, ejecuta todas las pruebas y retira solo sus contenedores identificados por etiqueta.
No usa Compose, no modifica `.env`, no elimina volúmenes y no consulta bases del equipo.
En Windows puede usarse `python` en lugar de `python3`.

Las pruebas de Gateway compilan por enlace el `Program.cs` original y cargan su `appsettings.json` original;
solo sustituyen destinos por puertos temporales y claves JWKS por claves de prueba. No cambian políticas, rutas ni transformaciones.
La sesión de pruebas se registra exclusivamente en el host de test. Sin bases, los puertos usan dobles de repositorio;
con el script, Razor → YARP → APIs utiliza PostgreSQL/MySQL/MongoDB reales.
Esto verifica el contrato de sesión y forwarding; no acredita un login real de Keycloak ni el despliegue global de Compose.

Revisión de Chrome opcional (requiere `playwright-core` y un Chrome local):

```sh
npm install --prefix /tmp/autotaller-ui playwright-core --no-audit --no-fund
# Inicie el Frontend del repositorio antes de la revisión.
PLAYWRIGHT_MODULE_PATH=/tmp/autotaller-ui/node_modules/playwright-core CHROME_PATH="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" GRUPO1_FRONTEND_URL=http://127.0.0.1:5105 node scripts/grupo-1/verificar_interfaz.cjs
```

La prueba de interfaz abre veinte páginas, verifica validaciones cliente y antiforgery y guarda capturas en `docs/grupo-1/evidencia/`.
Esas capturas muestran la interfaz sin sesión del equipo, y no constituyen una demostración de login funcional.

## Fuentes técnicas

- [Npgsql](https://www.nuget.org/packages/Npgsql/10.0.3).
- [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/2.6.2).
- [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver/3.14.0).
- [Configuración YARP](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/config-files).
- [WebApplicationFactory con Kestrel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1.usekestrel?view=aspnetcore-10.0).

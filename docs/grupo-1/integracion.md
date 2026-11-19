# Contrato HTTP y puesta en marcha

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

P4/P5 deben configurar su sesión real para que `AuthenticateAsync` pueda recuperar el access token del usuario,
renovarlo cuando corresponda y atender login/logout. Este cambio no se implementó en `frontend-app/Program.cs`.
Sin esa integración, las páginas muestran la dependencia de sesión y no consultan datos como anónimas.
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

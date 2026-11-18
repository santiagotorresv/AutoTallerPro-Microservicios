# Infraestructura Docker · Etapas 3A y 3B

El proyecto Compose `autotaller-ec3` reúne seis aplicaciones .NET 10 y siete
servicios de infraestructura para desarrollo local. Todos se resuelven por
nombre de servicio en la red `autotaller-ec3_default`.

## Preparar y ejecutar

Con Docker Desktop en modo de contenedores Linux, ejecuta estos comandos desde
la raíz del repositorio.

```powershell
docker version
docker compose version
docker ps
```

Para crear el archivo local por primera vez, copia la plantilla y asigna una
contraseña aleatoria distinta a cada variable que termina en `_PASSWORD`.
Conserva el archivo en tu equipo.

```powershell
if (-not (Test-Path -LiteralPath '.env')) {
    Copy-Item -LiteralPath '.env.example' -Destination '.env'
}
```

Si hay un puerto ocupado, cambia solo su variable `_PORT` en `.env` y usa el
nuevo puerto al conectarte desde Windows. Las aplicaciones dentro de Docker
siguen usando el puerto interno del servicio. Los puertos de las seis aplicaciones
están definidos en Compose. Ejecuta estos comandos para compilar, levantar y
revisar el conjunto.

```powershell
docker compose config --quiet
docker compose build
docker compose up -d --wait --wait-timeout 240
docker compose ps
```

`config --quiet` valida el YAML y la interpolación. La salida de
`docker compose config` sin `--quiet` incluye las credenciales resueltas.
Usa `docker compose stop` para detener el entorno y conservar sus volúmenes.
`docker compose up -d --wait --wait-timeout 240` lo reanuda. Para observar el
arranque de un servicio, usa, por ejemplo, `docker compose logs --tail 80 keycloak`.

Si el registro interrumpe una descarga, ejecuta
`docker compose --parallel 1 pull --ignore-buildable` y vuelve a levantar el
proyecto.

## Aplicaciones .NET y rutas del Gateway

| Servicio | DNS dentro de Docker | Desde Windows | Comprobación |
| --- | --- | --- | --- |
| `frontend` | `frontend:8080` | `http://127.0.0.1:5105` | `GET /` |
| `gateway` | `gateway:8080` | `http://127.0.0.1:5100` | Cuatro rutas de salud por YARP |
| `ms-clientes` | `ms-clientes:8080` | `http://127.0.0.1:5101` | `GET /salud` |
| `ms-servicios` | `ms-servicios:8080` | `http://127.0.0.1:5102` | `GET /salud` |
| `ms-mecanicos` | `ms-mecanicos:8080` | `http://127.0.0.1:5103` | `GET /salud` |
| `ms-usuarios` | `ms-usuarios:8080` | `http://127.0.0.1:5104` | `GET /salud` |

Cada Dockerfile publica su proyecto y las bibliotecas que referencia con el
SDK .NET 10. La imagen final contiene la publicación y el runtime ASP.NET Core
10 y corre como usuario sin privilegios. El contexto es la raíz para incluir
`Directory.Build.props`, `global.json` y los proyectos referenciados.

YARP retira el prefijo de módulo antes de reenviar la solicitud. Por ejemplo,
`GET /api/mecanicos/salud` llega a `GET http://ms-mecanicos:8080/salud`.
Al implementar sus endpoints, cada integrante define dentro de su API solo la
ruta restante. La configuración para ejecución directa con el SDK usa los
puertos locales. Compose sustituye los destinos con
`ReverseProxy__Clusters__<servicio>__Destinations__principal__Address`.
[Transformación PathRemovePrefix de YARP](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/servers/yarp/transforms-request?view=aspnetcore-10.0).

| Prefijo público | Destino en Docker | Prueba desde Windows |
| --- | --- | --- |
| `/api/clientes-vehiculos/{**catch-all}` | `http://ms-clientes:8080/` | `GET /api/clientes-vehiculos/salud` |
| `/api/servicios/{**catch-all}` | `http://ms-servicios:8080/` | `GET /api/servicios/salud` |
| `/api/mecanicos/{**catch-all}` | `http://ms-mecanicos:8080/` | `GET /api/mecanicos/salud` |
| `/api/usuarios/{**catch-all}` | `http://ms-usuarios:8080/` | `GET /api/usuarios/salud` |

`docker compose ps` muestra las aplicaciones .NET como `running`. Comprueba
su respuesta HTTP en los puertos indicados. `/salud` comprueba el proceso web.
El estado de cada base se comprueba de forma independiente con su healthcheck.

## Direcciones y credenciales

| Servicio | Responsabilidad | DNS y puerto en Docker | Desde Windows | Variables de conexión en `.env` |
| --- | --- | --- | --- | --- |
| `postgres-clientes` | PostgreSQL 17 de Clientes/Vehículos | `postgres-clientes:5432` | `127.0.0.1:5433` | `POSTGRES_CLIENTES_DB`, `POSTGRES_CLIENTES_USER`, `POSTGRES_CLIENTES_PASSWORD`, `POSTGRES_CLIENTES_PORT` |
| `mysql-servicios` | MySQL 8.4 de Servicios | `mysql-servicios:3306` | `127.0.0.1:3307` | `MYSQL_SERVICIOS_DB`, `MYSQL_SERVICIOS_USER`, `MYSQL_SERVICIOS_PASSWORD`, `MYSQL_SERVICIOS_PORT` |
| `mongo-mecanicos` | MongoDB 8.0 de Mecánicos | `mongo-mecanicos:27017` | `127.0.0.1:27018` | `MONGO_MECANICOS_DB`, `MONGO_MECANICOS_USER`, `MONGO_MECANICOS_PASSWORD`, `MONGO_MECANICOS_PORT` |
| `mysql-usuarios` | MySQL 8.4 del microservicio Usuarios | `mysql-usuarios:3306` | `127.0.0.1:3308` | `MYSQL_USUARIOS_DB`, `MYSQL_USUARIOS_USER`, `MYSQL_USUARIOS_PASSWORD`, `MYSQL_USUARIOS_PORT` |
| `postgres-keycloak` | PostgreSQL 17 de uso exclusivo de Keycloak | `postgres-keycloak:5432` | Sin puerto publicado | `POSTGRES_KEYCLOAK_DB`, `POSTGRES_KEYCLOAK_USER`, `POSTGRES_KEYCLOAK_PASSWORD` |
| `keycloak` | Servidor de identidad en modo desarrollo | `keycloak:8080` | `http://127.0.0.1:8180/admin/` | `KEYCLOAK_PORT`, `KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME`, `KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD` |
| `mailpit` | Bandeja de correo de prueba y servidor SMTP | `mailpit:8025` (web), `mailpit:1025` (SMTP) | `http://127.0.0.1:8025/` (web) | `MAILPIT_WEB_PORT` |

Una aplicación en la red Compose configura el DNS, el puerto interno, el nombre
de base y la cuenta de su fila. En Windows, usa `127.0.0.1` y el puerto publicado
en pgAdmin, MySQL Workbench, MongoDB Compass o el cliente que uses. Para MongoDB,
configura también `authSource` con el valor de `MONGO_MECANICOS_DB`. Por
ejemplo, la forma de la URI interna es
`mongodb://<usuario>:<contraseña>@mongo-mecanicos:27017/<base>?authSource=<base>`.
Codifica los caracteres reservados de la URI si eliges contraseñas que los
contengan. Las cuentas root de MySQL y MongoDB están reservadas para tareas
administrativas. El script `mongo/crear-usuario.js` inicializa la cuenta de
aplicación de MongoDB con permiso `readWrite` solo en su propia base.

Compose toma las variables `_DB`, `_USER` y `_PASSWORD` de la fila de cada
módulo e inyecta `Database__Name`, `Database__User` y `Database__Password`
en su API. `Database__Host` y `Database__Port` usan el DNS y el puerto
internos. Mecánicos recibe también `Database__AuthSource`. En .NET se leen
como `Database:Host`, `Database:Port`, `Database:Name`, `Database:User`,
`Database:Password` y, donde aplica, `Database:AuthSource`. La configuración
está preparada para los futuros drivers y se inyecta solo al arrancar.

Usuarios ya usa estas variables para su conexión MySQL. El esquema y la
verificación de la conexión se documentan en la
[guía de persistencia de Usuarios](mysql/usuarios/README.md).

## Trabajo por módulo

- Clientes/Vehículos implementará el acceso a PostgreSQL, los endpoints del
  módulo y sus páginas Razor.
- Servicios implementará el acceso a su MySQL, sus endpoints y sus páginas.
- Mecánicos implementará el acceso a MongoDB, sus endpoints y sus páginas.
- Usuarios implementará el acceso a su MySQL y conectará las páginas de
  administración existentes con sus endpoints. La integración de identidad
  con Keycloak se acordará en una etapa posterior.
- La configuración compartida de Gateway y Compose seguirá la convención de rutas
  anterior. El Frontend se sirve en su puerto propio.

## Keycloak y Mailpit

Abre la consola de Keycloak en el navegador de Windows y usa la cuenta
administrativa inicial de `.env`. Keycloak espera el estado saludable de su
PostgreSQL antes de arrancar y usa esa base para guardar su configuración.
Mailpit recibe correo de desarrollo por SMTP en `mailpit:1025`, sin autenticación
ni TLS en la red Compose. Los correos recibidos se muestran en su interfaz web.

## Persistencia y comprobación

Cada base usa un volumen de datos propio. MongoDB también tiene un volumen
separado para `/data/configdb` y Mailpit guarda su bandeja en `mailpit-data`.
Compose antepone `autotaller-ec3_` a los nombres de los volúmenes.

Los healthchecks SQL hacen un `SELECT 1` autenticado y el de MongoDB realiza
un ping autenticado. Keycloak comprueba su estado de disponibilidad en su
puerto interno de administración y Mailpit usa `readyz`. Para comprobar la
persistencia sin crear datos de prueba, anota los nombres de los volúmenes,
detén y levanta el proyecto, y vuelve a consultar sus montajes y estado.

```powershell
docker volume ls --filter label=com.docker.compose.project=autotaller-ec3
docker compose ps -q | ForEach-Object {
    docker inspect --format '{{.Name}} {{range .Mounts}}{{if eq .Type "volume"}}{{.Name}} -> {{.Destination}} {{end}}{{end}}' $_
}
docker compose stop
docker compose up -d --wait --wait-timeout 240
docker compose ps
```

Las variables de inicialización se aplican al crear los volúmenes por primera
vez. Para rotar una contraseña existente hay que cambiarla en su servicio y
actualizar `.env`; editar el archivo por sí solo no cambia la cuenta persistida.

Referencias oficiales de las imágenes y su configuración:
[PostgreSQL](https://hub.docker.com/_/postgres),
[MySQL](https://hub.docker.com/_/mysql),
[MongoDB](https://hub.docker.com/_/mongo),
[Keycloak](https://www.keycloak.org/server/containers) y
[Mailpit](https://mailpit.axllent.org/docs/install/docker/).

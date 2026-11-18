# Persistencia MySQL de Usuarios

El esquema de P4 se instala en la base configurada para `mysql-usuarios`.
`FichasAdministrativas` vincula el ID local entero con el par de Keycloak
`(iss, sub)` y guarda autores y fechas UTC de creación y modificación.
`AuditoriaAdministrativa` registra actor, acción, objetivo, fecha UTC,
resultado, código de motivo y correlación. La auditoría mantiene las
referencias como valores históricos, independientes de la existencia de la
ficha o del usuario. Los datos de identidad y acceso se consultarán en Keycloak.

## Inicializar la base local

Desde la raíz, abre PowerShell y confirma que tienes la configuración de
Compose ya preparada según la [guía de Docker](../../README.md). Arranca
`mysql-usuarios` y revisa qué tablas hay en la base configurada.

```powershell
docker compose up -d --wait mysql-usuarios
$clienteUsuarios = 'MYSQL_PWD="$MYSQL_PASSWORD" exec mysql --protocol=TCP --host=127.0.0.1 --user="$MYSQL_USER" --database="$MYSQL_DATABASE"'
'SHOW TABLES;' | docker compose exec -T mysql-usuarios sh -ec $clienteUsuarios
```

El script usa `CREATE TABLE IF NOT EXISTS`. Si alguna de las dos tablas ya
existe, revisa su definición con `SHOW CREATE TABLE` antes de aplicar el
script. Cualquier diferencia debe resolverse con un cambio de esquema
explícito. Crear tablas y verificar la conexión son operaciones separadas
del arranque de la API.

```powershell
Get-Content -Raw infra/mysql/usuarios/001_persistencia_usuarios.sql |
    docker compose exec -T mysql-usuarios sh -ec $clienteUsuarios
'SHOW TABLES;' | docker compose exec -T mysql-usuarios sh -ec $clienteUsuarios
docker compose up -d --build --no-deps ms-usuarios
docker compose exec -T ms-usuarios dotnet AutoTallerPro.Usuarios.Api.dll --verificar-mysql
```

El verificador usa el mismo origen de conexiones que los repositorios y
ejecuta `SELECT 1` sin iniciar el servidor HTTP. La API lee
`Database__Host`, `Database__Port`, `Database__Name`, `Database__User` y
`Database__Password`. Dentro de Compose usa `mysql-usuarios:3306`. Para el
SDK en Windows configura el host `127.0.0.1` y el puerto publicado, que por
defecto es 3308, con la cuenta de aplicación de Usuarios.

## Esquema de alta P4.4

Después de `001`, aplica `002_altas_usuarios.sql` en la base de Usuarios
para habilitar las rutas administrativas. Crea de forma aditiva la tabla
`AltasUsuarios` para la idempotencia, la reserva de correo y el progreso
de Keycloak a MySQL. El esquema anterior se conserva. La instalación del
esquema sigue siendo explícita y separada del arranque.

```powershell
Get-Content -Raw infra/mysql/usuarios/002_altas_usuarios.sql |
    docker compose exec -T mysql-usuarios sh -ec $clienteUsuarios
'SHOW CREATE TABLE AltasUsuarios;' | docker compose exec -T mysql-usuarios sh -ec $clienteUsuarios
```

El contrato HTTP y los estados de recuperación están en la
[guía de Usuarios para P5](../../../docs/USUARIOS_API.md).

## Probar con una base aislada

La suite incluye las pruebas de JWT y reglas de P4.1. Para activar las de
persistencia, proporciona una instancia MySQL 8.4 temporal, una cuenta
`usuarios_pruebas` y una base vacía con nombre
`usuarios_pruebas_<uuid de 32 caracteres hexadecimales en minúsculas>`.
La prueba aplica el SQL versionado. Ejecuta el siguiente bloque en una
sesión nueva de PowerShell con Docker Desktop iniciado.

```powershell
$nombre = 'autotaller-usuarios-pruebas-' + [guid]::NewGuid().ToString('N')
$env:MYSQL_DATABASE = 'usuarios_pruebas_' + [guid]::NewGuid().ToString('N')
$env:MYSQL_USER = 'usuarios_pruebas'
$env:MYSQL_PASSWORD = [guid]::NewGuid().ToString('N')
$env:MYSQL_ROOT_PASSWORD = [guid]::NewGuid().ToString('N')
$clientePrueba = 'MYSQL_PWD="$MYSQL_PASSWORD" exec mysql --protocol=TCP --host=127.0.0.1 --user="$MYSQL_USER" --database="$MYSQL_DATABASE" --batch --skip-column-names --execute="SELECT 1;"'
$instancia = $null
try {
    $instancia = docker run --rm --detach --name $nombre --tmpfs /var/lib/mysql --publish '127.0.0.1::3306' --env MYSQL_DATABASE --env MYSQL_USER --env MYSQL_PASSWORD --env MYSQL_ROOT_PASSWORD mysql:8.4
    if ($LASTEXITCODE -ne 0) { throw 'MySQL de prueba no arrancó.' }
    $publicado = docker port $instancia 3306/tcp
    if ($publicado -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Puerto de prueba inesperado.' }
    $puerto = $Matches[1]
    $disponible = $false
    for ($intento = 0; $intento -lt 50; $intento++) {
        $estado = docker exec $instancia sh -ec $clientePrueba 2>$null
        if ($LASTEXITCODE -eq 0 -and $estado -eq '1') { $disponible = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $disponible) { throw 'MySQL de prueba no está listo.' }
    $env:AUTOTALLER_USUARIOS_TEST_MYSQL = "Server=127.0.0.1;Port=$puerto;Database=$env:MYSQL_DATABASE;User ID=$env:MYSQL_USER;Password=$env:MYSQL_PASSWORD"
    dotnet test tests/AutoTallerPro.Usuarios.Tests/AutoTallerPro.Usuarios.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Falló la suite de Usuarios.' }
} finally {
    if ($instancia -match '^[0-9a-f]{64}$') { docker stop $instancia | Out-Null }
    Remove-Item Env:AUTOTALLER_USUARIOS_TEST_MYSQL -ErrorAction SilentlyContinue
    Remove-Item Env:MYSQL_DATABASE, Env:MYSQL_USER, Env:MYSQL_PASSWORD, Env:MYSQL_ROOT_PASSWORD
}
```

Si ejecutas la suite sin `AUTOTALLER_USUARIOS_TEST_MYSQL`, se omiten las
pruebas de integración y se ejecutan las de reglas y JWT.

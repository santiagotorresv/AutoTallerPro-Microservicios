# AutoTaller Pro — EC3 Microservicios

Solución .NET 10 con 19 proyectos: cuatro microservicios independientes con
cuatro capas cada uno, un Gateway, una biblioteca de contratos vacía y el
Frontend Razor Pages. Incluye la base visual, Inicio, Privacy, Error y la interfaz
de administración de Usuarios sin conexión al backend y con sus operaciones
desactivadas.

## Estructura

Cada nombre de proyecto del árbol corresponde a una carpeta con su archivo
`.csproj` del mismo nombre.

```text
AutoTallerPro-Microservicios/
├── AutoTallerPro.sln
├── docker-compose.yml                  # 6 aplicaciones y 7 servicios de infraestructura
├── .env.example
├── Directory.Build.props
├── global.json
├── frontend-app/
│   ├── AutoTallerPro.Frontend.csproj
│   ├── Pages/                           # Inicio, Privacy, Error, Usuarios y layout
│   └── wwwroot/                         # Recursos visuales locales
├── gateway/
│   └── AutoTallerPro.Gateway/
├── microservices/
│   ├── clientes-vehiculos/
│   │   ├── AutoTallerPro.ClientesVehiculos.Api/
│   │   ├── AutoTallerPro.ClientesVehiculos.Application/
│   │   ├── AutoTallerPro.ClientesVehiculos.Domain/
│   │   └── AutoTallerPro.ClientesVehiculos.Infrastructure/
│   ├── servicios/
│   │   ├── AutoTallerPro.Servicios.Api/
│   │   ├── AutoTallerPro.Servicios.Application/
│   │   ├── AutoTallerPro.Servicios.Domain/
│   │   └── AutoTallerPro.Servicios.Infrastructure/
│   ├── mecanicos/
│   │   ├── AutoTallerPro.Mecanicos.Api/
│   │   ├── AutoTallerPro.Mecanicos.Application/
│   │   ├── AutoTallerPro.Mecanicos.Domain/
│   │   └── AutoTallerPro.Mecanicos.Infrastructure/
│   └── usuarios/
│       ├── AutoTallerPro.Usuarios.Api/
│       ├── AutoTallerPro.Usuarios.Application/
│       ├── AutoTallerPro.Usuarios.Domain/
│       └── AutoTallerPro.Usuarios.Infrastructure/
├── shared/
│   └── Contracts/
│       └── AutoTallerPro.Contracts.csproj
├── infra/                                # Guía de Docker e inicialización de MongoDB
├── tests/                                # Reservado
└── docs/
    └── arquitectura.md
```

## Compilar

Requisito: SDK estable .NET 10. `global.json` permite las bandas de características
estables de .NET 10; `Directory.Build.props` centraliza `net10.0`, nullable e
implicit usings. Los nombres de ensamblado y namespaces raíz se derivan de los
nombres de los proyectos.

Desde la raíz:

```powershell
dotnet restore AutoTallerPro.sln
dotnet build AutoTallerPro.sln --no-restore
dotnet sln AutoTallerPro.sln list
```

## Arranque con Docker

### En un solo paso

Con Docker Desktop abierto y Node.js 22 o posterior, ejecuta desde la raíz:

```powershell
node infra/levantar-entorno.cjs
```

El script se puede repetir y conserva lo que ya existe. Hace estos pasos:
1. Crea `.env` si falta y genera las contraseñas locales vacías.
2. Compila y levanta los contenedores.
3. Importa el realm de Keycloak si no existe.
4. Configura la cuenta técnica de Usuarios, el tema y los secretos de los clientes.
5. Crea las tablas y colecciones de los cuatro microservicios, sin datos de ejemplo.
6. La primera vez, crea el administrador inicial.

Para una demostración, `node infra/levantar-entorno.cjs --datos-demo` agrega
datos de ejemplo de `infra/demo/`: 3 clientes con sus vehículos, 2 categorías,
4 servicios y 3 mecánicos. Sus documentos y placas empiezan por `DEMO`. Se
pueden agregar varias veces sin duplicarse. Para ver las bases desde DBeaver o
MongoDB Compass, consulta la [guía de Docker](infra/README.md#ver-las-bases-con-dbeaver-y-compass).

La invitación del administrador llega a Mailpit, en `http://127.0.0.1:8025`.
Ábrela, verifica el correo, elige la contraseña y entra en
`http://127.0.0.1:5105` con el usuario que muestra el script. Para usar un
correo real, ejecuta `node infra/levantar-entorno.cjs --admin tu@correo.com`
antes de crear cualquier cuenta. Si ya existe un volumen del proyecto sin
`.env`, el script se detiene, porque las contraseñas nuevas no coincidirían.

### Paso a paso

Con Docker Desktop en modo de contenedores Linux, clona el repositorio y abre la
raíz. Crea `.env` desde la plantilla y asigna una contraseña aleatoria distinta
a cada variable que termina en `_PASSWORD` antes del primer arranque. Conserva
ese archivo local.

```powershell
if (-not (Test-Path -LiteralPath '.env')) {
    Copy-Item -LiteralPath '.env.example' -Destination '.env'
}

docker compose config --quiet
docker compose build
docker compose up -d --wait --wait-timeout 240
docker compose ps
```

Inicio abre en `http://127.0.0.1:5105/`. El Gateway publica las APIs en
`http://127.0.0.1:5100/api/<módulo>/`. Para detener los 13 contenedores
conservando sus volúmenes, ejecuta `docker compose stop`.

Consulta los DNS internos, las rutas exactas, las variables y las tareas de
cada módulo en [infra/README.md](infra/README.md).

## Arranque directo con el SDK

Si los contenedores de las aplicaciones están detenidos, ejecuta cada comando
en una terminal independiente desde la raíz.

```powershell
dotnet run --project microservices/clientes-vehiculos/AutoTallerPro.ClientesVehiculos.Api
dotnet run --project microservices/servicios/AutoTallerPro.Servicios.Api
dotnet run --project microservices/mecanicos/AutoTallerPro.Mecanicos.Api
dotnet run --project microservices/usuarios/AutoTallerPro.Usuarios.Api
dotnet run --project gateway/AutoTallerPro.Gateway
dotnet run --project frontend-app/AutoTallerPro.Frontend.csproj
```

| Aplicación | URL local | Comprobación |
| --- | --- | --- |
| Gateway | http://localhost:5100 | `GET /api/mecanicos/salud`, con la API de Mecánicos en ejecución |
| Frontend | http://localhost:5105 | `GET /` devuelve la página de inicio |
| Clientes/Vehículos | http://localhost:5101 | `GET /salud` |
| Servicios | http://localhost:5102 | `GET /salud` |
| Mecánicos | http://localhost:5103 | `GET /salud` |
| Usuarios | http://localhost:5104 | `GET /salud` |

Los perfiles HTTP están en `Properties/launchSettings.json`. Cada microservicio
expone únicamente `GET /salud`, con HTTP 200 y JSON `{"estado":"OK"}`. Es una
comprobación de que el proceso HTTP responde; no comprueba bases de datos ni
servicios externos.

El Gateway usa YARP 2.3.0. Al arrancar con el SDK enruta a las APIs por sus puertos
locales. En Compose usa el nombre DNS de cada servicio. Las API exponen salud
HTTP y reciben su configuración de base de datos al iniciar el contenedor.
La implementación de la persistencia y la autenticación queda para otras etapas.

Las responsabilidades, referencias y decisiones pendientes están en
[docs/arquitectura.md](docs/arquitectura.md).
El alcance visual y el arranque del Frontend están en
[frontend-app/README.md](frontend-app/README.md).

## Verificación de la etapa 1

Comprobada el 17 de noviembre de 2026 con SDK 10.0.303:

- Restauración correcta de los 18 proyectos.
- `dotnet build AutoTallerPro.sln --no-restore --disable-build-servers -m:1 -nr:false`:
  compilación correcta, 0 errores y 0 advertencias.
- Los 18 proyectos existentes están registrados en la solución.
- Verificación de las 16 referencias entre proyectos: respetan las direcciones
  documentadas, sin ciclos ni referencias entre microservicios.
- Arranque independiente y petición HTTP real a los cuatro microservicios:
  `GET /salud` devuelve 200 y `{"estado":"OK"}`; `POST /salud` devuelve 405.
- Arranque independiente del Gateway con configuración vacía: `GET /` devuelve 404.

Las pruebas HTTP utilizaron puertos locales temporales y los cinco procesos se
detuvieron al finalizar. No se ejecutaron Docker, bases de datos ni servicios
externos.

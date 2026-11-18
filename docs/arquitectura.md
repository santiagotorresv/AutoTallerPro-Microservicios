# Arquitectura inicial

## Responsabilidades

| Proyecto | Responsabilidad prevista |
| --- | --- |
| `AutoTallerPro.ClientesVehiculos.Api` | Entrada HTTP y composición del microservicio Clientes/Vehículos. |
| `AutoTallerPro.ClientesVehiculos.Application` | Casos de uso y puertos de aplicación de la arquitectura Hexagonal. |
| `AutoTallerPro.ClientesVehiculos.Domain` | Modelo y reglas del dominio Clientes/Vehículos, independientes de frameworks. |
| `AutoTallerPro.ClientesVehiculos.Infrastructure` | Futura persistencia PostgreSQL e integraciones externas. |
| `AutoTallerPro.Servicios.Api` | Entrada HTTP y composición del microservicio Servicios. |
| `AutoTallerPro.Servicios.Application` | Casos de uso y puertos de aplicación de la arquitectura Hexagonal. |
| `AutoTallerPro.Servicios.Domain` | Modelo y reglas del dominio Servicios, independientes de frameworks. |
| `AutoTallerPro.Servicios.Infrastructure` | Futura persistencia MySQL e integraciones externas. |
| `AutoTallerPro.Mecanicos.Api` | Capa de presentación y composición de Clean Architecture. |
| `AutoTallerPro.Mecanicos.Application` | Casos de uso y abstracciones de aplicación. |
| `AutoTallerPro.Mecanicos.Domain` | Núcleo de dominio independiente. |
| `AutoTallerPro.Mecanicos.Infrastructure` | Futura persistencia MongoDB e integraciones externas. |
| `AutoTallerPro.Usuarios.Api` | Entrada HTTP y composición del microservicio Usuarios. |
| `AutoTallerPro.Usuarios.Application` | Futuros casos de uso y abstracciones de aplicación. |
| `AutoTallerPro.Usuarios.Domain` | Futuro modelo y reglas propias de Usuarios. |
| `AutoTallerPro.Usuarios.Infrastructure` | Futura persistencia MySQL e integración con Keycloak. |
| `AutoTallerPro.Gateway` | Entrada al sistema mediante YARP, con un prefijo por microservicio. |
| `AutoTallerPro.Contracts` | Futuros contratos de comunicación compartidos; inicialmente vacío. |

Estas responsabilidades son límites de organización. Solo los proyectos de
entrada contienen código ejecutable; no hay entidades, casos de uso, puertos,
implementaciones de Adapter ni repositorios todavía.

## Referencias

La misma dirección se aplica de forma independiente a cada microservicio:

```text
Api ─────────────→ Application ─────→ Domain
 └───────────────→ Infrastructure ──→ Application
```

- `Api` referencia `Application` e `Infrastructure` como raíz de composición.
- `Application` referencia únicamente `Domain`.
- `Infrastructure` referencia únicamente `Application`; recibe `Domain`
  transitivamente y podrá implementar los puertos cuando se definan.
- `Domain` no tiene referencias a otros proyectos ni paquetes externos.
- Ningún microservicio referencia proyectos de otro microservicio.
- `Gateway` no referencia proyectos de los microservicios.
- `Contracts` no tiene referencias y todavía ningún proyecto lo consume.
- La única dependencia NuGet explícita es `Yarp.ReverseProxy` 2.3.0 en Gateway.

En Clientes/Vehículos y Servicios, `Application` y `Domain` forman el núcleo
hexagonal. `Api` será la entrada HTTP e `Infrastructure` alojará las futuras
implementaciones de los puertos. En Mecánicos, los mismos nombres expresan las
capas de Clean Architecture y las dependencias apuntan al núcleo. La referencia
de `Api` a `Infrastructure` queda destinada a la composición de dependencias.

## Decisiones antes de continuar

1. Confirmar la separación interna de Usuarios. Se prepararon cuatro capas para
   mantener la organización uniforme; su arquitectura específica no estaba
   definida. Antes de implementar, acordar qué información será responsabilidad
   de Keycloak y cuál será propia del microservicio.
2. Definir puertos y casos de uso reales antes de crear interfaces o
   implementaciones. Elegir después las bibliotecas de acceso a PostgreSQL,
   MySQL y MongoDB.
3. Acordar el alcance y versionado de `Contracts` antes del primer DTO. Mantener
   las entidades y persistencia dentro de cada microservicio.
4. Acordar los endpoints de negocio dentro de cada prefijo público del Gateway.
   Los puertos y prefijos actuales están en [la guía de Docker](../infra/README.md).

Compose ejecuta las aplicaciones y las bases locales. Las operaciones de negocio,
la conexión de las API a sus bases y la integración de la autenticación quedan
para etapas posteriores.

## Fuentes de configuración de YARP

- [Paquete Yarp.ReverseProxy 2.3.0](https://www.nuget.org/packages/Yarp.ReverseProxy/2.3.0).
- [Configuración de YARP en Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/config-files?view=aspnetcore-10.0).

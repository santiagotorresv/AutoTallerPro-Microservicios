# Informe de entrega · AutoTaller Pro EC3 · Grupo 1

## 1. Resumen ejecutivo

Se implementaron los tres microservicios asignados, con cinco recursos CRUD: Clientes, Vehículos,
Categorías, Servicios y Mecánicos. Cada módulo incluye persistencia real, reglas con Result, validación JWT,
páginas Razor, pruebas y documentación. El trabajo está en `microservicios-grupo-1`.
El producto todavía requiere la sesión real y configuración de integración que corresponden a P4/P5/P6.

## 2. Archivos creados y modificados

- P1: Domain, Application, Infrastructure y Api bajo `microservices/clientes-vehiculos/`.
- P2: mismas capas bajo `microservices/servicios/`.
- P3: mismas capas bajo `microservices/mecanicos/`.
- Frontend: `GrupoUno/`, las cinco carpetas de páginas de módulo y `wwwroot/css/grupo-uno.css`.
- Pruebas: `tests/AutoTallerPro.GrupoUno.Tests/` y `tests/AutoTallerPro.GrupoUno.GatewayVerificacion/`.
- Verificación: `scripts/grupo-1/`.
- Documentación, diagramas y capturas: `docs/grupo-1/`.

El [inventario completo](archivos.md) distingue archivos nuevos y modificaciones.
No se cambiaron archivos fuente de Usuarios, Gateway, Contracts, Compose, layout o configuración global.

## 3. Arquitectura aplicada

P1/P2 aplican arquitectura hexagonal: endpoints de entrada, casos de uso y puertos de repositorio,
entidades independientes y adaptadores de salida para PostgreSQL y MySQL.
P3 aplica Clean Architecture, con BSON y MongoDB.Driver limitados a Infrastructure.
Se mantienen `Api → Application/Infrastructure`, `Infrastructure → Application` y `Application → Domain`.
Los diagramas C4 y UML están en [arquitectura](arquitectura.md).

## 4. Bases de datos

PostgreSQL implementa `clientes` y `vehiculos`, con relación 1:N y borrado restringido.
MySQL implementa `categorias_servicio` y `servicios`, también 1:N, con dinero decimal(12,2).
MongoDB implementa `mecanicos`, ObjectId, versión de esquema e índice único de documento.
Se prepararon tres scripts idempotentes. Solo se aplicaron a recursos efímeros de pruebas.

## 5. Funcionalidades CRUD

GET de listado con búsqueda/ordenación/refresco, GET por Id, POST, PUT y DELETE para los cinco recursos.
Razor incluye listado, creación, edición y confirmación de eliminación; utiliza el Gateway y el token de la sesión del servidor.
Selección de propietario/categoría, validación inline, mensajes de éxito/error y navegación interna.
Las rutas y códigos HTTP están en [integración](integracion.md).

## 6. Patrones de diseño

Adapter: endpoints y repositorios concretos. Result: `Resultado<T>`, `Error`, `TipoError` y conversión HTTP.
Repository: puertos `I*Repositorio`. Strategy: delegados de comparación seleccionados en `ListarAsync`.
Las fábricas estáticas de entidad no se contabilizan como Factory Method GoF.

## 7. Principios SOLID

SRP separa reglas, orquestación, persistencia y HTTP. DIP mantiene las dependencias de negocio sobre puertos.
ISP usa un puerto por agregado. LSP permite sustituir adaptadores en pruebas sin cambiar casos de uso.
OCP permite agregar adaptadores de persistencia conservando el contrato del núcleo.
No se añadieron repositorios genéricos ni dependencias entre módulos de negocio.

## 8. Pruebas ejecutadas

- SDK .NET 10.0.302. Compilación de APIs, Razor, Gateway de verificación y pruebas sin errores ni advertencias.
- `dotnet test tests/AutoTallerPro.GrupoUno.Tests`: 66 aprobadas, 3 omitidas por requerir bases aisladas, 0 fallidas.
- `python3 scripts/grupo-1/verificar_persistencia.py`: 69 aprobadas, 0 fallidas y 0 omitidas.
- La ejecución aislada incluye CRUD real, constraints, rechazo sin escritura, lectura desde repositorios nuevos y recorrido Razor → Gateway → APIs → las tres bases.
- Los JWT de prueba se firman criptográficamente; se rechazan firma ajena, emisor/audiencia incorrectos, vencimiento, tipo distinto de Bearer y roles no autorizados.
- Chrome: 20 páginas verificadas, validación cliente, antiforgery y cero errores JavaScript. Vista móvil de 390 px sin desbordamiento.
- Se corrigió y probó la independencia del formato decimal en es-BO, es-ES y en-US.

La sesión y las claves JWT de los tests pertenecen al host de pruebas. No prueban un inicio de sesión real de Keycloak.
Resultados estructurados y casos individuales: [evidencia/resultados.json](evidencia/resultados.json).

## 9. Estado de Docker

Motor 24.0.6 comprobado. Se crearon únicamente contenedores propios PostgreSQL 17, MySQL 8.4 y MongoDB 8.0,
con puertos aleatorios, etiquetas de propiedad y almacenamiento tmpfs; se retiraron al finalizar cada ejecución.
Los contenedores existentes se conservaron. No se eliminaron volúmenes ni se inició/reconstruyó el Compose del equipo.
Las API de los tests se ejecutaron con SDK/Kestrel; no se atribuye esta verificación a las imágenes de despliegue del equipo.

## 10. Rúbrica

| Criterio | Máximo | Verificado | Evidencia |
| --- | ---: | --- | --- |
| P1 Clientes/Vehículos | 22 | CRUD, Result, relaciones, Razor y PostgreSQL en entorno aislado; sin nota asignada | `ClientesVehiculosTests`, `PersistenciaTests`, `GatewayRazorTests`. |
| P2 Servicios | 22 | CRUD de dos tablas, Result, Razor y MySQL en entorno aislado; sin nota asignada | `ServiciosTests`, `PersistenciaTests`, `GatewayRazorTests`. |
| P3 Mecánicos | 26 | CRUD, Result, Razor y MongoDB en entorno aislado; sin nota asignada | `MecanicosTests`, `PersistenciaTests`, `GatewayRazorTests`. |
| Total | 70 | Sin calificación propia; pendiente la demostración integrada del equipo | [Trazabilidad detallada](rubrica.md). |

## 11. Pendientes y riesgos

P4/P5: login real, sesión y renovación del access token compatibles con `GetTokenAsync("access_token")`.
P5: habilitar enlaces del menú global; las páginas se abren por sus rutas directas y tienen navegación interna.
P6: aplicar los esquemas revisados a las bases propias y agregar discovery/JWKS interno y emisor correcto a las tres APIs en Compose.
Las modificaciones exactas propuestas están en [integración](integracion.md); no se aplicaron a archivos fuera de alcance.

No quedan pruebas fallidas en la ejecución final. No se verificó login real, despliegue global con Compose ni capturas de un usuario real autenticado.
No se compilaron ni modificaron proyectos ajenos a Grupo 1, salvo que el host de pruebas enlaza la fuente existente del Gateway.
Faltan paginación y resolución de actualizaciones simultáneas para volúmenes grandes; no son funcionalidad simulada.
No se recibió el PDF `ARQSW T2-7 (1).pdf`. El PDF de la rúbrica sí se leyó.

## 12. Estado final de Git

Rama: `microservicios-grupo-1`, creada desde `main` en `2a6433d` con el árbol inicialmente limpio.
Los cambios quedan sin stage ni commit. No se ejecutaron commit, push, merge ni rebase.
El [inventario](archivos.md) refleja todos los archivos nuevos y modificados del Grupo 1.

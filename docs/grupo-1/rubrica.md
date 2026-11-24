# Trazabilidad de la rúbrica · Grupo 1

La rúbrica original suma 22 + 22 + 26 = 70 puntos. La siguiente tabla vincula cada criterio a implementación y pruebas.
**No se asigna una calificación académica.** La verificación técnica aislada incluye persistencia real y el recorrido Razor/Gateway/APIs;
la demostración del producto con login real, configuración de Compose y navegación principal continúa dependiendo del equipo.

Las rutas de código de cada módulo son relativas a `microservices/<módulo>/AutoTallerPro.<Nombre>.<Capa>/`.
Todas las pruebas pertenecen a `tests/AutoTallerPro.GrupoUno.Tests/`.

| Criterio | Máximo | Implementación | Razor y endpoint público | Evidencia y estado |
| --- | ---: | --- | --- | --- |
| P1 SELECT, refresco y orden | 2 | `Application/CasosDeUso/ClienteServicio.cs` y `VehiculoServicio.cs`; repositorios PostgreSQL | `/Clientes/Index`, `/Vehiculos/Index`; GET `/api/clientes-vehiculos/clientes` y `/vehiculos` | `ClientesVehiculosTests.ListadoFiltraYOrdena`, `PersistenciaTests.PostgreSqlCrudRestriccionesYRefrescoReales`, `GatewayRazorTests.RazorGatewayYApisCompletanCrudConSesionDePrueba`: verificado en entorno aislado. |
| P1 INSERT de dos tablas | 6 | `Cliente.Crear`, `Vehiculo.Crear`, `GuardarAsync` y adaptadores PostgreSQL | Crear de Clientes/Vehículos; POST de ambos recursos | Alta real mediante servicio, API y formulario; propietario existente y estado activo controlados. Verificado. |
| P1 UPDATE de dos tablas | 6 | `ClienteServicio.GuardarAsync`, `VehiculoServicio.GuardarAsync` | Editar de Clientes/Vehículos; PUT `/{id}` | Actualización persistida, consulta desde nueva conexión, fechas conservadas y rechazo inválido: verificado. |
| P1 DELETE | 2 | Repositorios + FK `ON DELETE RESTRICT` | Eliminar de Clientes/Vehículos; DELETE `/{id}` | Conflicto al borrar propietario con vehículos y eliminación posterior: verificado contra PostgreSQL. |
| P1 Result | 6 | `Domain/Resultados/Resultado.cs`, `Reglas`, fábricas y `Api/Adaptadores/Respuestas.cs` | Validación inline y ProblemDetails | Rechazos sin escritura, documentos/placas únicos, HTTP 400/404/409 y restricciones en carrera: verificado. |
| P2 SELECT, refresco y orden | 2 | `CategoriaServicioServicio.ListarAsync`, `ServicioServicio.ListarAsync` | `/Categorias/Index`, `/Servicios/Index`; GET `/api/servicios/categorias` y `/servicios` | `ServiciosTests.ListadoFiltraYOrdena`, `PersistenciaTests.MySqlCrudRestriccionesYRefrescoReales`, integración Razor: verificado. |
| P2 INSERT de dos tablas | 6 | `CategoriaServicio`, `Servicio`, servicios y repositorios MySQL | Crear de Categorías/Servicios; POST de ambos recursos | FK, categoría activa, precio decimal y duración: verificado con MySQL real y formularios. |
| P2 UPDATE de dos tablas | 6 | Casos de uso y `UPDATE` parametrizados | Editar de Categorías/Servicios; PUT `/{id}` | Ambas tablas, cambio de precio y descripción; rechazos sin modificación: verificado. |
| P2 DELETE | 2 | Repositorios + FK restrictiva | Eliminar de Categorías/Servicios; DELETE `/{id}` | Borrado de categoría en uso devuelve conflicto; bajas finales verificadas. |
| P2 Result | 6 | `Resultado`, `TipoError`, reglas de precio/duración/nombre | Validación y errores del backend en español | `ServiciosTests`, flujo HTTP y restricciones de unicidad de MySQL: verificado. |
| P3 SELECT, refresco y orden | 6 | `MecanicoServicio.ListarAsync`, `MecanicoRepositorio` | `/Mecanicos/Index`; GET `/api/mecanicos/mecanicos` | `MecanicosTests.ListadoOrdenaYBusca`, `PersistenciaTests.MongoCrudIndiceUnicoYRefrescoReales`, integración Razor: verificado. |
| P3 INSERT | 6 | `Mecanico.Crear`, `InsertOneAsync` | `/Mecanicos/Crear`; POST del recurso | BSON real, ObjectId e índice único de documento: verificado. |
| P3 UPDATE | 6 | `ReplaceOneAsync`, conservación de fecha original | `/Mecanicos/Editar/{id}`; PUT `/{id}` | Modificación persistida y rechazo inválido: verificado. |
| P3 DELETE | 2 | `DeleteOneAsync`, control de DeletedCount | `/Mecanicos/Eliminar/{id}`; DELETE `/{id}` | Confirmación, eliminación y 404 posterior: verificado. |
| P3 Result | 6 | `Resultado`, reglas y validación de Id de 24 caracteres | Validación y ProblemDetails | Nombres, documento, teléfono, correo, especialidad, duplicados e Id inválido: verificado. |

## Requisitos transversales

| Requisito | Evidencia | Estado |
| --- | --- | --- |
| Cuatro patrones | [Arquitectura](arquitectura.md): Adapter, Result, Repository, Strategy con delegados | Implementados y vinculados a código real. |
| SOLID y fronteras | Referencias csproj, puertos específicos, entidades independientes | Revisado estáticamente; sin dependencias entre microservicios. |
| C4 1, 2, 3 y combinado | [Diagramas](arquitectura.md) | Documentados en Mermaid. |
| Bases y relaciones | [Modelos](bases-de-datos.md), scripts SQL/JS | Cuatro tablas y una colección; esquemas idempotentes probados. |
| JWT por API | `SeguridadHttpTests`, `TokensPrueba`, código del módulo | Firma, emisor, audiencia, vigencia, Bearer y roles verificados. |
| Gateway | Proyecto de verificación enlaza fuente original y carga configuración original | Enrutamiento y token forwarding verificados en Kestrel. Despliegue general pendiente. |
| Razor real | `GatewayRazorTests`, antiforgery, `ClienteGateway` | CRUD de cinco recursos verificado mediante formularios y bases reales con sesión de prueba. |
| Interfaz | `verificar_interfaz.cjs`, seis PNG en evidencia | 20 páginas, validación cliente, cero errores JavaScript, 390 px sin desbordamiento. |
| Login real de Keycloak | Competencia P4/P5 | Integrado: sesión OIDC con renovación del token (PR #7). |
| Bitácora de usuarios | `usuarioregistro` y `usuarioactualizacion` en las cinco tablas | El usuario del token se guarda al crear y en cada edición. |
| Bases del equipo | Scripts preparados; guía de configuración para P6 | Inicialización no aplicada sobre bases existentes. |
| Menú principal | Enlaces del menú global | Integrado por P5. |
| Capturas de producto autenticado / defensa | Capturas actuales muestran formularios sin sesión | Completar con despliegue integrado del equipo. |

## Evaluación responsable

| Criterio | Máximo | Verificado | Evidencia |
| --- | ---: | --- | --- |
| P1 Clientes/Vehículos | 22 | Evidencia técnica completa en entorno aislado; sin puntuación adjudicada | Casos de uso, PostgreSQL, API, Razor y pruebas indicadas. |
| P2 Servicios | 22 | Evidencia técnica completa en entorno aislado; sin puntuación adjudicada | Categorías/Servicios, MySQL, API, Razor y pruebas indicadas. |
| P3 Mecánicos | 26 | Evidencia técnica completa en entorno aislado; sin puntuación adjudicada | MongoDB, API, Razor y pruebas indicadas. |
| Total | 70 | Sin calificación propia; producto integrado sujeto a los pendientes descritos | [Resultados](evidencia/resultados.json). |

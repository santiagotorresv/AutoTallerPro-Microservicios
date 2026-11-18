# Usuarios · Contrato HTTP para P5

P5 usa el Gateway local en `http://127.0.0.1:5100` desde Windows o
`http://gateway:8080` desde Docker. La API directa en
`http://127.0.0.1:5104` recibe las rutas de los ejemplos sin el prefijo
`/api/usuarios`. Resuelve la cabecera relativa `Location` del alta desde
la URL de la solicitud.

| Método y ruta completa de Gateway | Rol requerido | Éxito |
| --- | --- | --- |
| `GET /api/usuarios/salud` | Anónimo | 200 |
| `GET /api/usuarios/yo` | `Administrador` o `Recepcionista` | 200 |
| `POST /api/usuarios/usuarios` | `Administrador` | 201 |
| `GET /api/usuarios/usuarios` | `Administrador` | 200 |
| `GET /api/usuarios/usuarios/{id}` | `Administrador` | 200 |
| `GET /api/usuarios/usuarios/resumen` | `Administrador` | 200 |
| `PUT /api/usuarios/usuarios/{id}` | `Administrador` | 204 |
| `POST /api/usuarios/usuarios/{id}/desactivar` | `Administrador` | 204 |
| `POST /api/usuarios/usuarios/{id}/reactivar` | `Administrador` | 204 |
| `POST /api/usuarios/usuarios/{id}/reenviar-invitacion` | `Administrador` | 204 |

Usa `Authorization: Bearer <access-token>` de un usuario humano de Keycloak
con audiencia `autotaller-api`. Antes de cada escritura, Usuarios verifica
en Keycloak que el actor esté habilitado, conserve `Administrador`, tenga
el correo verificado y carezca de acciones pendientes.

`GET /salud` devuelve `{"estado":"OK"}`. `GET /yo` devuelve la identidad
del usuario autenticado, por ejemplo
`{"id":"394cbabd-d883-41fe-adb9-bbf61ae46d3d","username":"ana.perez","roles":["Recepcionista"]}`.
En `/yo`, `id` es el sujeto de Keycloak. Las rutas administrativas usan
el ID local entero que devuelve el alta o el listado.

## Alta

```http
POST /usuarios
Content-Type: application/json
Authorization: Bearer <access-token>
Idempotency-Key: 829e5ab6-14c8-4c88-9271-b76e6895d4e2

{
  "email": "ana.perez@ejemplo.invalid",
  "rol": "Recepcionista",
  "nombre": "Ana",
  "apellido": "Pérez"
}
```

`rol` admite `Administrador` y `Recepcionista` con la capitalización del
ejemplo. `nombre` y `apellido` son obligatorios y aceptan hasta 100 caracteres
cada uno. El correo es obligatorio, admite hasta 254 caracteres y se
normaliza antes de comprobar duplicados. El backend genera el username.
Envía solamente los cuatro campos del ejemplo. El destinatario abre su
invitación, verifica el correo y elige su contraseña en Keycloak. Antes
de completar esas acciones, la cuenta puede figurar habilitada aunque
todavía esté pendiente de invitación.

```http
HTTP/1.1 201 Created
Location: ./usuarios/42
Content-Type: application/json

{"id":42,"username":"ana.perez","estadoInvitacion":"correoSolicitado"}
```

Genera un UUID nuevo para cada alta y guarda el valor hasta resolver la
operación. Si se interrumpe la solicitud, repite el mismo cuerpo y la misma
clave con el mismo administrador. Un alta ya completada en MySQL conserva
su ID y username al repetirla. Usa el detalle para consultar el estado
vigente de la invitación.

| Estado de invitación | Significado |
| --- | --- |
| `pendiente` | La ficha quedó creada. Falta solicitar el correo o Keycloak rechazó la solicitud. El mismo intento puede recuperar un fallo confirmado. |
| `envioIncierto` | La ficha quedó creada. La entrega de la solicitud a Keycloak o su registro local quedó sin confirmar. Repetir el alta conserva ese estado y evita un segundo envío automático. |
| `correoSolicitado` | Keycloak aceptó la solicitud de correo. La recepción y las acciones del destinatario siguen siendo comprobaciones independientes. |
| `completada` | Keycloak muestra el correo verificado y las dos acciones requeridas ya se cumplieron. Disponible en consultas. |
| `sinRegistro` | La ficha vinculada carece de un registro de alta P4.4. Disponible en consultas. |

## Consultas

| Método y ruta | Respuesta |
| --- | --- |
| `GET /usuarios?busqueda=ana&rol=Recepcionista&activo=true&pagina=1&tamanoPagina=20` | Usuarios de la página y total filtrado. Todos los parámetros son opcionales. |
| `GET /usuarios/{id}` | Usuario por ID local entero. Devuelve 404 si la ficha o la identidad dejó de existir. |
| `GET /usuarios/resumen` | Total, activos y administradores activos. |

La búsqueda admite hasta 254 caracteres y usa coincidencia parcial sin
distinguir mayúsculas en username o correo. `rol` acepta los dos roles de
negocio y `activo` acepta `true` o `false`. Los filtros se aplican antes de
paginar. El orden es por ID local ascendente. `pagina` empieza en 1 y
`tamanoPagina` admite de 1 a 100, con 20 por defecto. MySQL aporta
las fichas del realm y Keycloak aporta la identidad y los roles actuales.
Las fichas cuya identidad devuelve 404 se omiten del listado y resumen.
`activo` indica habilitación en Keycloak. Una cuenta habilitada puede
seguir pendiente de invitación.

```json
{
  "usuarios": [
    {
      "id": 42,
      "username": "ana.perez",
      "email": "ana.perez@ejemplo.invalid",
      "nombre": "Ana",
      "apellido": "Pérez",
      "roles": ["Recepcionista"],
      "activo": true,
      "estadoInvitacion": "correoSolicitado"
    }
  ],
  "total": 1,
  "pagina": 1,
  "tamanoPagina": 20
}
```

El detalle devuelve un objeto con esa misma forma. Un resumen de ejemplo
es `{"total":3,"activos":2,"administradoresActivos":1}`.

## Edición y administración

| Método y ruta | Cuerpo | Respuesta correcta |
| --- | --- | --- |
| `PUT /usuarios/{id}` | `email`, `rol` y `activo` | `204 No Content` |
| `POST /usuarios/{id}/desactivar` | Vacío | `204 No Content` |
| `POST /usuarios/{id}/reactivar` | Vacío | `204 No Content` |
| `POST /usuarios/{id}/reenviar-invitacion` | Vacío | `204 No Content` |

Las cuatro rutas reciben un ID local y requieren `Idempotency-Key` con un
UUID nuevo por operación. Ante una respuesta interrumpida, conserva la
clave, el actor, la ruta y los datos para consultar o recuperar el resultado.
A través de Gateway se conserva el prefijo, por ejemplo
`PUT /api/usuarios/usuarios/42`.

```http
PUT /usuarios/42
Authorization: Bearer <access-token>
Idempotency-Key: 5383f59c-67e5-4d34-8fb4-88da3b089c06
Content-Type: application/json

{"email":"ana.nuevo@ejemplo.invalid","rol":"Recepcionista","activo":true}
```

Los tres campos de la edición son obligatorios. El username es inmutable.
Un correo nuevo queda sin verificar y Keycloak exige `VERIFY_EMAIL`.
El cambio de rol actúa sobre los dos grupos de negocio y conserva los
roles técnicos. Una asignación directa de negocio que impide retirar el rol
devuelve `409 rol_fuera_de_gestion` para revisar su origen en Keycloak.
Un `PUT` puede conservar `activo` mientras modifica otros datos. Las rutas
dedicadas devuelven `409 estado_sin_cambio` si ya se alcanzó ese estado.

Un administrador puede editar una cuenta con ficha de P4 que pertenezca a
`usuarios-administrados`. Su propia cuenta está protegida contra la
desactivación y el retiro de `Administrador`. La protección del último
administrador operativo también cubre el cambio de correo que deja pendiente
su verificación. Para contar como operativo, el administrador debe estar
habilitado, tener el correo verificado, una contraseña y ninguna acción
requerida pendiente. El procedimiento de instalación registra también
la ficha del primer administrador.

Al desactivar o retirar `Administrador`, el backend solicita el cierre de
sesiones en Keycloak. Los JWT ya emitidos pueden seguir vigentes hasta
expirar en los sistemas que solo validan firma y expiración. Las escrituras
de P4 verifican el estado actual del actor. [API de cierre de sesiones de Keycloak](https://www.keycloak.org/docs-api/26.8.2/rest-api/index.html)

El reenvío requiere una cuenta habilitada con verificación de correo o
configuración de contraseña pendiente. Keycloak recibe únicamente las
acciones pendientes. MySQL exige dos minutos desde la última solicitud
registrada, incluida la invitación inicial. Un `429` incluye `Retry-After`
en segundos. Un `204` confirma que Keycloak aceptó la solicitud. El detalle
muestra después `correoSolicitado` o `completada` según el estado de Keycloak.

## Errores y recuperación

| HTTP | Uso |
| --- | --- |
| 400 | JSON, clave, datos de perfil o filtros inválidos. |
| 401 | Token ausente o inválido. |
| 403 | Rol insuficiente o administrador desactivado o degradado desde la emisión del token. |
| 404 | Usuario no encontrado por ID local. |
| 409 | Correo duplicado, clave reutilizada, operación en curso, protección de cuenta propia o del último administrador, estado repetido, rol fuera de gestión o invitación no elegible. |
| 415 | Se esperaba `Content-Type: application/json` para el cuerpo. |
| 429 | Reenvío dentro del intervalo mínimo. Respeta el valor de `Retry-After`. |
| 503 | Dependencia no disponible, cambio pendiente de reconciliación o fallo de una solicitud de correo. Conserva la misma clave hasta comprobar el estado. |

Ejemplo de un problema de aplicación en formato `application/problem+json`.

```json
{"title":"El correo ya está asociado a un usuario o a un alta pendiente.","status":409,"code":"correo_duplicado"}
```

Los códigos de aplicación identifican motivos que la pantalla puede
tratar. El middleware de autenticación o Gateway puede devolver un
`401` o `403` sin campo `code`. Atiende el estado HTTP incluso si el
cuerpo está vacío o contiene solo un problema genérico.

| Código de aplicación | Comportamiento de P5 |
| --- | --- |
| `clave_invalida`, `datos_invalidos`, `solicitud_invalida`, `perfil_rechazado` | Corrige los campos e inicia un intento distinto si cambia la solicitud. |
| `administrador_no_vigente` | Informa de que el perfil o el rol ya no permiten escribir y solicita una nueva autenticación si corresponde. |
| `usuario_no_encontrado` | Actualiza el listado y cierra el formulario de la ficha que ya no está disponible. |
| `correo_duplicado`, `username_no_disponible`, `clave_reutilizada` | Muestra el conflicto. Si cambia el cuerpo, usa otra clave. |
| `alta_en_curso`, `administracion_en_curso` | Conserva el intento original y espera antes de repetir la misma clave. |
| `cuenta_propia`, `ultimo_administrador`, `estado_sin_cambio`, `rol_fuera_de_gestion`, `invitacion_no_elegible` | Explica la regla y actualiza el detalle de la cuenta. |
| `intervalo_invitacion` | Usa `Retry-After` en segundos antes de habilitar el reenvío. |
| `alta_pendiente`, `administracion_pendiente`, `dependencia_no_disponible` | Mantén el estado pendiente, conserva actor, ruta, cuerpo y clave, y consulta el detalle cuando haya un ID. Reintenta solo la misma solicitud. Si persiste, deriva la revisión al responsable de P4. |
| `envio_incierto` | Informa de que el correo pudo haberse solicitado. Conserva la clave y evita otro envío automático. |
| `envio_fallido` | Informa del rechazo confirmado. Tras el intervalo, un nuevo envío debe ser una decisión explícita. |

MySQL reserva el correo durante el alta y fija el candidato de username
antes de llamar a Keycloak. El atributo administrativo `p4AltaId` permite
recuperar una identidad con el marcador del intento tras un timeout o un
fallo local. Si otra cuenta utiliza el correo, el alta devuelve conflicto.
Si la procedencia no puede confirmarse, el alta queda pendiente para
reconciliación. La ficha usa el par `(iss, sub)`. La ficha, su auditoría y
el avance del alta se confirman en la misma transacción MySQL. El envío
se solicita después de esa transacción.

P4.5 toma un bloqueo de MySQL por realm y verifica de nuevo al actor y a
los administradores antes de los cambios de riesgo. Todas las instancias
deben usar la misma base de Usuarios. La coordinación cubre las solicitudes
de P4. Los cambios directos en la consola de Keycloak requieren coordinación
operativa aparte.

La operación y la auditoría pendiente quedan guardadas antes de escribir
en Keycloak. Se guarda también el paso que se está ejecutando. Al retomar
con la misma clave, el backend comprueba el estado real y continúa desde el
último paso confirmado. Si el efecto de una escritura incierta aún no se
puede comprobar, devuelve `503 administracion_pendiente` y bloquea otros
cambios administrativos del realm. Revisa el detalle de la cuenta,
`OperacionesUsuarios` y la auditoría antes de decidir cómo resolver el
intento. El registro guarda los tipos de cambio, el actor, el ID local y
los resultados. El correo nuevo interviene en el cálculo de la huella.
La auditoría guarda el tipo de cambio y omite la dirección.

Un `503 envio_incierto` indica que el correo pudo haberse solicitado.
Repetir la misma clave conserva ese resultado y evita otro correo. Una
nueva solicitud requiere una decisión explícita y respeta el intervalo.
Un `503 envio_fallido` indica rechazo confirmado de Keycloak.
Una desconexión o un `5xx` sin código de aplicación deja incierto el
resultado de una escritura. Conserva la clave y el cuerpo original.

## Result y Adapter en Usuarios

- **Domain.** [Validacion.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Domain/Validaciones/Validacion.cs)
  está en `Domain/Validaciones/`. Su método `Requerir` produce un
  `Resultado` o `Resultado<T>`. Ambos tipos y `ErrorResultado` están en
  [Resultado.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Domain/Resultados/Resultado.cs),
  en `Domain/Resultados/`, junto a
  [ErrorUsuarios.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Domain/Resultados/ErrorUsuarios.cs).
  `ErrorResultado` identifica la regla y puede indicar segundos de espera.
  Los códigos HTTP pertenecen a la API.
- **Application.** `AltaUsuariosServicio` y `AdministracionUsuariosFacade`
  reutilizan `ReglasCuenta` y `Roles` para validar los datos. El registro
  de idempotencia, la auditoría y las comprobaciones en Keycloak se ejecutan
  en el mismo orden al devolver un rechazo o un cambio pendiente.
- **Adapter de entrada.** En `Api/Adaptadores/Http/`,
  [EndpointsUsuarios.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Api/Adaptadores/Http/EndpointsUsuarios.cs)
  convierte JSON, rutas, `Idempotency-Key` y la identidad JWT en llamadas
  a Application. [ProblemasUsuarios.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Api/Adaptadores/Http/ProblemasUsuarios.cs)
  traduce el resultado fallido a HTTP mediante `DesdeError`.
  Las respuestas exitosas conservan su JSON y sus cabeceras. Los fallos
  técnicos conservan el tratamiento de excepciones de la API.
- **Adaptadores de salida.** En `Infrastructure/Keycloak/`,
  `KeycloakIdentidadesCliente` implementa `IIdentidadesKeycloak`.
  En `Infrastructure/Persistencia/MySql/`, `FichasAdministrativasMySql` implementa
  `IFichasAdministrativas` e `IConsultaFichasUsuarios`.
  `AuditoriaAdministrativaMySql`, `AltasUsuariosMySql` y
  `AdministracionUsuariosMySql` implementan `IAuditoriaAdministrativa`,
  `IAltasUsuarios` e `IAdministracionUsuarios`, respectivamente.

Para la defensa, un `PUT /api/usuarios/usuarios/42` con un correo inválido
se convierte en `EdicionUsuario`. Application aplica la regla existente
de correo, registra el rechazo y devuelve `DatosInvalidos`. La API
responde `400` con `code: "datos_invalidos"`. Un reenvío dentro del
intervalo devuelve `IntervaloInvitacion` y los segundos restantes. El
adaptador los convierte en `429` y `Retry-After`.

## Facade y Factory Method en Usuarios

- **Facade.** [AdministracionUsuariosFacade.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Application/Fachadas/AdministracionUsuariosFacade.cs)
  expone `EditarAsync`, `DesactivarAsync`, `ReactivarAsync` y
  `ReenviarInvitacionAsync` a `EndpointsUsuarios`. Abre la sesión de
  administración, comprueba al actor, normaliza la solicitud, compara
  la clave y su huella, comprueba la cuenta y coordina el registro de
  intentos y rechazos. Conserva el bloqueo hasta que termina la ejecución.
- **Factory Method.** En `Application/Fabricas/`,
  [CreadorOperacionUsuario.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Application/Fabricas/CreadorOperacionUsuario.cs)
  define `CrearOperacion`. [CreadorCambiosCuenta.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Application/Fabricas/CreadorCambiosCuenta.cs)
  construye un [AdministracionUsuariosServicio](../microservices/usuarios/AutoTallerPro.Usuarios.Application/Servicios/AdministracionUsuariosServicio.cs)
  para planificar y aplicar cambios de correo, rol, habilitación y sesiones.
  [CreadorReenvioInvitacion.cs](../microservices/usuarios/AutoTallerPro.Usuarios.Application/Fabricas/CreadorReenvioInvitacion.cs)
  crea un `ReenvioInvitacion`, que calcula las acciones pendientes,
  comprueba el intervalo, reserva el envío y registra su resultado.
  Ambos son productos de `EjecucionOperacionUsuario`.

Al recibir `POST /api/usuarios/usuarios/42/reenviar-invitacion`, el Adapter
HTTP convierte el JWT, el ID y la clave en una llamada a la fachada.
La fachada comprueba el intento y usa el creador de reenvío. El producto
aplica su flujo y devuelve un Result. Si se cumplió el intervalo y
Keycloak acepta el envío, el Adapter responde `204`. Si el producto
devuelve `IntervaloInvitacion`, responde `429` con `Retry-After`.
Una repetición de un intento cerrado devuelve su resultado registrado.

En el repositorio anterior, `RegistroFacade` coordinaba
`RegistroUsuarioService` e `IEmailService`. `OrdenServicioFacade`
agrupaba la creación y anulación de órdenes. `ConsultaOrdenesFacade`
agrupaba consultas y generación de comprobantes.
La fachada actual conserva la idea de coordinar un flujo desde una
entrada breve. Utiliza la autenticación, las invitaciones y los puertos
vigentes de P4. El autorregistro, el hash de contraseñas, los tokens
propios y el envío de correo del sistema anterior quedaron fuera.

La forma del Factory Method sigue `CreadorRepositorio<T>` y
`CreadorPuerto<TPuerto>` de Órdenes. Allí el método abstracto
`CrearRepositorio` permite que cada creador elija su repositorio.
Usuarios tenía `DatabaseConnectionFactory` y su implementación
`MySqlConnectionFactory`, que creaba conexiones para sus repositorios.
En P4, los creadores eligen entre dos ejecuciones con reglas y pasos
distintos. Los repositorios actuales siguen recibiendo sus dependencias
desde la composición existente, incluida la conexión gestionada por
`MySqlDataSource`.

## Primer acceso y aprovisionamiento

El responsable de despliegue crea al primer administrador con el
[procedimiento de instalación de P4](../infra/keycloak/usuarios-admin.md#primer-administrador-de-negocio).
El comando registra la identidad y su ficha MySQL, y solicita la invitación.
El primer administrador completa las mismas acciones de correo y
contraseña que las altas normales. P5 consume exclusivamente las rutas
HTTP de esta guía con el token del usuario.

# Usuarios · Contrato HTTP para P5

La API local recibe las rutas de esta guía en `http://127.0.0.1:5104`.
Para pasar por Gateway añade el prefijo `http://127.0.0.1:5100/api/usuarios`
a las mismas rutas. Por ejemplo, el listado se solicita con
`GET /api/usuarios/usuarios`. Conserva el prefijo también para el detalle.
La cabecera `Location` del alta es relativa y se resuelve desde la URL de la
solicitud.

Usa `Authorization: Bearer <access-token>` de un usuario humano de
Keycloak con rol `Administrador`. El backend comprueba el rol en todos los
endpoints administrativos. Antes del alta consulta además la habilitación
y el rol vigentes del actor en Keycloak. `GET /salud` admite solicitudes
anónimas y `GET /yo` admite los dos roles de negocio.

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

`rol` admite `Administrador` y `Recepcionista`. `nombre` y `apellido` son
obligatorios y aceptan hasta 100 caracteres cada uno. El backend normaliza
el correo y genera el username. Envía solamente los cuatro campos
del ejemplo. Keycloak permite al destinatario elegir su contraseña al
completar la invitación.

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

La búsqueda usa coincidencia parcial sin distinguir mayúsculas en username
o correo. Los filtros se aplican antes de paginar. El orden es por ID local
ascendente. El tamaño por defecto es 20 y el máximo es 100. MySQL aporta
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

## Errores y recuperación

| HTTP | Uso |
| --- | --- |
| 400 | JSON, clave, datos de perfil o filtros inválidos. |
| 401 | Token ausente o inválido. |
| 403 | Rol insuficiente o administrador desactivado o degradado desde la emisión del token. |
| 404 | Usuario no encontrado por ID local. |
| 409 | Correo duplicado, username agotado, clave reutilizada con otros datos o ejecución activa con la misma clave. |
| 503 | Fallo de Keycloak o MySQL, o alta pendiente de reconciliación. Conserva la misma clave para repetir el intento. |

Ejemplo de un problema de aplicación en formato `application/problem+json`.

```json
{"title":"El correo ya está asociado a un usuario o a un alta pendiente.","status":409,"code":"correo_duplicado"}
```

MySQL reserva el correo durante el alta y fija el candidato de username
antes de llamar a Keycloak. El atributo administrativo `p4AltaId` permite
recuperar una identidad con el marcador del intento tras un timeout o un
fallo local. Si otra cuenta utiliza el correo, el alta devuelve conflicto.
Si la procedencia no puede confirmarse, el alta queda pendiente para
reconciliación. La ficha usa el par `(iss, sub)`. La ficha, su auditoría y
el avance del alta se confirman en la misma transacción MySQL. El envío
se solicita después de esa transacción.

## Aprovisionamiento inicial

1. Ejecuta el [configurador de Keycloak](../infra/keycloak/usuarios-admin.md)
   para instalar el atributo de correlación y verificar el cliente técnico.
2. Instala el [esquema MySQL](../infra/mysql/usuarios/README.md) en orden
   antes de desplegar la API.
3. Si el realm aún carece de administrador de negocio, usa la consola
   administrativa de Keycloak para crear una cuenta habilitada sin
   contraseña. Agrégala a `/usuarios-administrados` y
   `/usuarios-rol-administrador`, y envía las acciones `VERIFY_EMAIL` y
   `UPDATE_PASSWORD` siguiendo la [guía de Keycloak](../infra/keycloak/README.md).
   El destinatario completa la invitación y ya puede autenticar las
   altas. Así se aprovisiona el primer administrador con el autorregistro
   del realm deshabilitado. Como el aprovisionamiento se hace en Keycloak,
   esa cuenta carece de ficha P4.4 y queda fuera de las consultas hasta que
   el equipo defina su incorporación administrativa.

Edición, desactivación, reactivación y reenvío administrativo quedan para
P4.5.


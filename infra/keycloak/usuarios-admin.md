# Cuenta técnica de Usuarios

`autotaller-usuarios-api` es un cliente confidencial de `autotaller` con
`client_credentials`. El backend lo usa para llamar a Admin REST. Carece de
flujo de navegador, redirecciones y roles de negocio. El scope nativo
`kc-admin-api` agrega la audiencia administrativa. En el entorno local
es `http://127.0.0.1:8180/admin/realms/autotaller`. El actor humano
se obtiene del access token que valida Usuarios con audiencia
`autotaller-api`. Mantén el cliente técnico y su secreto fuera del navegador.
[Audiencia administrativa de Keycloak 26.8.2](https://www.keycloak.org/docs/26.8.2/upgrading/#admin-rest-api-now-enforces-an-audience-check)

## Configurar una instalación

1. Arranca Keycloak. Si el realm aún no existe, crea el realm con
   [autotaller-realm.json](autotaller-realm.json). El JSON contiene el cliente
   y los grupos. Keycloak crea la cuenta de servicio y genera su secreto.
2. Desde la raíz ejecuta `node infra/keycloak/configurar-usuarios-admin.cjs`
   con Node.js 22 o posterior. El configurador usa una sesión administrativa
   local para configurar FGAP v2, añadir `realm-management:query-users`
   a la cuenta y al alcance del cliente y vincular las políticas al ID real
   de la cuenta de servicio. Lee las credenciales de arranque del entorno o
   de `.env` para este paso. Aplica cambios puntuales en un realm existente
   y se detiene ante definiciones incompatibles. Ajusta los scopes del
   cliente técnico a la lista del JSON, incluidos los heredados al importar.
   Incorpora `p4AltaId` como atributo opcional de perfil, visible y editable
   solo en administración. Conserva las reglas de perfil y los permisos del
   cliente técnico. En ejecución normal, Usuarios usa únicamente las
   credenciales del cliente técnico.
3. En **Clients → autotaller-usuarios-api → Credentials** obtén el secreto y
   guárdalo en `KEYCLOAK_USUARIOS_CLIENT_SECRET` del `.env` local ignorado
   por Git. Mantén el valor vacío en `.env.example`. Ejecuta
   `docker compose up -d --no-deps --build ms-usuarios`.
4. Comprueba el grant y el permiso de búsqueda desde Docker con
   `docker compose exec -T ms-usuarios dotnet AutoTallerPro.Usuarios.Api.dll --verificar-keycloak-admin`.
   La comprobación es de solo lectura.

Compose traduce las variables `KEYCLOAK_USUARIOS_*` a
`KeycloakAdmin__BaseUrl`, `KeycloakAdmin__Realm`, `KeycloakAdmin__ClientId`
y `KeycloakAdmin__ClientSecret`. Docker usa `http://keycloak:8080`. Para
Windows, la API usa `http://127.0.0.1:8180` y el secreto se configura por
separado en el entorno local. HTTP en loopback o en el DNS `keycloak` está
limitado a `Development`. La ausencia de credenciales impide las llamadas
administrativas y permite que la API atienda salud e identidad propia con
su configuración JWT.

## Alcance de permisos

| Permiso | Alcance |
| --- | --- |
| `realm-management:query-users` | Habilita la búsqueda. FGAP filtra las identidades visibles. Un username que parece libre todavía puede colisionar fuera del grupo. Keycloak devuelve HTTP 409 al crear. |
| `p43-gestion` | Los scopes de vista, gestión de miembros y pertenencia se aplican a `usuarios-administrados`. El backend crea en ese grupo y omite credenciales. |
| `p43-grupos-roles` y `p43-roles` | Permiten pertenecer a los dos grupos de rol y usar sus roles de realm directos. El backend comprueba nombre e ID real del rol y el contenido del grupo antes de modificar la pertenencia. |

La prueba aislada confirmó que `manage-members` también autoriza la
eliminación de miembros y que el endpoint de reset pasa la comprobación de
permisos. Keycloak 26.8.2 requiere ese scope para editar, habilitar, cerrar
sesiones y solicitar invitaciones. Una regla global explícita de reset
impidió consultar por ID a los usuarios del grupo durante la prueba. La
configuración conserva la delegación por grupo, sin `manage-users` global
ni `realm-admin`. Borrado y contraseña quedan fuera del puerto del backend.
[Scopes de administración granular](https://www.keycloak.org/docs/26.8.2/server_admin/#understanding-the-scopes-of-access)

Revisa los tres permisos `p43-*` bajo **Permissions**. Para probar escrituras,
usa identidades nuevas y temporales. Comprueba que la consulta funcione y
que Keycloak deniegue editar fuera del grupo, asignar roles directamente y
consultar clientes administrativos.

## Coordinación con MySQL

P4.4 crea la identidad, asigna el rol, guarda la ficha y el evento
administrativo en una sola transacción MySQL y solicita el correo al final.
Las escrituras de Keycloak y MySQL son independientes. El registro del
intento permite reanudar con la misma clave de idempotencia. Consulta
el [contrato de alta](../../docs/USUARIOS_API.md).

| Punto de fallo | Recuperación prevista |
| --- | --- |
| Alta en Keycloak | Si hubo timeout, 5xx o respuesta de alta sin ID válido, consulta username y correo exactos. Confirma la correlación con el intento antes de adoptar una identidad existente. Las coincidencias de datos por sí solas son insuficientes. |
| Asignación del rol | Conserva el ID y consulta el rol efectivo antes de repetir el cambio. Mantén pendiente el envío de la invitación. |
| Ficha o auditoría | Busca la ficha por emisor y sujeto. Usa su restricción única al reanudar y confirma si el evento ya quedó registrado antes de repetirlo. |
| Invitación | Usa la misma identidad para un reenvío controlado. Un envío con resultado incierto requiere revisión antes de reenviar. |

El cliente marca `ResultadoIncierto` si la respuesta de una escritura
falla y la operación podría haberse aplicado. HTTP 204 confirma que
Keycloak aceptó la solicitud del correo. Comprueba por separado la
recepción y la finalización de las acciones requeridas.

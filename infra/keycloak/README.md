# Keycloak en el entorno local

La definición [autotaller-realm.json](autotaller-realm.json) corresponde a
Keycloak 26.8.2. Usa la instancia y el PostgreSQL de Keycloak que ya levanta
[Compose](../../docker-compose.yml). El realm se llama `autotaller` y tiene
dos roles de negocio, `Administrador` y `Recepcionista`, además de los roles
técnicos que crea Keycloak. La cuenta local de administración de Keycloak
se gestiona aparte de los usuarios del taller.

El login usa nombre de usuario. Se requiere verificar el correo y el
autorregistro está deshabilitado. Las acciones nativas `VERIFY_EMAIL` y
`UPDATE_PASSWORD` están habilitadas para el alta por invitación. La política
usa las reglas nativas de longitud (8 a 128), mayúscula, minúscula, dígito y
carácter especial, más una expresión regular para rechazar espacios en los
extremos.

## Reproducir en otra computadora

1. Arranca Keycloak, su PostgreSQL y Mailpit con la
   [guía de Docker](../README.md). Desde Windows abre
   `http://127.0.0.1:8180/admin/` e inicia sesión con la cuenta administrativa
   que preparaste en tu archivo local.
2. Revisa el selector de realms. Si `autotaller` ya existe, verifica su
   configuración en la consola y coordina ajustes puntuales. Conserva el
   realm y el volumen existentes.
3. Si aún no existe, selecciona **Create realm**, carga
   `autotaller-realm.json` como recurso y confirma **Create**. Para
   automatizar la misma creación, comprueba primero que
   `GET /admin/realms/autotaller` devuelve 404 y usa
   `POST /admin/realms` con la representación JSON y un token
   administrativo local. Una colisión debe detener la operación.
4. Comprueba los roles, las acciones requeridas, la política y el cliente.
   Keycloak genera el secreto del cliente en la instancia. En la futura
   integración se tomará de **Clients → autotaller-frontend → Credentials**
   y se guardará en la configuración local de Razor, fuera del repositorio.

En una instancia nueva también se puede montar el archivo de solo lectura
en `/opt/keycloak/data/import/autotaller-realm.json` e iniciar Keycloak con
`--import-realm`. Keycloak omite los realms que ya existen. Se comprobó
este método en una instancia aislada de la misma versión con base en
memoria y una respuesta HTTP 200 en el documento de descubrimiento OIDC.

El JSON y Compose usan `http://127.0.0.1:8180` como URL pública por defecto.
Con ese puerto el realm se importa tal como está. Si cambias `KEYCLOAK_PORT`,
Compose ajusta `KC_HOSTNAME` y el puerto publicado. Como la URL pública del
realm tiene prioridad, ajusta también **Realm settings → General → Frontend
URL** o, para un realm nuevo, modifica una copia local del JSON antes de
importarlo. Mailpit recibe SMTP en `mailpit:1025` desde Keycloak y su bandeja
web se abre desde Windows en `http://127.0.0.1:8025/`. SMTP local usa la red
de Compose sin credenciales ni TLS.

## Conectividad OIDC entre Windows y Docker

Compose configura `KC_HOSTNAME` como URL pública completa y activa
`KC_HOSTNAME_BACKCHANNEL_DYNAMIC`. Desde Windows el discovery anuncia URLs
públicas. Cuando discovery se consulta en `keycloak:8080`, Keycloak anuncia
ahí los endpoints de claves, tokens, UserInfo, introspección y PAR. En ambos
casos el issuer, la autorización, el cierre de sesión y los enlaces de
invitación conservan el host público. [Hostname v2 de Keycloak](https://www.keycloak.org/server/hostname)

| Uso | Dirección local |
| --- | --- |
| Issuer esperado en ambos entornos | `http://127.0.0.1:8180/realms/autotaller` |
| Discovery consultado desde Windows | `http://127.0.0.1:8180/realms/autotaller/.well-known/openid-configuration` |
| Discovery consultado por Razor y Gateway desde Docker | `http://keycloak:8080/realms/autotaller/.well-known/openid-configuration` |
| Autorización del navegador | `http://127.0.0.1:8180/realms/autotaller/protocol/openid-connect/auth` |
| Claves públicas para Razor y Gateway en Docker | `http://keycloak:8080/realms/autotaller/protocol/openid-connect/certs` |
| Canje de código de Razor en Docker | `http://keycloak:8080/realms/autotaller/protocol/openid-connect/token` |

Para aplicar el cambio en un entorno con Compose ya en ejecución, ejecuta
estos comandos desde la raíz. Se recrea solo Keycloak con su PostgreSQL y su
realm existentes.

```powershell
docker compose config --quiet
docker compose up -d --no-deps --no-build --wait keycloak
```

Para comprobarlo desde Windows, consulta el discovery público con
`Invoke-RestMethod` y comprueba el issuer y `jwks_uri`. Para obtener el
documento desde la red de Compose puedes usar el Bash que ya trae Frontend.

```powershell
docker compose exec -T frontend bash -ec 'exec 3<>/dev/tcp/keycloak/8080; printf "GET /realms/autotaller/.well-known/openid-configuration HTTP/1.0\r\nHost: keycloak:8080\r\n\r\n" >&3; cat <&3'
```

Comprueba que el issuer sea el de la tabla y que `jwks_uri` y `token_endpoint`
apunten al DNS interno. El loopback de un contenedor apunta a ese contenedor.

## Invitar a un usuario

1. Dentro de `autotaller`, crea un usuario habilitado con nombre de usuario y
   correo. Completa nombre y apellido si lo exige el perfil nativo. Deja
   **Email verified** apagado y la sección **Credentials** sin contraseña.
2. Asigna exactamente uno de los dos roles de negocio. En **Required user
   actions** añade **Verify Email** y **Update Password**.
3. En **Credentials → Credential Reset**, selecciona ambas acciones y envíalas
   por correo. La operación oficial equivalente es
   `PUT /admin/realms/autotaller/users/{id}/execute-actions-email` con cuerpo
   JSON `["VERIFY_EMAIL","UPDATE_PASSWORD"]`. El envío puede hacerse sin
   redirección de aplicación mientras Razor aún no participa.
4. Abre en Mailpit el mensaje del destinatario y sigue el enlace desde una
   sesión de navegador separada de la consola administrativa. Confirma la
   invitación y escribe la contraseña que elija el destinatario. Comprueba
   que el correo quede verificado, las dos acciones se completen y la cuenta
   inicie sesión al usar su nueva contraseña.

La acción entrega un enlace temporal de Keycloak. El administrador crea la
cuenta y selecciona el rol. La contraseña se establece en Keycloak por el
destinatario. La configuración versionada contiene solo datos del realm,
sin cuentas, credenciales ni enlaces.

## Cliente reservado para Razor Pages

- Cliente confidencial `autotaller-frontend`, con Authorization Code Flow y
  PKCE obligatorio `S256`.
- Retorno de login `http://127.0.0.1:5105/signin-oidc`. Retorno de logout
  `http://127.0.0.1:5105/signout-callback-oidc`. Usa el host literal
  `127.0.0.1` en Razor o acuerda actualizar las URLs exactas del cliente.
- Para la futura integración de Razor en Docker, usa `Authority` con el emisor
  público y `MetadataAddress` con el discovery interno. Usa el endpoint de
  tokens anunciado para el canje. Cuando .NET se ejecute en Windows, usa el
  discovery público. `RequireHttpsMetadata = false` queda reservado al
  entorno local con HTTP. [Opciones de OpenID Connect de ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.openidconnect.openidconnectoptions?view=aspnetcore-10.0)

## Audiencia de las API

El scope OIDC `autotaller-api` se asigna como **Default** a
`autotaller-frontend`. Su mapper nativo **Audience** añade `autotaller-api`
al claim `aud` del access token. El ID token conserva su audiencia de cliente.
El scope nativo `roles` sigue disponible y emite los roles del realm en
`realm_access.roles`. [Mappers oficiales de Keycloak](https://www.keycloak.org/admin-api/protocol-mappers)

Si el realm ya existe, aplica el cambio desde la consola de Keycloak.

1. En **Client scopes**, crea `autotaller-api` con protocolo **OpenID Connect**
   y desactiva **Include in token scope**. Si ya existe, comprueba su mapper
   antes de cambiarlo.
2. En **Mappers**, añade un mapper **Audience** con el nombre `autotaller-api`.
   En **Included Custom Audience** escribe `autotaller-api`. Activa **Add to
   access token** y deja desactivado **Add to ID token**.
3. En **Clients → autotaller-frontend → Client scopes**, añade el scope como
   **Default** del cliente y conserva los scopes existentes. Los tokens
   emitidos antes del cambio deben renovarse.

Para un realm nuevo, importa [el JSON](autotaller-realm.json) siguiendo los
pasos anteriores de reproducción. El archivo activa la creación de los
scopes nativos y asigna la audiencia exclusivamente al cliente previsto.
La [guía de Gateway](../../docs/GUIA_DESARROLLO.md#gateway-yarp) detalla las
políticas y la validación.

La cuenta técnica de P4 se configura siguiendo la
[guía de Keycloak Admin para Usuarios](usuarios-admin.md). Su audiencia
administrativa es independiente de `autotaller-api`.

Quedan pendientes el login y logout de Razor, la validación de tokens en las
API restantes y los casos de uso que coordinan Keycloak con MySQL. Las
contraseñas se mantienen en Keycloak.

[Mecanismos de importación de Keycloak](https://www.keycloak.org/server/importExport) ·
[Hostname y URLs de Keycloak](https://www.keycloak.org/server/hostname) ·
[Administración de Keycloak](https://www.keycloak.org/docs/latest/server_admin/).

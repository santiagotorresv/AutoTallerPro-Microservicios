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
   Keycloak genera el secreto del cliente en la instancia. Usa **Clients →
   autotaller-frontend → Credentials** para configurar Razor localmente,
   fuera del repositorio.

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
importarlo. Un realm nuevo usa Mailpit por defecto. Su bandeja web se abre
desde Windows en `http://127.0.0.1:8025/`.

## Tema de inicio de sesión

El tema [`autotaller`](themes/autotaller/login/) usa los formularios nativos de
`keycloak.v2`. La hoja de estilos y los mensajes en español se aplican al acceso,
los errores, la verificación de correo, la primera contraseña y la recuperación.
[Temas oficiales de Keycloak](https://www.keycloak.org/ui-customization/themes)

Compose monta el tema en modo de solo lectura. El JSON versionado ya lo
selecciona para instalaciones nuevas. Para un realm existente, ejecuta desde
la raíz con Node.js 22 o posterior.

```powershell
docker compose up -d --no-deps --no-build --wait keycloak
node infra/keycloak/configurar-usuarios-admin.cjs --tema
```

El configurador comprueba que Keycloak descubrió el tema y actualiza solo
el tema de login y el idioma del realm. Conserva los usuarios, los clientes,
los flujos existentes y la opción de recuperación que tenga el realm. Se
puede volver a ejecutar sobre el mismo realm.

Para comprobar la integración de P5, abre Inicio en
`http://127.0.0.1:5105/` sin sesión. El enlace **Iniciar sesión** apunta a
Inicio, cuya política de autenticación emite el desafío OIDC. El cliente
confidencial `autotaller-frontend` recibe el retorno en `/signin-oidc` con
Authorization Code y PKCE. Usa la imagen de Frontend de la rama actual y
configura `KEYCLOAK_FRONTEND_CLIENT_SECRET` en el `.env` local a partir de
**Clients → autotaller-frontend → Credentials**. Después de actualizar el
secreto en tu entorno local puedes reconstruir solo Frontend.

```powershell
docker compose up -d --no-deps --build frontend
```

Si el realm existente tiene desactivada la recuperación de contraseña,
Keycloak omite el enlace de recuperación. El JSON de una instalación nueva
ya conserva el ajuste de recuperación de P5. La verificación de correo y las
invitaciones de desarrollo pueden probarse con Mailpit.

## Elegir SMTP para las invitaciones

Keycloak envía los correos del realm `autotaller`. Desde la raíz del repositorio,
con Keycloak en ejecución y Node.js 22 o posterior, consulta el modo guardado
en el realm.

```powershell
node infra/keycloak/configurar-usuarios-admin.cjs --smtp estado
```

El modo `mailpit` usa `mailpit:1025` en la red de Compose sin credenciales ni
TLS. El modo `gmail` usa `smtp.gmail.com:587`, autenticación por contraseña de
aplicación y STARTTLS obligatorio con validación del certificado. El nombre
visible del remitente es **AutoTaller Pro**.
[SMTP de Gmail](https://support.google.com/mail/answer/7104828)
[STARTTLS en Keycloak 26.8.2](https://www.keycloak.org/docs/26.8.2/upgrading/#smtp-starttls-is-required-when-enabled-cve-2026-107121)

Para activar Gmail, habilita la verificación en dos pasos de la cuenta de
Google y genera una contraseña de aplicación. Completa en el `.env` local
`KEYCLOAK_SMTP_GMAIL_USER` con el correo de esa cuenta y
`KEYCLOAK_SMTP_GMAIL_APP_PASSWORD` con la contraseña de aplicación. Deja
`KEYCLOAK_SMTP_GMAIL_FROM` vacío para usar la misma cuenta, o completa ese
campo con un alias previamente verificado en Gmail.
[Contraseñas de aplicación de Google](https://support.google.com/accounts/answer/185833)

```powershell
node infra/keycloak/configurar-usuarios-admin.cjs --smtp gmail
```

Este comando usa las credenciales locales `KEYCLOAK_BOOTSTRAP_ADMIN_*` para
actualizar únicamente `smtpServer` mediante la API administrativa. No
reimporta el realm ni reinicia contenedores. El comando confirma qué modo
quedó guardado y no envía mensajes por sí solo. El cliente técnico de Usuarios
conserva sus permisos existentes.

Para volver a desarrollo, ejecuta lo siguiente. Keycloak reemplaza la
configuración SMTP del realm por la de Mailpit, que no usa credenciales.

```powershell
node infra/keycloak/configurar-usuarios-admin.cjs --smtp mailpit
node infra/keycloak/configurar-usuarios-admin.cjs --smtp estado
```

Mantén la contraseña de aplicación fuera de Git y de la salida de comandos.
Keycloak guarda el SMTP activo en su propia base. Trata cualquier exportación
del realm o copia de su base como confidencial porque puede incluir secretos;
usa el JSON versionado como plantilla compartida.

### Verificar entrega con Gmail

1. Antes de invitar a alguien, usa **Realm settings → Email → Test connection**
   con una cuenta administrativa cuyo correo sea un buzón de prueba autorizado.
   Keycloak envía esta prueba a la dirección de la cuenta administrativa
   conectada. Una respuesta exitosa confirma que el servidor SMTP aceptó el
   mensaje. Confirma por separado que llegó a Recibidos o Spam del buzón.
2. Usa una cuenta temporal del realm con un destinatario autorizado. Sigue
   [el flujo de invitación](#invitar-a-un-usuario) con `VERIFY_EMAIL` y
   `UPDATE_PASSWORD`. Abre el correo en el navegador de Windows de la
   computadora anfitriona y comprueba que ambas acciones se completan. Retira
   la cuenta temporal al finalizar.

**Los enlaces de invitación apuntan a `http://127.0.0.1:8180`. Solo funcionan
desde la computadora anfitriona.** Desde otro dispositivo, esa dirección
apunta al propio dispositivo. Para incorporar destinatarios externos habrá
que desplegar Keycloak en una URL HTTPS accesible y coordinar la migración
de hostname e issuer en otra etapa.

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
4. Abre el mensaje en Mailpit o en el buzón autorizado según el SMTP activo.
   Sigue el enlace desde una sesión de navegador en la computadora anfitriona,
   separada de la consola administrativa. Confirma la
   invitación y escribe la contraseña que elija el destinatario. Comprueba
   que el correo quede verificado, las dos acciones se completen y la cuenta
   inicie sesión al usar su nueva contraseña.

La acción entrega un enlace temporal de Keycloak. El administrador crea la
cuenta y selecciona el rol. La contraseña se establece en Keycloak por el
destinatario. La configuración versionada contiene solo datos del realm,
sin cuentas, credenciales ni enlaces.

## Cliente de Razor Pages

- Cliente confidencial `autotaller-frontend`, con Authorization Code Flow y
  PKCE obligatorio `S256`.
- Retorno de login `http://127.0.0.1:5105/signin-oidc`. Retorno de logout
  `http://127.0.0.1:5105/signout-callback-oidc`. Abre el Frontend con el host
  literal `127.0.0.1`; con `localhost` Keycloak rechaza la redirección.
- En Docker, Razor usa `Authority` con el emisor público y `MetadataAddress`
  con el discovery interno. Con el SDK en Windows usa el discovery público.
  `RequireHttpsMetadata = false` queda reservado a `Development`.
- El secreto se obtiene en **Clients → autotaller-frontend → Credentials** y
  se guarda en `KEYCLOAK_FRONTEND_CLIENT_SECRET` del `.env` local. Compose lo
  entrega como `Autenticacion__ClientSecret`. Con el SDK, define la misma
  variable de entorno antes de `dotnet run`.

### Idioma y recuperación de contraseña

El JSON activa el español como único idioma de las pantallas de Keycloak y el
enlace de recuperación de contraseña del login (`resetPasswordAllowed`). En un
realm ya importado, aplica lo mismo desde la consola:

1. **Realm settings → Localization**: activa **Internationalization**, deja
   `es` como único idioma admitido y como idioma predeterminado.
2. **Realm settings → Login**: activa **Forgot password**.

El cambio de contraseña desde Razor usa la acción nativa `UPDATE_PASSWORD`
iniciada por la aplicación (`kc_action`) con `max_age=0`, de modo que Keycloak
pide primero la contraseña actual y después la nueva dos veces, con la
política del realm. [Opciones de OpenID Connect de ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.openidconnect.openidconnectoptions?view=aspnetcore-10.0)

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

Razor inicia y cierra sesión con este cliente y envía el access token a las
API a través del Gateway. Las contraseñas se mantienen en Keycloak.

[Mecanismos de importación de Keycloak](https://www.keycloak.org/server/importExport) ·
[Hostname y URLs de Keycloak](https://www.keycloak.org/server/hostname) ·
[Administración de Keycloak](https://www.keycloak.org/docs/latest/server_admin/).

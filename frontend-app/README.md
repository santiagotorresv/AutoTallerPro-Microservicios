# Frontend — Razor Pages de AutoTaller Pro

Proyecto `AutoTallerPro.Frontend`, ASP.NET Core Razor Pages en .NET 10.

Desde la raíz del repositorio:

```powershell
dotnet build frontend-app/AutoTallerPro.Frontend.csproj
$env:Autenticacion__ClientSecret = '<secreto de autotaller-frontend>'
dotnet run --project frontend-app/AutoTallerPro.Frontend.csproj
```

El perfil HTTP usa `http://127.0.0.1:5105`. Abre siempre esa dirección: Keycloak
solo acepta el retorno `http://127.0.0.1:5105/signin-oidc`. En Docker, Compose
entrega el secreto desde `KEYCLOAK_FRONTEND_CLIENT_SECRET` del `.env` local.

## Sesión con Keycloak (P5)

[SesionKeycloak.cs](Seguridad/SesionKeycloak.cs) configura una cookie de sesión y
OpenID Connect con el cliente `autotaller-frontend` (Authorization Code + PKCE).

- **Login.** Todas las páginas exigen sesión, salvo Error, Privacy y el aviso de
  error de inicio de sesión. Sin sesión, Razor redirige al login de Keycloak.
- **JWT hacia las API.** La cookie guarda el access token emitido por Keycloak.
  `GrupoUno/ClienteGateway` y `ApiUsuarios/ClienteUsuarios` lo envían en
  `Authorization: Bearer` al Gateway, que valida firma, emisor, audiencia y rol.
- **Roles.** Al iniciar sesión se leen `Administrador` y `Recepcionista` de
  `realm_access.roles`. El menú muestra Usuarios solo a `Administrador`, y la
  carpeta `/Administracion/Usuarios` exige la política `SoloAdministrador`.
- **Renovación.** El access token dura 5 minutos. Antes de que venza, la cookie
  lo renueva con el refresh token. Si Keycloak lo rechaza (cuenta desactivada,
  sesión cerrada por un administrador), se cierra la sesión local.
- **Sesión.** La cookie expira tras 30 minutos sin actividad.
- **Cerrar sesión.** Cierra la cookie y la sesión de Keycloak.
- **Cambiar contraseña.** Inicia la acción `UPDATE_PASSWORD` de Keycloak con
  `max_age=0`: Keycloak pide la contraseña actual y la nueva dos veces, y
  aplica la política del realm. Al volver, Razor informa el resultado.
- **Primer acceso.** El usuario recibe la invitación, verifica su correo y
  elige su contraseña en Keycloak antes de su primer inicio de sesión.

## Administración de Usuarios

Las páginas de `Pages/Administracion/Usuarios/` consumen el contrato
[USUARIOS_API.md](../docs/USUARIOS_API.md) mediante el Gateway.

- **Listado:** búsqueda por usuario o correo, filtros de rol y estado,
  paginación, contadores del resumen y estado de la invitación. Acciones por
  fila para desactivar (con confirmación), reactivar y reenviar la invitación.
- **Crear:** nombre, apellido, correo y rol. El administrador no define la
  contraseña; Keycloak envía la invitación. No existe registro público.
- **Editar:** correo, rol y estado. El nombre de usuario es inmutable.

[ClienteUsuarios](ApiUsuarios/ClienteUsuarios.cs) es el Adapter del Frontend:
traduce las llamadas de las páginas a HTTP y los ProblemDetails de la API a
mensajes de la interfaz. Cada escritura envía `Idempotency-Key`. La clave viaja
en el formulario y se conserva cuando el resultado es incierto (503, falta de
respuesta, operación en curso), para que **Reintentar** repita la misma
operación sin duplicarla. Un 429 muestra los segundos de `Retry-After` y
bloquea el botón de reenvío hasta que se cumplen.

La verificación de extremo a extremo está en
[USUARIOS_FRONTEND.md](../docs/USUARIOS_FRONTEND.md).

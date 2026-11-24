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

## Interfaz del Grupo 1 · 24/11/2026

Se recuperó la presentación de [ArielFlowers/TallerMecanico](https://github.com/ArielFlowers/TallerMecanico/tree/2ac55604ad6795a1293a7d964c45443b170e8719/frontend-app),
referencia `2ac5560`: cabeceras, tablas, búsqueda, botones, tarjetas de formularios y confirmaciones.
Los CSS originales `site.css`, `vehiculos.css`, `servicios.css` y `mecanicos.css` ya estaban presentes
y se reutilizan sin cambios. `grupo-uno.css` aporta únicamente ajustes de integración y adaptación móvil.

- Clientes, Vehículos y Mecánicos abren creación, edición y eliminación en ventanas modales.
  Sus rutas Crear/Editar/Eliminar siguen disponibles para acceso directo y uso sin JavaScript.
- Servicios conserva formularios en páginas separadas. Categorías mantiene su CRUD y adopta la misma presentación.
- Búsqueda, refresco, ordenación, antiforgery y mensajes del backend conservan sus contratos.
  Los listados muestran el propietario del vehículo y la categoría del servicio mediante sus APIs actuales.
- Las vistas usan `GrupoUno/ClienteGateway` y la sesión existente. No se importaron configuraciones,
  conexiones ni lógica de persistencia del sistema anterior.

La migración conserva los campos del dominio EC3. El sistema anterior tenía nombres y apellidos del cliente
separados, kilometraje, observaciones, género y cobro por unidad,
entre otros datos que no existen en estos contratos. No se muestran campos sin persistencia ni valores
simulados para aparentar que esas funciones están implementadas. Servicios continúa expresando duración en minutos.

Verificación: 75 pruebas aprobadas, 3 de bases aisladas omitidas; Chrome completó 36 comprobaciones de
listados, formularios, modales, validaciones, relaciones y móvil de 390 px, sin errores JavaScript.
También se comprobó creación, validación de servidor y eliminación con JavaScript desactivado.
La verificación visual usa sesión y datos de prueba en memoria; no modifica Atlas ni Neon.
Las capturas y el registro están en [evidencia del diseño restaurado](../docs/grupo-1/evidencia/README.md).

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

Al integrar main se conservaron las validaciones nuevas de CI y celular y el modo de placa extranjera, adaptado también a los formularios modales.

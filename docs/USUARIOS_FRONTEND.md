# Usuarios · Verificación de extremo a extremo (P5)

Entorno: Docker Compose local desde cero, el 19/11/2026. Realm importado desde
`infra/keycloak/autotaller-realm.json`, cuenta técnica de P4 configurada, esquema
MySQL `001`–`003` aplicado y primer administrador aprovisionado. SMTP en Mailpit.
Navegador: Chrome sobre `http://127.0.0.1:5105`.

## Resultado

| Funcionalidad | Resultado | Evidencia |
| --- | --- | --- |
| Sin sesión, cualquier página redirige al login de Keycloak | Pasa | 302 a `/protocol/openid-connect/auth` (PAR) |
| Login de Keycloak en español, sin autorregistro | Pasa | Captura del login |
| Login correcto: cabecera con usuario y rol | Pasa | `adrianmercado782-admin · Administrador` |
| Logout cierra la cookie y la sesión de Keycloak | Pasa | Tras salir, Keycloak vuelve a pedir credenciales |
| JWT de Razor hacia las API por el Gateway | Pasa | Listado y resumen con datos reales |
| Gateway sin token o con token falso | Pasa | 401 en ambos casos; API directa sin token: 401 |
| Renovación del access token (dura 5 minutos) | Pasa | A los 6 minutos del login, el Gateway aceptó el token renovado y reenvió la solicitud |
| Menú por rol | Pasa | Recepcionista no ve Usuarios |
| Recepcionista abre `/Administracion/Usuarios` por URL | Pasa | Redirige a «Acceso denegado» |
| Listado: búsqueda, filtro por rol y estado, paginación y resumen | Pasa | `busqueda=recep&rol=Recepcionista&activo=true` |
| Alta sin contraseña: cuenta en Keycloak y ficha en MySQL | Pasa | Ficha 2 vinculada al `sub` de Keycloak; `CreadoPorSujeto` = admin |
| Username generado a partir del correo, sin duplicados | Pasa | `adrianmercado782-recepcion` |
| Validación del formulario (campos obligatorios) | Pasa | Mensajes en español bajo cada campo |
| `409 correo_duplicado` | Pasa | Mensaje mostrado y clave regenerada |
| Misma `Idempotency-Key` dos veces | Pasa | Dos POST: 1 ficha, 1 cuenta, 1 correo, 1 evento de auditoría |
| API caída (502 del Gateway) y reintento | Pasa | Conserva clave y datos; «Reintentar» crea el usuario una sola vez |
| Reenvío dentro del intervalo: `429` + `Retry-After` | Pasa | «Debe esperar 95 segundos»; botón con cuenta regresiva |
| Invitación: verificación de correo y primera contraseña | Pasa | Enlace de Mailpit, política aplicada, luego login correcto |
| Política de contraseña | Pasa | `corta1` rechazada por longitud, mayúscula y carácter especial |
| Edición: cambio de rol Recepcionista ↔ Administrador | Pasa | Administradores activos 1 → 2 → 1 |
| Desactivar con confirmación / reactivar | Pasa | Keycloak `enabled=false` y sin sesiones; luego reactivada |
| `cuenta_propia` | Pasa | «No puede desactivar su propia cuenta…» |
| `ultimo_administrador` | Pasa | Cambio de correo del único administrador rechazado |
| Cambio de contraseña | Pasa | Pide la contraseña actual (rechaza una incorrecta), la nueva dos veces (rechaza si no coinciden), vuelve con aviso de éxito; la anterior deja de funcionar |
| Recuperación de contraseña | Pasa con observación | Ver hallazgo 2 |
| Auditoría en MySQL | Pasa | Altas, ediciones, desactivación, reactivación y rechazos (`CUENTA_PROPIA`, `ULTIMO_ADMINISTRADOR`, `INTERVALO_INVITACION`) |
| Páginas del Grupo 1 con la sesión de P5 | Parcial | La página recibe el token; la API de Clientes no estaba levantada (ver pendientes) |

Pruebas automáticas: `AutoTallerPro.Frontend.Tests` (11 pruebas del adaptador
`ClienteUsuarios`) y `GatewayRazorTests` del Grupo 1 (9 pruebas), todas en verde.

## Hallazgos para P4

1. **La invitación no informa el username** y el realm tiene
   `loginWithEmailAllowed: false`. El correo de Keycloak no lo incluye, y al
   terminar las acciones Keycloak muestra «Cuenta actualizada» sin enlace de
   regreso. La UI muestra el username al administrador tras el alta. Propuesta:
   permitir el login con correo (los correos son únicos) o enviar la invitación
   con `client_id` y `redirect_uri` de `autotaller-frontend`.
2. **Recuperación de contraseña**: con `loginWithEmailAllowed: false`, el
   formulario «¿Ha olvidado su contraseña?» solo busca por username. Con el
   correo responde lo mismo pero no envía nada. Con el username, el correo
   llega y el enlace termina dentro de la aplicación. La propuesta del punto 1
   lo resuelve.
3. **Ortografía en textos de Keycloak** (la rúbrica descuenta un punto por error):
   «longitud minima» sin tilde, «al menos 1 letras mayúsculas», «expirará en
   12 horas minutos» en el correo y mezcla de «tú» y «usted». Se corrigen con
   *Realm settings → Localization → Realm overrides* para `es`.
4. **Auditoría de altas rechazadas**: un alta con `correo_duplicado` no deja
   registro en `AuditoriaAdministrativa`. Las demás operaciones sí registran
   sus rechazos.
5. **`/resumen`** devuelve el DTO sin pasar por `Resultado`. Funciona, pero es la
   única ruta que no aplica el patrón Result.

## Revisión del patrón Adapter

- **Adaptador de entrada** (`Api/Adaptadores/Http/EndpointsUsuarios.cs`):
  convierte JSON, ruta, `Idempotency-Key` y claims del JWT en llamadas a
  Application. `ProblemasUsuarios.DesdeError` traduce el `Resultado` a
  ProblemDetails y `Retry-After`. Respeta la dirección `Api → Application`.
- **Adapter GoF más claro para la defensa**: `KeycloakIdentidadesCliente`
  (Infrastructure) implementa `IIdentidadesKeycloak` (Application) y adapta la
  API REST de Keycloak (`UsuarioRemoto`) al modelo del dominio (`CuentaKeycloak`).
  Target: `IIdentidadesKeycloak`; Adaptee: Admin REST de Keycloak; Client:
  `AltaUsuariosServicio` y la fachada.
- **Uso desde el Frontend**: `JsonUnmappedMemberHandling.Disallow` exige enviar
  exactamente los campos documentados; `ClienteUsuarios` envía solo esos.
  `Location` es relativo, por lo que Razor usa el `id` del cuerpo 201.
- **Adapter del Frontend**: `ApiUsuarios/ClienteUsuarios.cs` traduce las
  llamadas de las páginas a HTTP y los ProblemDetails a mensajes de la interfaz.

No se modificó la arquitectura del backend.

## Pendientes

- **Gmail SMTP**: falta la contraseña de aplicación en el `.env` local para
  probar envío real, reenvío y recuperación, y volver a Mailpit.
- **Enlaces de Keycloak** (`127.0.0.1:8180`): solo funcionan en la computadora
  anfitriona. Para otros dispositivos hace falta una URL accesible. Identificado,
  no resuelto.
- **APIs del Grupo 1 en Compose**: les faltan las variables `Authentication__*`
  documentadas en `docs/grupo-1/integracion.md` (P6).
- **Bitácora del Grupo 1**: sus tablas guardan fechas pero no el usuario que
  creó o modificó cada registro. La rúbrica del Grupo 2 asigna 20 puntos a la
  bitácora de los CRUD en atributos de auditoría.
- **Data Protection del Frontend** sin persistencia: al recrear el contenedor se
  pierde la cookie y Keycloak vuelve a iniciar la sesión por SSO.

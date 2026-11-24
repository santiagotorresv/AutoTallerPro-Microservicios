# Usuarios · Verificación de extremo a extremo (P5)

Entorno: Docker Compose local desde cero, el 19/11/2026. Realm importado desde
`infra/keycloak/autotaller-realm.json`, cuenta técnica de P4 configurada, esquema
MySQL `001`–`003` aplicado y primer administrador aprovisionado. SMTP en Mailpit.
Navegador: Chrome sobre `http://127.0.0.1:5105`.

El 24/11/2026 se repitió la verificación sobre `main`, después del PR #9 de P4
(ajustes de pantallas y tema de login), con SMTP en Gmail. El resultado está en
[Repetición del 24/11 sobre `main`](#repetición-del-2411-sobre-main).

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

## Repetición del 24/11 sobre `main`

Mismo entorno local, con `main` en `2034733`, el tema `autotaller` aplicado con
`configurar-usuarios-admin.cjs --tema` y Keycloak enviando por Gmail
(`--smtp gmail`) desde la cuenta del proyecto. Las direcciones de prueba usan
alias `+pruebaN` de esa cuenta.

| Funcionalidad | Resultado | Evidencia |
| --- | --- | --- |
| Gmail: invitación de un alta nueva | Pasa | El correo llegó a Gmail; Mailpit no recibió nada |
| Gmail: reenvío de invitación | Pasa | Primero `429` («Debe esperar 83 segundos»); pasado el intervalo, el correo llegó |
| Gmail: recuperación de contraseña por username | Pasa | El correo llegó |
| Invitación completa y primer inicio de sesión | Pasa | Con Mailpit, que usa el mismo flujo de Keycloak: enlace, verificación, primera contraseña y entrada como Recepcionista; la invitación pasa a «Completada» |
| Login con el tema AutoTaller Pro | Pasa | Administrador y Recepcionista |
| Recepcionista: menú sin Usuarios y URL directa | Pasa | «Acceso denegado» |
| Búsqueda con filtro por rol y estado | Pasa | `adrian` + Recepcionista + activos: 3 resultados |
| Validación del alta y `409 correo_duplicado` | Pasa | Mensajes bajo cada campo; «El correo ya está asociado…» |
| Cambio de rol | Pasa | Administradores activos 1 → 2 → 1 |
| Desactivar con confirmación y reactivar | Pasa | Reactivar no abre el diálogo de confirmación |
| `cuenta_propia` y `ultimo_administrador` | Pasa | Mensajes de la API mostrados en la página |
| API caída y «Reintentar» con `ReintentosUsuarios` | Pasa | Con `ms-usuarios` detenido, la página ofrece reintentar; al volver la API, una sola alta |
| Auditoría en MySQL | Pasa | Altas, reenvíos, ediciones, desactivación, reactivación y rechazos del día |
| Cambio de contraseña | Pasa | Reautenticación, política aplicada y cancelación con aviso |
| Páginas del Grupo 1 con la sesión de P5 | Parcial | El Gateway y las API aceptan el token. Mecánicos carga; Clientes, Vehículos y Servicios fallan porque la base local nueva no tenía sus tablas |

Pruebas automáticas sobre `main`: `AutoTallerPro.sln` con 19 pruebas del
Frontend y 101 de Usuarios (36 omitidas), y `AutoTallerPro.GrupoUno.Tests` con
66 (3 omitidas), todas en verde.

Las capturas están en [usuarios/evidencia](usuarios/evidencia/README.md).

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
   Con el tema del 24/11, «longitud mínima 8» ya sale bien, pero siguen «al
   menos 1 letras mayúsculas», «1 caracteres numéricos» y «1 caracteres
   especiales». Propuesta para
   `infra/keycloak/themes/autotaller/login/messages/messages_es.properties`:

   ```properties
   invalidPasswordMinUpperCaseCharsMessage=Contraseña no válida: debe contener al menos {0} letra mayúscula.
   invalidPasswordMinLowerCaseCharsMessage=Contraseña no válida: debe contener al menos {0} letra minúscula.
   invalidPasswordMinDigitsMessage=Contraseña no válida: debe contener al menos {0} número.
   invalidPasswordMinSpecialCharsMessage=Contraseña no válida: debe contener al menos {0} carácter especial.
   ```

   Otros textos vistos el 24/11: la versión de texto plano del correo dice
   «expirará en 12 horas minutos» (la HTML dice «caducará en 12 horas»); el
   enlace de la invitación abre «Haz click aquí para proceder» («clic» y trato
   de tú); y el aviso «Vuelva a autenticarse para continuar» casi no se lee
   sobre el fondo oscuro del tema.
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
  Desde el PR #9, `ApiUsuarios/ReintentosUsuarios.cs` protege con Data
  Protection la clave y los datos de un reintento, y las páginas solo muestran
  el resultado.

No se modificó la arquitectura del backend.

## Pendientes

- **SMTP activo**: el entorno local quedó con Gmail. Para volver a Mailpit:
  `node infra/keycloak/configurar-usuarios-admin.cjs --smtp mailpit`.
- **Enlaces de Keycloak** (`127.0.0.1:8180`): solo funcionan en la computadora
  anfitriona. Para otros dispositivos hace falta una URL accesible. Identificado,
  no resuelto.
- **Tablas del Grupo 1 en un entorno nuevo**: Compose ya entrega
  `Authentication__*` a sus API (PR #8), pero sus tablas se crean con sus
  propios scripts.
- **Bitácora del Grupo 1**: sus tablas guardan fechas pero no el usuario que
  creó o modificó cada registro. La rúbrica del Grupo 2 asigna 20 puntos a la
  bitácora de los CRUD en atributos de auditoría.
- **Data Protection del Frontend** sin persistencia: al recrear el contenedor se
  pierde la cookie y Keycloak vuelve a iniciar la sesión por SSO. También deja
  de valer un «Reintentar» pendiente.

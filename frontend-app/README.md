# Frontend — Base visual y administración de Usuarios

Proyecto `AutoTallerPro.Frontend`, ASP.NET Core Razor Pages en .NET 10.

Desde la raíz del repositorio:

```powershell
dotnet build frontend-app/AutoTallerPro.Frontend.csproj
dotnet run --project frontend-app/AutoTallerPro.Frontend.csproj
```

El perfil HTTP utiliza `http://localhost:5105`. Incluye Inicio, Privacy, Error y
las tres páginas de administración de Usuarios. El enlace Usuarios del grupo
Personal está habilitado y resalta la sección actual. Las demás secciones
pendientes conservan sus nombres y rutas previstas en `data-pagina-pendiente`,
sin enlaces activos.

Se conservaron los nueve CSS de `wwwroot/css`, la imagen `hero-taller.jpg`, el
favicon, `site.js` y las cuatro bibliotecas locales con sus licencias del
frontend anterior. `frontend.css` agrega únicamente los estados de controles
pendientes y de indicadores sin conexión. Las bibliotecas conservan sus avisos
de licencia originales.

Inicio conserva la portada, las tres tarjetas, las tres métricas y la sección de
auditoría. Los valores de las métricas son `—`, con estado «Sin conexión». La
fecha se presenta en la zona horaria de Bolivia. El encabezado indica
«Autenticación pendiente»; no hay usuario ni estadísticas simulados.

No hay referencias a otros proyectos, paquetes NuGet explícitos, clientes HTTP,
sesiones ni autenticación. No se copiaron configuraciones de conexiones,
credenciales, certificados o Docker.

## Administración de Usuarios

Rutas originales conservadas:

- `/Administracion/Usuarios/Index` y su ruta equivalente `/Administracion/Usuarios`.
- `/Administracion/Usuarios/Crear`.
- `/Administracion/Usuarios/Editar/{id:int}`.

Editar conserva el identificador en la ruta por compatibilidad visual; no carga
un usuario ni interpreta ese identificador como un registro existente. Los campos
se muestran vacíos y el estado de la cuenta figura como no disponible.

Se recuperaron las tres vistas, `_ResultadosUsuarios.cshtml` y sus PageModels.
Los modelos `UsuarioCreateInput`, `UsuarioEditInput` y `UsuarioListViewModel`
contienen solamente datos de presentación y metadatos de etiquetas/tipos de
campo. No hay DTO, reglas de cuenta, validadores del backend ni políticas de roles.

`usuarios.css` ya estaba copiado sin cambios. `usuarios-form.css` reúne los
estilos originales que Crear y Editar repetían en sus bloques inline.
`password-toggle.js` se conserva del original y `usuarios.js` contiene únicamente
interacciones locales: limpiar los filtros, indicar que no se consulta ningún
servicio, cerrar el diálogo y representar el estado desconocido de la cuenta.

La tabla conserva sus columnas y el diseño de sus acciones, pero la colección
de usuarios está vacía y no se muestran filas ficticias. Los contadores muestran
`—`, con estado «Sin conexión»; la paginación está desactivada. Se conserva el
diálogo de confirmación con su acción de negocio desactivada.

Crear, guardar, editar y activar/desactivar permanecen inoperativos. El botón
Crear Usuario del listado también está desactivado; la página de creación puede
revisarse directamente por su URL. Los bloques de formulario conservan campos,
etiquetas y distribución con `role="form"`, sin elementos HTML que puedan enviar
formularios. Pulsar Enter no genera solicitudes, incluso sin JavaScript.
Cancelar vuelve al listado y mostrar/ocultar contraseñas funciona localmente.

No se migró `Pages/Auth`. La creación de cuentas, el correo de verificación, la
política de contraseñas y las conexiones con Usuarios/Keycloak quedan pendientes.
Cada responsable implementará el CRUD y el frontend de Clientes, Vehículos,
Servicios y Mecánicos; esos módulos no forman parte de esta migración.

## Verificación de Usuarios

Comprobada el 17 de noviembre de 2026:

- Frontend y solución de 19 proyectos: 0 errores y 0 advertencias.
- HTTP 200 en las tres páginas; Editar se comprobó con `/Editar/1`, sin cargar
  un registro, respetando su ruta original con identificador entero obligatorio.
- Navegación desde el menú, sección activa y retorno al listado correctos.
- Las tres páginas se comprobaron en escritorio (1440 px) y móvil (390 px), sin
  desbordamiento horizontal del documento; la tabla conserva su scroll local.
- Nueve recursos relacionados con Usuarios y el layout comprobados por HTTP,
  con contenido idéntico a sus archivos locales.
- Filtros, limpieza, Enter, controles desactivados y contraseñas comprobados sin
  solicitudes adicionales. Los bloques sin envío también se probaron con
  JavaScript deshabilitado.
- POST manuales rechazados; no se implementaron handlers de escritura.
- Sin solicitudes AJAX, a APIs o destinos externos, cookies ni errores de consola.
- Solo se inició el Frontend y se detuvieron el proceso y el navegador al terminar.
- Hashes del frontend anterior y de los otros 18 proyectos, la solución y la
  configuración compartida sin cambios.

## Verificación de la etapa 2B.1

Comprobada el 17 de noviembre de 2026:

- Frontend y solución completa: compilación correcta, 0 errores y 0 advertencias.
- HTTP 200 en `/`, `/Index`, `/Privacy` y `/Error`.
- 45 recursos CSS, JavaScript, imágenes y favicon comprobados por HTTP, con
  contenido idéntico al archivo local.
- Navegación probada en Chromium con anchos de 1440, 768 y 390 píxeles, sin
  desbordamiento horizontal; los cinco grupos del menú abren y cierran.
- Nueve entradas de módulos desactivadas y controles de autenticación sin acción.
- Sin solicitudes a APIs, AJAX, destinos externos, cookies ni errores de consola.
- Solo se inició el Frontend; el proceso y el navegador de comprobación se
  detuvieron al finalizar.

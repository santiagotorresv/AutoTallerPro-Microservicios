# Evidencia de ejecución

## Diseño restaurado · 24/11/2026

Referencia visual: `ArielFlowers/TallerMecanico`, commit `2ac5560`.
Estas capturas usan datos y sesión de prueba en memoria, y no representan una prueba del despliegue cloud.
No incluyen credenciales ni datos de las bases del equipo.

- [Clientes, escritorio](diseno-restaurado/clientes-escritorio.png).
- [Vehículos, escritorio](diseno-restaurado/vehiculos-escritorio.png).
- [Servicios, escritorio](diseno-restaurado/servicios-escritorio.png).
- [Mecánicos, escritorio](diseno-restaurado/mecanicos-escritorio.png).
- [Formulario de Servicios](diseno-restaurado/servicios-formulario.png).
- [Modal de Mecánicos](diseno-restaurado/mecanicos-formulario.png).
- [Mecánicos, móvil](diseno-restaurado/mecanicos-movil.png).
- [Modal de Mecánicos, móvil](diseno-restaurado/mecanicos-modal-movil.png).
- [36 verificaciones de Chrome](diseno-restaurado/verificacion.json).

Los formularios usan las rutas, validaciones y servicios actuales. Los campos que no pertenecen a los
contratos EC3 no se importaron desde el backend anterior. La [guía de Frontend](../../../frontend-app/README.md)
explica el alcance y las diferencias de datos.

## Evidencia inicial · historial

Las capturas se obtienen con Chrome real mediante `scripts/grupo-1/verificar_interfaz.cjs`.
Muestran formularios del Grupo 1, validación inline, identidad visual existente y diseño móvil.
Se capturaron sin una sesión de Usuarios conectada; no se incluyen registros simulados.

- [Clientes](clientes-crear.png).
- [Vehículos](vehiculos-crear.png).
- [Categorías](categorias-crear.png).
- [Servicios](servicios-crear.png).
- [Mecánicos](mecanicos-crear.png).
- [Vista móvil](mecanicos-movil.png).

Los resultados de ejecución resumidos están en [resultados.json](resultados.json).
Los TRX completos quedan en `tests/AutoTallerPro.GrupoUno.Tests/TestResults/`, ignorado por Git.
No contienen credenciales; los hosts de test y las claves JWT son efímeros.

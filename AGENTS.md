# Instrucciones para agentes de IA

1. Lee la [guía de desarrollo](docs/GUIA_DESARROLLO.md) antes de cambios arquitectónicos importantes. Para comandos y configuración consulta el [README](README.md) y la [guía de Docker](infra/README.md).
2. Identifica el rol de la tarea y trabaja únicamente dentro de su alcance: P1 Clientes/Vehículos, P2 Servicios, P3 Mecánicos, P4 Usuarios Backend, P5 Usuarios Frontend o P6 integración e infraestructura. Si una tarea cruza módulos, delimita el cambio solicitado antes de editar y coordina la parte compartida. No modifiques otros microservicios sin una necesidad explícita.
3. Inspecciona el código y las referencias existentes antes de crear clases, endpoints, paquetes o configuraciones. No modifiques Docker Compose, Gateway, configuración compartida ni contratos globales sin autorización. El rol P6 tampoco sustituye la autorización de una tarea concreta.
4. Conserva las referencias `Api → Application, Infrastructure`, `Infrastructure → Application` y `Application → Domain`. Mantén `Domain` independiente y evita referencias directas entre microservicios o acceso a bases de otros módulos.
5. Usa el código de este repositorio como base. No copies indiscriminadamente el backend anterior ni uses Aiven. Implementa Result, Adapter, autenticación y funciones adicionales solo si la tarea lo solicita.
6. Mantén el código sencillo y coherente con el módulo. Evita comentarios innecesarios y datos ficticios que aparenten una operación funcional.
7. Usa `.env.example` como referencia. No modifiques archivos `.env` ajenos ni leas, imprimas, copies o publiques secretos que no sean necesarios para la tarea.
8. No ejecutes migraciones, borrados u operaciones destructivas de base sin autorización. No ejecutes `docker compose down -v`, `docker system prune` ni comandos que destruyan volúmenes o afecten contenedores ajenos.
9. No hagas commit, push, merge ni cambios en `main` sin solicitud expresa.
10. Compila y verifica únicamente lo necesario. Si una tarea es documental, valida rutas, enlaces y comandos con lecturas o comprobaciones estáticas.
11. Al entregar, indica los archivos modificados, las pruebas realmente ejecutadas y los pendientes o bloqueos.

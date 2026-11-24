# Grupo 1 · AutoTaller Pro EC3

Implementación en la rama `microservicios-grupo-1`, con fecha de revisión 19/11/2026.
La rama `correcciones-grupo-1` añade el [modo Atlas/Neon](integracion.md#bases-online--atlas-y-neon--22112026)
para Clientes/Vehículos y Mecánicos. Preparado el 22/11/2026; pendiente de crear los proyectos y probar conexiones reales.
Los tres contextos incluyen persistencia real, CRUD, Result, controles JWT y páginas Razor.
El inicio de sesión con Keycloak (P4/P5) y los enlaces del menú principal ya están integrados. Cada escritura guarda en la bitácora el usuario del token que la realizó.

- [Arquitectura, C4 y patrones](arquitectura.md).
- [Modelo de datos y reglas](bases-de-datos.md).
- [Contrato HTTP, ejecución e integración pendiente](integracion.md).
- [Trazabilidad de la rúbrica](rubrica.md).
- [Informe de entrega](informe-final.md).
- [Inventario de archivos](archivos.md).
- [Resultados verificables](evidencia/resultados.json).
- [Capturas de interfaz](evidencia/README.md).

La fuente académica `ARQ EC3 - Entregable Unico.pdf` se leyó del archivo facilitado por el usuario.
Confirma cuatro tablas relacionales, una fuente documental, Adapter y Result obligatorios, C4 1–3 y defensa el 24/11/2026.
No se recibió `ARQSW T2-7 (1).pdf`; no se atribuyen requisitos adicionales a ese documento.

La preparación de Atlas/Neon conserva Usuarios, Gateway, Contracts y autenticación del Frontend.
Usa un override de Compose, sin sustituir el modo local.
El pequeño ejecutable de pruebas `tests/AutoTallerPro.GrupoUno.GatewayVerificacion` compila por enlace el `Program.cs` original del Gateway;
no contiene una implementación alternativa del proxy. Utiliza JWT Bearer 10.0.10 y YARP 2.3.0 en el entorno de pruebas.

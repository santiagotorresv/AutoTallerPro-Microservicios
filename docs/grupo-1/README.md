# Grupo 1 · AutoTaller Pro EC3

Implementación en la rama `microservicios-grupo-1`, con fecha de revisión 19/11/2026.
Los tres contextos incluyen persistencia real, CRUD, Result, controles JWT y páginas Razor.
El inicio de sesión real y la activación de enlaces del menú principal dependen de P4/P5/P6.

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

Los archivos de Usuarios, Gateway, Contracts, Compose, configuración global y autenticación del Frontend se conservan.
El pequeño ejecutable de pruebas `tests/AutoTallerPro.GrupoUno.GatewayVerificacion` compila por enlace el `Program.cs` original del Gateway;
no contiene una implementación alternativa del proxy. Utiliza JWT Bearer 10.0.10 y YARP 2.3.0 en el entorno de pruebas.

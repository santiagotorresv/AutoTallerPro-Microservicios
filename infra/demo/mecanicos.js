// Datos de ejemplo de Mecánicos para demostraciones.
// Se puede repetir: solo inserta los documentos que todavía no existen.
const ahora = new Date();
[
  { nombres: 'Juan Carlos', apellidos: 'Pérez Rojas', documento: '5123456', telefono: '76543211', especialidad: 'Mecánica general' },
  { nombres: 'Ana', apellidos: 'Torrez Vargas', documento: '6789012', telefono: '70011223', especialidad: 'Diagnóstico' },
  { nombres: 'Luis', apellidos: 'Mamani Choque', documento: '3456789-1B', telefono: '61234567', especialidad: 'Electricidad' }
].forEach(({ documento, ...datos }) => db.mecanicos.updateOne(
  { documento },
  { $setOnInsert: { ...datos, correo: null, activo: true, fechaRegistro: ahora, fechaActualizacion: ahora, versionEsquema: 1, usuarioRegistro: 'datos-demo', usuarioActualizacion: 'datos-demo' } },
  { upsert: true }
));

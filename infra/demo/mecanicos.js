// Datos de ejemplo de Mecánicos. Documentos DEMO-*.
// Se puede repetir: solo inserta los documentos que todavía no existen.
const ahora = new Date();
[
  { nombres: 'Juan Carlos', apellidos: 'Pérez Rojas', documento: 'DEMO-2001', telefono: '76543211', especialidad: 'Mecánica general' },
  { nombres: 'Ana', apellidos: 'Torrez Vargas', documento: 'DEMO-2002', telefono: '70011223', especialidad: 'Diagnóstico' },
  { nombres: 'Luis', apellidos: 'Mamani Choque', documento: 'DEMO-2003', telefono: null, especialidad: 'Electricidad' }
].forEach(({ documento, ...datos }) => db.mecanicos.updateOne(
  { documento },
  { $setOnInsert: { ...datos, correo: null, activo: true, fechaRegistro: ahora, fechaActualizacion: ahora, versionEsquema: 1 } },
  { upsert: true }
));

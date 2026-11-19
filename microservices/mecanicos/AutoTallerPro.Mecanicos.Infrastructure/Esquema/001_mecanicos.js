if (!db.getCollectionNames().includes('mecanicos')) {
  db.createCollection('mecanicos', {
    validator: { $jsonSchema: {
      bsonType: 'object',
      required: ['_id', 'nombres', 'apellidos', 'documento', 'especialidad', 'activo', 'fechaRegistro', 'fechaActualizacion', 'versionEsquema'],
      properties: {
        _id: { bsonType: 'objectId' },
        nombres: { bsonType: 'string', minLength: 1, maxLength: 80 },
        apellidos: { bsonType: 'string', minLength: 1, maxLength: 80 },
        documento: { bsonType: 'string', minLength: 4, maxLength: 20 },
        telefono: { bsonType: ['string', 'null'] }, correo: { bsonType: ['string', 'null'] },
        especialidad: { enum: ['Mecánica general', 'Electricidad', 'Diagnóstico', 'Carrocería'] },
        activo: { bsonType: 'bool' }, fechaRegistro: { bsonType: 'date' }, fechaActualizacion: { bsonType: 'date' },
        versionEsquema: { enum: [1] }
      }
    } }, validationAction: 'error'
  });
}
db.mecanicos.createIndex({ documento: 1 }, { unique: true, name: 'ux_mecanicos_documento' });
db.mecanicos.createIndex({ apellidos: 1, nombres: 1 }, { name: 'ix_mecanicos_nombre' });

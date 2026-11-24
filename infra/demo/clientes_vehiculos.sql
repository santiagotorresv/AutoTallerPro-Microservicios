-- Datos de ejemplo de Clientes/Vehículos para demostraciones.
-- Se puede repetir: los índices únicos de documento y placa evitan duplicados.
BEGIN;
INSERT INTO clientes (id, documento, nombre, telefono, correo, activo, fecharegistro, fechaactualizacion) VALUES
 (gen_random_uuid(), '4812736', 'María Quispe Mamani', '71234567', 'maria.quispe@demo.test', true, now(), now()),
 (gen_random_uuid(), '1020304025', 'Transportes Illimani S.R.L.', '62445566', 'contacto@demo.test', true, now(), now()),
 (gen_random_uuid(), '6234519-1A', 'Carlos Rojas Vargas', '76543210', NULL, true, now(), now())
ON CONFLICT DO NOTHING;

INSERT INTO vehiculos (id, clienteid, placa, esplacaextranjera, marca, modelo, anio, color, activo, fecharegistro, fechaactualizacion)
SELECT gen_random_uuid(), c.id, v.placa, v.extranjera, v.marca, v.modelo, v.anio, v.color, true, now(), now()
FROM (VALUES
 ('4812736', '2345ABC', false, 'Toyota', 'Corolla', 2018, 'Blanco'),
 ('1020304025', '1987KLM', false, 'Nissan', 'Frontier', 2021, 'Gris'),
 ('6234519-1A', '456XYZ', false, 'Suzuki', 'Swift', 2016, 'Rojo'),
 ('1020304025', 'AB123CD', true, 'Volkswagen', 'Amarok', 2020, 'Negro')
) AS v(documento, placa, extranjera, marca, modelo, anio, color)
JOIN clientes c ON upper(c.documento) = v.documento
ON CONFLICT DO NOTHING;
COMMIT;

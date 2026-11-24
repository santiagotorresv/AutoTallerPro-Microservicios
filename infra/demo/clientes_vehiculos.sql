-- Datos de ejemplo de Clientes/Vehículos. Documentos DEMO-* y placas DEMO*.
-- Se puede repetir: los índices únicos de documento y placa evitan duplicados.
BEGIN;
INSERT INTO clientes (id, documento, nombre, telefono, correo, activo, fecharegistro, fechaactualizacion) VALUES
 (gen_random_uuid(), 'DEMO-1001', 'María Quispe Mamani', '71234567', 'maria.quispe@demo.test', true, now(), now()),
 (gen_random_uuid(), 'DEMO-1002', 'Transportes Illimani S.R.L.', '22445566', 'contacto@demo.test', true, now(), now()),
 (gen_random_uuid(), 'DEMO-1003', 'Carlos Rojas Vargas', '76543210', NULL, true, now(), now())
ON CONFLICT DO NOTHING;

INSERT INTO vehiculos (id, clienteid, placa, marca, modelo, anio, color, activo, fecharegistro, fechaactualizacion)
SELECT gen_random_uuid(), c.id, v.placa, v.marca, v.modelo, v.anio, v.color, true, now(), now()
FROM (VALUES
 ('DEMO-1001', 'DEMO01', 'Toyota', 'Corolla', 2018, 'Blanco'),
 ('DEMO-1002', 'DEMO02', 'Nissan', 'Frontier', 2021, 'Gris'),
 ('DEMO-1003', 'DEMO03', 'Suzuki', 'Swift', 2016, 'Rojo')
) AS v(documento, placa, marca, modelo, anio, color)
JOIN clientes c ON upper(c.documento) = v.documento
ON CONFLICT DO NOTHING;
COMMIT;

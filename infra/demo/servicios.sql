-- Datos de ejemplo de Categorías y Servicios.
-- Se puede repetir: solo inserta los nombres que todavía no existen.
INSERT INTO categorias_servicio (id, nombre, descripcion, activo, fecharegistro, fechaactualizacion)
SELECT UUID(), d.nombre, d.descripcion, true, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM (
 SELECT 'Mantenimiento preventivo' AS nombre, 'Revisiones periódicas para evitar fallas.' AS descripcion
 UNION ALL SELECT 'Frenos y suspensión', 'Revisión y cambio de piezas de frenado y suspensión.'
) d
WHERE NOT EXISTS (SELECT 1 FROM categorias_servicio c WHERE c.nombre = d.nombre COLLATE utf8mb4_0900_as_ci);

INSERT INTO servicios (id, categoriaservicioid, nombre, descripcion, preciobase, duracionestimada, activo, fecharegistro, fechaactualizacion)
SELECT UUID(), c.id, d.nombre, d.descripcion, d.precio, d.duracion, true, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM (
 SELECT 'Mantenimiento preventivo' AS categoria, 'Cambio de aceite y filtro' AS nombre,
        'Aceite de motor y filtro nuevo.' AS descripcion, 180.00 AS precio, 45 AS duracion
 UNION ALL SELECT 'Mantenimiento preventivo', 'Alineación y balanceo', 'Alineación de dirección y balanceo de las cuatro ruedas.', 150.00, 60
 UNION ALL SELECT 'Frenos y suspensión', 'Cambio de pastillas de freno', 'Pastillas delanteras y revisión de discos.', 250.00, 90
 UNION ALL SELECT 'Frenos y suspensión', 'Revisión de amortiguadores', 'Prueba y diagnóstico de la suspensión.', 120.00, 40
) d
JOIN categorias_servicio c ON c.nombre = d.categoria COLLATE utf8mb4_0900_as_ci
WHERE NOT EXISTS (SELECT 1 FROM servicios s WHERE s.nombre = d.nombre COLLATE utf8mb4_0900_as_ci);

-- Bitácora: usuario de Keycloak que registró cada fila y el que la modificó por última vez.
-- Se puede repetir: solo agrega las columnas si todavía no existen.
SET @sql := IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'categorias_servicio' AND COLUMN_NAME = 'usuarioregistro') = 0,
    'ALTER TABLE categorias_servicio ADD COLUMN usuarioregistro varchar(255) NULL, ADD COLUMN usuarioactualizacion varchar(255) NULL',
    'SELECT 1');
PREPARE cambio FROM @sql; EXECUTE cambio; DEALLOCATE PREPARE cambio;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'servicios' AND COLUMN_NAME = 'usuarioregistro') = 0,
    'ALTER TABLE servicios ADD COLUMN usuarioregistro varchar(255) NULL, ADD COLUMN usuarioactualizacion varchar(255) NULL',
    'SELECT 1');
PREPARE cambio FROM @sql; EXECUTE cambio; DEALLOCATE PREPARE cambio;

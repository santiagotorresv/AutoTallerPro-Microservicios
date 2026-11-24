-- Bitácora: usuario de Keycloak que registró cada fila y el que la modificó por última vez.
-- Se puede repetir; las filas existentes quedan sin usuario hasta su próxima edición.
BEGIN;
ALTER TABLE clientes ADD COLUMN IF NOT EXISTS usuarioregistro varchar(255);
ALTER TABLE clientes ADD COLUMN IF NOT EXISTS usuarioactualizacion varchar(255);
ALTER TABLE vehiculos ADD COLUMN IF NOT EXISTS usuarioregistro varchar(255);
ALTER TABLE vehiculos ADD COLUMN IF NOT EXISTS usuarioactualizacion varchar(255);
COMMIT;

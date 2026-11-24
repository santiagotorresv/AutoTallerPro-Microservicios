-- Placa extranjera: amplía la placa a 15 caracteres y guarda si es extranjera.
-- Se puede repetir; los vehículos existentes quedan como placas nacionales.
BEGIN;
ALTER TABLE vehiculos ALTER COLUMN placa TYPE varchar(15);
ALTER TABLE vehiculos ADD COLUMN IF NOT EXISTS esplacaextranjera boolean NOT NULL DEFAULT false;
COMMIT;

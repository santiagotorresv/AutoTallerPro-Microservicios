BEGIN;
CREATE TABLE IF NOT EXISTS clientes (
 id uuid PRIMARY KEY, documento varchar(20) NOT NULL, nombre varchar(150) NOT NULL,
 telefono varchar(16), correo varchar(254), activo boolean NOT NULL DEFAULT true,
 fecharegistro timestamptz NOT NULL, fechaactualizacion timestamptz NOT NULL,
 CONSTRAINT ck_cliente_nombre CHECK (length(trim(nombre)) > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_clientes_documento ON clientes (upper(documento));
CREATE TABLE IF NOT EXISTS vehiculos (
 id uuid PRIMARY KEY, clienteid uuid NOT NULL REFERENCES clientes(id) ON DELETE RESTRICT,
 placa varchar(10) NOT NULL, marca varchar(60) NOT NULL, modelo varchar(60) NOT NULL,
 anio integer NOT NULL CHECK (anio >= 1900), color varchar(40), activo boolean NOT NULL DEFAULT true,
 fecharegistro timestamptz NOT NULL, fechaactualizacion timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_vehiculos_placa ON vehiculos (upper(placa));
CREATE INDEX IF NOT EXISTS ix_vehiculos_cliente ON vehiculos (clienteid);
COMMIT;

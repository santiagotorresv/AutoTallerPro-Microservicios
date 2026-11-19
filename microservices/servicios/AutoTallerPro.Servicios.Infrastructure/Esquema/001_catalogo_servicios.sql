CREATE TABLE IF NOT EXISTS categorias_servicio (
 id char(36) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 nombre varchar(100) NOT NULL, descripcion varchar(500), activo boolean NOT NULL DEFAULT true,
 fecharegistro datetime(6) NOT NULL, fechaactualizacion datetime(6) NOT NULL,
 CONSTRAINT ux_categorias_nombre UNIQUE (nombre),
 CONSTRAINT ck_categorias_nombre CHECK (char_length(trim(nombre)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_as_ci;
CREATE TABLE IF NOT EXISTS servicios (
 id char(36) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 categoriaservicioid char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 nombre varchar(120) NOT NULL, descripcion varchar(500), preciobase decimal(12,2) NOT NULL,
 duracionestimada int, activo boolean NOT NULL DEFAULT true,
 fecharegistro datetime(6) NOT NULL, fechaactualizacion datetime(6) NOT NULL,
 CONSTRAINT ux_servicios_nombre UNIQUE (nombre),
 CONSTRAINT fk_servicio_categoria FOREIGN KEY (categoriaservicioid) REFERENCES categorias_servicio(id) ON DELETE RESTRICT,
 CONSTRAINT ck_servicios_precio CHECK (preciobase >= 0),
 CONSTRAINT ck_servicios_duracion CHECK (duracionestimada IS NULL OR duracionestimada BETWEEN 1 AND 10080),
 INDEX ix_servicios_categoria (categoriaservicioid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_as_ci;

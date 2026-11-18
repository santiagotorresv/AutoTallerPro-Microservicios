CREATE TABLE IF NOT EXISTS OperacionesUsuarios (
    Id CHAR(36) NOT NULL,
    Emisor VARCHAR(255) NOT NULL,
    Clave CHAR(36) NOT NULL,
    ActorSujeto VARCHAR(255) NOT NULL,
    FichaId INT NOT NULL,
    Tipo TINYINT UNSIGNED NOT NULL,
    Huella BINARY(32) NOT NULL,
    Cambios TINYINT UNSIGNED NOT NULL,
    Restantes TINYINT UNSIGNED NOT NULL,
    Paso TINYINT UNSIGNED NOT NULL DEFAULT 0,
    Estado TINYINT UNSIGNED NOT NULL DEFAULT 0,
    Error VARCHAR(60) NULL,
    FechaUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (Id),
    CONSTRAINT UQ_OperacionesUsuarios_Clave UNIQUE (Emisor, Clave),
    CONSTRAINT FK_OperacionesUsuarios_Ficha FOREIGN KEY (FichaId) REFERENCES FichasAdministrativas (Id),
    INDEX IX_OperacionesUsuarios_Pendientes (Emisor, Estado, Tipo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_bin;

CREATE TABLE IF NOT EXISTS ControlInvitacionesUsuarios (
    FichaId INT NOT NULL,
    ReservadoPor CHAR(36) NOT NULL,
    UltimoIntentoUtc DATETIME(6) NULL,
    Estado TINYINT UNSIGNED NOT NULL,
    PRIMARY KEY (FichaId),
    CONSTRAINT FK_ControlInvitacionesUsuarios_Ficha FOREIGN KEY (FichaId) REFERENCES FichasAdministrativas (Id),
    CONSTRAINT CK_ControlInvitacionesUsuarios_Estado CHECK (Estado BETWEEN 1 AND 3)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_bin;

INSERT INTO ControlInvitacionesUsuarios (FichaId, ReservadoPor, UltimoIntentoUtc, Estado)
SELECT a.FichaId, a.Id, UTC_TIMESTAMP(6), a.Estado
FROM AltasUsuarios a LEFT JOIN ControlInvitacionesUsuarios c ON c.FichaId = a.FichaId
WHERE a.Estado IN (2, 3) AND c.FichaId IS NULL;

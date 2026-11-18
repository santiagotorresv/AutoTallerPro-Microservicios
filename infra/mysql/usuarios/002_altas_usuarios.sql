CREATE TABLE IF NOT EXISTS AltasUsuarios (
    Id CHAR(36) NOT NULL,
    Emisor VARCHAR(255) NOT NULL,
    Clave CHAR(36) NOT NULL,
    ActorSujeto VARCHAR(255) NOT NULL,
    Huella BINARY(32) NOT NULL,
    CorreoReservado VARCHAR(254) NULL,
    Username VARCHAR(50) NULL,
    Sujeto VARCHAR(255) NULL,
    FichaId INT NULL,
    Estado TINYINT NOT NULL DEFAULT 0,
    PRIMARY KEY (Id),
    CONSTRAINT UQ_AltasUsuarios_Clave UNIQUE (Emisor, Clave),
    CONSTRAINT UQ_AltasUsuarios_CorreoReservado UNIQUE (Emisor, CorreoReservado),
    CONSTRAINT UQ_AltasUsuarios_Ficha UNIQUE (FichaId),
    CONSTRAINT FK_AltasUsuarios_Ficha FOREIGN KEY (FichaId) REFERENCES FichasAdministrativas (Id),
    CONSTRAINT CK_AltasUsuarios_Estado CHECK (Estado BETWEEN 0 AND 5),
    CONSTRAINT CK_AltasUsuarios_FichaEstado CHECK ((FichaId IS NOT NULL) = (Estado BETWEEN 1 AND 3))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_bin;


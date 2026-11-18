CREATE TABLE IF NOT EXISTS FichasAdministrativas (
    Id INT NOT NULL AUTO_INCREMENT,
    KeycloakEmisor VARCHAR(255) NOT NULL,
    KeycloakSujeto VARCHAR(255) NOT NULL,
    FechaCreacionUtc DATETIME(6) NOT NULL,
    CreadoPorEmisor VARCHAR(255) NULL,
    CreadoPorSujeto VARCHAR(255) NULL,
    FechaModificacionUtc DATETIME(6) NULL,
    ModificadoPorEmisor VARCHAR(255) NULL,
    ModificadoPorSujeto VARCHAR(255) NULL,
    PRIMARY KEY (Id),
    CONSTRAINT UQ_FichasAdministrativas_Identidad UNIQUE (KeycloakEmisor, KeycloakSujeto),
    CONSTRAINT CK_FichasAdministrativas_Creador CHECK ((CreadoPorEmisor IS NULL) = (CreadoPorSujeto IS NULL)),
    CONSTRAINT CK_FichasAdministrativas_Modificador CHECK ((ModificadoPorEmisor IS NULL) = (ModificadoPorSujeto IS NULL)),
    CONSTRAINT CK_FichasAdministrativas_Fecha CHECK (FechaModificacionUtc >= FechaCreacionUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_bin;

CREATE TABLE IF NOT EXISTS AuditoriaAdministrativa (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    ActorEmisor VARCHAR(255) NOT NULL,
    ActorSujeto VARCHAR(255) NOT NULL,
    Accion VARCHAR(60) NOT NULL,
    Entidad VARCHAR(60) NOT NULL,
    ObjetivoId VARCHAR(255) NOT NULL,
    FechaUtc DATETIME(6) NOT NULL,
    Resultado VARCHAR(16) NOT NULL,
    CodigoMotivo VARCHAR(64) NULL,
    CorrelacionId VARCHAR(64) NULL,
    PRIMARY KEY (Id),
    CONSTRAINT CK_AuditoriaAdministrativa_Resultado CHECK (Resultado IN ('Exitoso', 'Fallido', 'Pendiente')),
    INDEX IX_AuditoriaAdministrativa_Fecha (FechaUtc DESC, Id DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_bin;

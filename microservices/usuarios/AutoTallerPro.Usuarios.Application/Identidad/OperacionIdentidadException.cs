namespace AutoTallerPro.Usuarios.Application.Identidad;

public enum ErrorIdentidad
{
    Configuracion,
    NoAutorizado,
    Prohibido,
    NoEncontrado,
    Conflicto,
    SolicitudRechazada,
    NoDisponible,
    TiempoAgotado,
    RespuestaInvalida
}

public sealed class OperacionIdentidadException(
    string operacion, ErrorIdentidad error, int? estadoRemoto = null, bool resultadoIncierto = false,
    CampoIdentidad? campoEnConflicto = null)
    : InvalidOperationException($"Falló la operación de identidad {operacion} ({error}).")
{
    public string Operacion { get; } = operacion;
    public ErrorIdentidad Error { get; } = error;
    public int? EstadoRemoto { get; } = estadoRemoto;
    public bool ResultadoIncierto { get; } = resultadoIncierto;
    public CampoIdentidad? CampoEnConflicto { get; } = campoEnConflicto;
}

public enum CampoIdentidad { Username, Correo }

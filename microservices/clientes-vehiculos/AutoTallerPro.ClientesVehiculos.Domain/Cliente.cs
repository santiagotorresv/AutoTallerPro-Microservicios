using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
namespace AutoTallerPro.ClientesVehiculos.Domain;
public sealed class Cliente
{
    public Guid Id { get; }
    public string Documento { get; }
    public string Nombre { get; }
    public string? Telefono { get; }
    public string? Correo { get; }
    public bool Activo { get; }
    public DateTimeOffset FechaRegistro { get; }
    public DateTimeOffset FechaActualizacion { get; }
    private Cliente(Guid id, string documento, string nombre, string? telefono, string? correo, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion) { Id = id; Documento = documento; Nombre = nombre; Telefono = telefono; Correo = correo; Activo = activo; FechaRegistro = fechaRegistro; FechaActualizacion = fechaActualizacion; }
    public static Resultado<Cliente> Crear(Guid id, string documento, string nombre, string? telefono, string? correo, bool activo, DateTimeOffset? fechaRegistro = null, DateTimeOffset? fechaActualizacion = null)
    {
        if (id == Guid.Empty) return Resultado<Cliente>.Fallo("cliente.id", "El identificador del cliente no es válido.");
        var ciNit = Reglas.SinEspacios(documento);
        if (!Reglas.CiNit(ciNit)) return Resultado<Cliente>.Fallo("cliente.documento", "El CI/NIT debe tener entre 5 y 13 dígitos; el CI puede llevar complemento, por ejemplo 1234567-1A.");
        if (!Reglas.Texto(nombre, 150)) return Resultado<Cliente>.Fallo("cliente.nombre", "El nombre o razón social es obligatorio y admite hasta 150 caracteres.");
        var celular = Reglas.SinEspacios(telefono);
        if (celular.Length == 0) return Resultado<Cliente>.Fallo("cliente.telefono", "El celular es obligatorio.");
        if (!Reglas.Celular(celular)) return Resultado<Cliente>.Fallo("cliente.telefono", "El celular debe tener 8 dígitos y comenzar en 6 o 7.");
        if (!Reglas.Correo(correo)) return Resultado<Cliente>.Fallo("cliente.correo", "El correo electrónico no es válido.");
        return Resultado<Cliente>.Exito(new(id, ciNit, nombre.Trim(), celular, string.IsNullOrWhiteSpace(correo) ? null : correo.Trim(), activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }

    // Reconstruye un registro ya guardado; las reglas se aplican al crear o editar.
    public static Cliente Restaurar(Guid id, string documento, string nombre, string? telefono, string? correo, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion)
        => new(id, documento, nombre, telefono, correo, activo, fechaRegistro, fechaActualizacion);
}

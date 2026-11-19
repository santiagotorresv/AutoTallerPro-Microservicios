namespace AutoTallerPro.Frontend.ApiUsuarios;

public sealed record UsuarioApi(
    int Id, string Username, string Email, string Nombre, string Apellido,
    string[] Roles, bool Activo, string EstadoInvitacion)
{
    public string Rol => Roles.FirstOrDefault() ?? "Sin rol";
    public bool PuedeReenviarInvitacion => Activo && EstadoInvitacion != "completada";
}

public sealed record PaginaUsuarios(UsuarioApi[] Usuarios, int Total, int Pagina, int TamanoPagina)
{
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));
}

public sealed record ResumenUsuarios(int Total, int Activos, int AdministradoresActivos);

public sealed record UsuarioCreado(int Id, string Username, string EstadoInvitacion);

public sealed record RespuestaUsuarios<T>(T? Valor, int Estado, string? Codigo, string? Mensaje, int? EsperaSegundos = null)
{
    public bool Exito => Mensaje is null;

    // Ante un resultado incierto se repite la misma solicitud con la misma Idempotency-Key.
    public bool ConservarClave => !Exito && Codigo != "envio_fallido" &&
        (Estado == 0 || Estado >= 500 || Codigo is "alta_en_curso" or "administracion_en_curso");
}

public static class EstadosInvitacion
{
    public static string Texto(string estado) => estado switch
    {
        "pendiente" => "Pendiente",
        "envioIncierto" => "Envío sin confirmar",
        "correoSolicitado" => "Invitación enviada",
        "completada" => "Completada",
        "sinRegistro" => "Sin registro",
        _ => estado
    };

    public static string Clase(string estado) => estado switch
    {
        "completada" => "bg-success",
        "correoSolicitado" => "bg-info text-dark",
        "pendiente" or "envioIncierto" => "bg-warning text-dark",
        _ => "bg-secondary"
    };
}

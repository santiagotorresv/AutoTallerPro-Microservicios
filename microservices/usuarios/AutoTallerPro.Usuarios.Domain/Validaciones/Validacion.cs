using AutoTallerPro.Usuarios.Domain.Resultados;

namespace AutoTallerPro.Usuarios.Domain.Validaciones;

public static class Validacion
{
    public static Resultado Requerir(bool condicion, ErrorUsuarios error) =>
        condicion ? Resultado.Exitoso() : Resultado.Fallido(error);

    public static Resultado<T> Requerir<T>(bool condicion, ErrorUsuarios error, Func<T> crearValor) =>
        condicion ? Resultado<T>.Exitoso(crearValor()) : Resultado<T>.Fallido(error);
}

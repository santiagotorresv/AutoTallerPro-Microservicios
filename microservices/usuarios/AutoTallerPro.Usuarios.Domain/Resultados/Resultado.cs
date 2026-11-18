using System.Diagnostics.CodeAnalysis;

namespace AutoTallerPro.Usuarios.Domain.Resultados;

public sealed record ErrorResultado(ErrorUsuarios Codigo, int? SegundosEspera = null);

public sealed class Resultado
{
    private Resultado(ErrorResultado? error) => Error = error;

    [MemberNotNullWhen(false, nameof(Error))]
    public bool EsExitoso => Error is null;
    public ErrorResultado? Error { get; }

    public static Resultado Exitoso() => new(null);
    public static Resultado Fallido(ErrorUsuarios codigo) => Fallido(new ErrorResultado(codigo));

    public static Resultado Fallido(ErrorResultado error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
}

public sealed class Resultado<T>
{
    private readonly T? _valor;

    private Resultado(T? valor, ErrorResultado? error)
    {
        _valor = valor;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool EsExitoso => Error is null;
    public ErrorResultado? Error { get; }

    public T Valor => EsExitoso ? _valor!
        : throw new InvalidOperationException("Un resultado fallido carece de valor.");

    public static Resultado<T> Exitoso(T valor) => new(valor, null);
    public static Resultado<T> Fallido(ErrorUsuarios codigo) => Fallido(new ErrorResultado(codigo));

    public static Resultado<T> Fallido(ErrorResultado error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default, error);
    }
}

namespace AutoTallerPro.Servicios.Domain.Resultados;

public enum TipoError { Validacion, NoEncontrado, Conflicto }
public sealed record Error(string Codigo, string Mensaje, TipoError Tipo);
public sealed class Resultado<T>
{
    private readonly T? valor;
    private Resultado(T? valor, Error? error) { this.valor = valor; Error = error; }
    public bool EsExitoso => Error is null;
    public T Valor => EsExitoso ? valor! : throw new InvalidOperationException("El resultado no contiene un valor.");
    public Error? Error { get; }
    public static Resultado<T> Exito(T valor) => new(valor, null);
    public static Resultado<T> Fallo(string codigo, string mensaje, TipoError tipo = TipoError.Validacion)
        => new(default, new(codigo, mensaje, tipo));
    public static Resultado<T> Fallo(Error error) => new(default, error);
}

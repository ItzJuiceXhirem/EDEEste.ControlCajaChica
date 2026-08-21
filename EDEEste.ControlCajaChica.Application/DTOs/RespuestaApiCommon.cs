using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Envoltura estandar de las respuestas del APICommon: { "data": ..., "meta": ... }.
    /// El payload util siempre viene dentro de <c>data</c>, nunca en la raiz.
    /// </summary>
    public sealed record RespuestaApiCommon<T>
    {
        public T? Data { get; init; }
        public MetaApiCommon? Meta { get; init; }
    }

    /// <summary>
    /// Bloque de paginacion que el APICommon incluye en todas sus respuestas, incluso
    /// en las que devuelven un solo registro. Se modela para que la deserializacion no
    /// lo descarte en silencio, aunque en las consultas de un unico usuario no aporte.
    /// </summary>
    public sealed record MetaApiCommon
    {
        public int TotalCount { get; init; }
        public int PageSize { get; init; }
        public int CurrentPage { get; init; }
        public int TotalPages { get; init; }
        public bool HasNextPage { get; init; }
        public bool HasPreviousPage { get; init; }
        public string? NextPageUrl { get; init; }
        public string? PreviousPageUrl { get; init; }
    }
}

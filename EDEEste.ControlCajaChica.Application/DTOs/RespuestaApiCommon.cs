using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
  /* Envoltura estándar de las respuestas del APICommon: { "data": ..., "meta": ... }.
     El payload útil siempre viene dentro de <c>data</c>, nunca en la raiz. */
    public sealed record RespuestaApiCommon<T>
    {
        public T? Data { get; init; }
        public MetaApiCommon? Meta { get; init; }
    }

  /* Bloque de paginación que el APICommon incluye en todas sus respuestas, incluso
     en las que devuelven un solo registro. Se modela para que la deserialización no
     lo descarte en silencio, aunque en las consultas de un único usuario no aporte. */
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

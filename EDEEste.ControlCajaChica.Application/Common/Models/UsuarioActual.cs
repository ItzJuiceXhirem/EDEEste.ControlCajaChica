using System;

namespace EDEEste.ControlCajaChica.Application.Common.Models
{
    /* Foto del usuario que está ejecutando la operación actual. Es un dato plano a
       propósito: la capa de Application no necesita conocer ClaimsPrincipal ni nada
       de ASP.NET para saber a quién atribuirle una acción en la auditoría */
    public sealed record UsuarioActual(string? Id, string? Nombre, bool EstaAutenticado)
    {
        //se usa cuando la operación la dispara el sistema y no una persona
        public static readonly UsuarioActual Anonimo = new(null, null, false);
    }
}

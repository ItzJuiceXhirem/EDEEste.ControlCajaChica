using System;

namespace EDEEste.ControlCajaChica.Application.Common.Models
{
    /* Foto del usuario que esta ejecutando la operacion actual. Es un dato plano a
       proposito: la capa de aplicacion no necesita conocer ClaimsPrincipal ni nada
       de ASP.NET para saber a quien atribuirle una accion en la auditoria*/
    public sealed record UsuarioActual(string? Id, string? Nombre, bool EstaAutenticado)
    {
        //se usa cuando la operacion la dispara el sistema y no una persona
        public static readonly UsuarioActual Anonimo = new(null, null, false);
    }
}

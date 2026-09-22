using System;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Common.Models
{
    /// <summary>
    /// Resultado de verificar unas credenciales.
    ///
    /// Deliberadamente NO trae un motivo de fallo detallado: quien la consume la
    /// traduce siempre al mismo mensaje genérico ("usuario o contraseña inválidos"),
    /// así que distinguir "no existe" de "contraseña incorrecta" solo serviría para
    /// filtrar qué cuentas existen.
    ///
    /// <see cref="Perfil"/> solo se llena en modo ActiveDirectory: es la ficha que
    /// devuelve el directorio, para poder crear o refrescar el registro local del
    /// usuario con sus datos reales (nombre, departamento, correo).
    /// </summary>
    public sealed record ResultadoAutenticacion(bool Exitoso, UsuarioDirectorioDto? Perfil)
    {
        public static ResultadoAutenticacion Ok(UsuarioDirectorioDto? perfil = null) =>
            new(true, perfil);

        public static ResultadoAutenticacion Fallo() => new(false, null);
    }
}

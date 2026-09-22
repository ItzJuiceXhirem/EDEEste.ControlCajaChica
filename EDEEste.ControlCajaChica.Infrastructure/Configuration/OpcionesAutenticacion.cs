using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    // De dónde salen las contraseñas del sistema
    public enum ModoAutenticacion
    {
        /* V2: cuentas propias de esta aplicación, con contraseña gestionada por
           ASP.NET Identity. Es el modo por defecto y el único funcional hoy. */
        Local = 0,

        /// <summary>
        /// V1: las credenciales las valida el Active Directory de la empresa via
        /// APICommon. Incompleto: falta la API Key y el contrato del endpoint que
        /// valida credenciales.
        /// </summary>
        ActiveDirectory = 1
    }

    public sealed class OpcionesAutenticacion
    {
        public const string Seccion = "Autenticacion";

        /// <summary>
        /// Se deja en Local por defecto a propósito: si alguien despliega sin
        /// configurar nada, la aplicación arranca con el modo que sí funciona en vez
        /// de con el que está a medias.
        /// </summary>
        public ModoAutenticacion Modo { get; set; } = ModoAutenticacion.Local;
    }
}

using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// De donde salen las contrasenas del sistema.
    /// </summary>
    public enum ModoAutenticacion
    {
        /// <summary>
        /// V2: cuentas propias de esta aplicacion, con contrasena gestionada por
        /// ASP.NET Identity. Es el modo por defecto y el unico funcional hoy.
        /// </summary>
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
        /// Se deja en Local por defecto a proposito: si alguien despliega sin
        /// configurar nada, la aplicacion arranca con el modo que si funciona en vez
        /// de con el que esta a medias.
        /// </summary>
        public ModoAutenticacion Modo { get; set; } = ModoAutenticacion.Local;
    }
}

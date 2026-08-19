using System;

namespace EDEEste.ControlCajaChica.Domain.Enums
{
    /// <summary>
    /// Estado de acceso de una cuenta, independiente de su rol.
    ///
    /// Se mantiene aparte del rol a propósito: "denegado" no es algo que alguien
    /// pueda hacer, es la ausencia de acceso. Teniéndolo separado, denegarle el
    /// acceso a un Custodio no le borra que era Custodio, y además se puede
    /// distinguir a quien nunca fue revisado (Pendiente) de a quien ya se revisó y
    /// se rechazó (Denegado) — algo imposible con un único campo de rol.
    /// </summary>
    public enum EstadoAccesoUsuario
    {
        /// <summary>Se registró y espera que un Administrador lo revise.</summary>
        Pendiente = 1,

        /// <summary>Tiene rol asignado y puede entrar al sistema.</summary>
        Aprobado = 2,

        /// <summary>Un Administrador determinó que no debe tener acceso.</summary>
        Denegado = 3
    }
}

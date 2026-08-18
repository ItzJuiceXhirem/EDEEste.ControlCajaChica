using System;

namespace EDEEste.ControlCajaChica.Domain.Enums
{
    /// <summary>
    /// Estado de acceso de una cuenta, independiente de su rol.
    ///
    /// Se mantiene aparte del rol a proposito: "denegado" no es algo que alguien
    /// pueda hacer, es la ausencia de acceso. Teniendolo separado, denegarle el
    /// acceso a un Custodio no le borra que era Custodio, y ademas se puede
    /// distinguir a quien nunca fue revisado (Pendiente) de a quien ya se reviso y
    /// se rechazo (Denegado) — algo imposible con un unico campo de rol.
    /// </summary>
    public enum EstadoAccesoUsuario
    {
        /// <summary>Se registro y espera que un Administrador lo revise.</summary>
        Pendiente = 1,

        /// <summary>Tiene rol asignado y puede entrar al sistema.</summary>
        Aprobado = 2,

        /// <summary>Un Administrador determino que no debe tener acceso.</summary>
        Denegado = 3
    }
}

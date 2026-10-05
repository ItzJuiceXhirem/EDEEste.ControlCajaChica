using System;

namespace EDEEste.ControlCajaChica.Domain.Enums
{
    /* Estado de acceso de una cuenta, independiente de su rol.
     Se mantiene aparte del rol a propósito, ya que no son algo que alguien
     pueda hacer, sino el estado de su acceso. Teniéndolo separado, denegarle el
     acceso a un Custodio no le borra que era Custodio. */
    public enum EstadoAccesoUsuario
    {
        Pendiente = 1,

        Aprobado = 2,

        Denegado = 3
    }
}

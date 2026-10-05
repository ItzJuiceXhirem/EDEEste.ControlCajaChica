using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Enums
{
    public enum EstadoFondo
    {
        Activo = 1,

        /* Lo pone el sistema, nunca una pantalla: desde que el Gerente aprueba una
           reposición hasta que Finanzas la paga o el Gerente la rechaza. El fondo sigue
           admitiendo gastos y arqueos; lo único que no admite es otra solicitud. */
        EnReposicion = 2,

        // El 3 queda sin usar a propósito: era BloqueadaPorArqueo, que se eliminó
        // porque el arqueo es atómico y no deja nada a medias que bloquear. No se
        // renumera Inactivo: es el valor que ya está guardado en la base de datos.
        Inactivo = 4
    }
}

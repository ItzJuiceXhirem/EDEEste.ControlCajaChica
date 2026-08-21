using EDEEste.ControlCajaChica.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface ICriptografiaService
    {
        /* <summary>HMAC-SHA256 en hexadecimal, con la clave secreta del servidor */
        string CalcularHMAC(string datos);

        /* Compara en tiempo constante la firma esperada contra la recalculada.
           Se usa para verificar filas de la bitacora, que no son ITamperProofEntity */
        bool ValidarFirma(string datos, string firmaGuardada);

        // <summary>Recalcula la firma de la entidad y la compara con la que trae de la BDD
        bool ValidarIntegridad(ITamperProofEntity entidad);
    }
}

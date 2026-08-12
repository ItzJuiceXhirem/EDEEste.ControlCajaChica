using EDEEste.ControlCajaChica.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface ICriptografiaService
    {
        string CalcularHMAC(string datos);
        bool ValidarIntegridad(ITamperProofEntity entidad);
    }
}

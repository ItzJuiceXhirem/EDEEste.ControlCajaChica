using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Interfaces
{
    public interface ITamperProofEntity
    {
        string HashFirma { get; set; }
        string ObtenerCadenaParaHash();
    }
}

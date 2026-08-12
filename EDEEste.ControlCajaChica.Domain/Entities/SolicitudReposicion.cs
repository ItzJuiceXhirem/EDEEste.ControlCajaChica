using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class SolicitudReposicion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        //public FondoCajaChica FondoCajaChica { get; set; } //FK
        public string CodigoSolicitud { get; set; }
        public decimal MontoReclamado { get; set; }
        public DateTime FechaSolicitud { get; set; }
        //string SolicitoUsuarioId FK
        //string AprobadorUsuarioId FK
        public DateTime FechaAprobacion { get; set; }
        //string PagadorUsuarioId FK
        public DateTime FechaPago { get; set; }
        //string ReferenciaPago
        public string RutaPdfConsolidado { get; set; }
        string Estado {  get; set; }
    }
}

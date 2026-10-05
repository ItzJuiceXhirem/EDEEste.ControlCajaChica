using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class SolicitudReposicion : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid FondoCajaChicaId { get; set; }
        public FondoCajaChica? FondoCajaChica { get; set; }

        public decimal MontoReclamado { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string SolicitoUsuarioId { get; set; } = string.Empty; //FK hacia Usuario (Identity)
        public string? GerenteUsuarioId { get; set; } //FK hacia Usuario (Identity)
        public DateTime? FechaAprobacion { get; set; }
        public string? FinanzasUsuarioId { get; set; } //FK hacia Usuario (Identity)
        public DateTime? FechaPago { get; set; }
        public string? ReferenciaPago { get; set;}
        public string? RutaPdfConsolidado { get; set; }

        // SHA-256 del expediente al generarse, para detectar si el archivo en disco se
        // reemplaza despues. Null en las solicitudes creadas antes de que existiera.
        public string? HashPdfConsolidado { get; set; }

        public EstadoReposicion Estado { get; set; }

        /* Cada motivo guarda la ÚLTIMA vez que ocurrió esa acción: una solicitud puede
           devolverse y aprobarse de nuevo varias veces, y la bitácora conserva todas las
           rondas. Cada uno lo lee quien tiene que actuar después. */

        // Por qué Finanzas la devolvió al Gerente. Lo lee el Gerente.
        public string? MotivoDevolucion { get; set; }

        // Por qué el Gerente la aprobó de nuevo tras una devolución. Lo lee Finanzas.
        public string? MotivoReaprobacion { get; set; }

        // Por qué se rechazó. Lo lee el Custodio, para corregir y volver a solicitar.
        public string? MotivoRechazo { get; set; }

        // Gastos que entran en esta reposición; es lo que alimenta el PDF consolidado
        public ICollection<Gasto> Gastos { get; set; } = new List<Gasto>();

        public string HashFirma { get; set; } = string.Empty;

        [NotMapped]
        public bool IntegridadVerificada { get; set; } = true;

        /* Se firma toda la cadena de aprobación (quien solicitó, quien aprobó, quien
           pagó y cuándo) porque es justo lo que un fraude querría reescribir */
        public string ObtenerCadenaParaHash()
        {
            return new ConstructorFirma(nameof(SolicitudReposicion))
                .Agregar(Id)
                .Agregar(FondoCajaChicaId)
                .Agregar(MontoReclamado)
                .Agregar(FechaSolicitud)
                .Agregar(SolicitoUsuarioId)
                .Agregar(GerenteUsuarioId)
                .Agregar(FechaAprobacion)
                .Agregar(FinanzasUsuarioId)
                .Agregar(FechaPago)
                .Agregar(ReferenciaPago)
                .Agregar(RutaPdfConsolidado)
                .Agregar((long)Estado)
                .Agregar(IsDeleted)
                // Campos agregados despues de que ya habia solicitudes firmadas: las que
                // no los usan deben seguir produciendo exactamente la misma cadena, o
                // todas se leerian como manipuladas. Los nuevos van siempre al final de
                // esta lista, nunca en medio; ver ConstructorFirma.AgregarOpcionalesAlFinal.
                .AgregarOpcionalesAlFinal(HashPdfConsolidado, MotivoDevolucion, MotivoReaprobacion, MotivoRechazo)
                .ToString();
        }
    }
}

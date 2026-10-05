using System;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Infrastructure.Identity
{
    /// <summary>
    /// Solicitud de restablecimiento de contraseña, exclusiva del flujo V2 (cuentas
    /// manuales; en V1 la contraseña es la de Windows y no se toca desde aquí).
    ///
    /// Vive en Infrastructure junto a Usuario porque es una preocupación de
    /// identidad, no de caja chica -- igual que Usuario, no tiene ninguna dependencia
    /// de framework que la obligue a estar aquí, es una decisión de organización por
    /// tema.
    ///
    /// Hereda AuditableEntity para que el interceptor de auditoría deje rastro (quién
    /// y cuándo) de cada fila que toca; ResueltaPorUsuarioId es distinto de
    /// ModificadoPorId porque este último lo pisa cualquier guardado posterior
    /// (incluida la transición a Usada), mientras que ResueltaPorUsuarioId es un
    /// hecho de negocio fijo: que Administrador decidió Aprobar o Ignorar.
    /// </summary>
    public class SolicitudPasswordReset : AuditableEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UsuarioId { get; set; } = string.Empty;

        public DateTime FechaSolicitud { get; set; }

        public EstadoSolicitudPasswordReset Estado { get; set; } = EstadoSolicitudPasswordReset.Pendiente;

        /// <summary>
        /// Token de reseteo de ASP.NET Identity, generado recién al Aprobar (no al
        /// solicitar) para que su vencimiento natural (1 día por defecto) cuente
        /// desde que el Administrador habilitó el cambio, no desde que el usuario lo
        /// pidió -- que puede haber sido días antes.
        /// </summary>
        public string? TokenReseteo { get; set; }

        /// <summary>
        /// Hash SHA-256 (hex) de un secreto aleatorio de un solo uso, generado al
        /// Aprobar. El secreto en sí NUNCA se guarda ni se le entrega a quien pidió
        /// el restablecimiento -- solo al Administrador, para que lo mande por Teams.
        /// Sin este secreto, el Id de la solicitud (que el solicitante SÍ conoce,
        /// porque se lo devuelve la propia pantalla al pedirlo) no alcanza para
        /// completar el cambio de contraseña.
        /// </summary>
        public string? HashSecreto { get; set; }

        /// <summary>
        /// El enlace deja de servir 12 horas después de Aprobar, sin importar que el
        /// token de Identity (que vence por su cuenta) todavía sea válido -- es una
        /// segunda ventana, más corta y bajo control nuestro.
        /// </summary>
        public DateTime? FechaExpiracionSecreto { get; set; }

        public DateTime? FechaResolucion { get; set; }

        // Administrador que aprobó o ignoró la solicitud
        public string? ResueltaPorUsuarioId { get; set; }
    }
}

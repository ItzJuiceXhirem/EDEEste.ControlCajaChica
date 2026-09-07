using System;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Infrastructure.Identity
{
    /// <summary>
    /// Solicitud de restablecimiento de contrasena, exclusiva del flujo V2 (cuentas
    /// manuales; en V1 la contrasena es la de Windows y no se toca desde aqui).
    ///
    /// Vive en Infrastructure junto a Usuario porque es una preocupacion de
    /// identidad, no de caja chica -- igual que Usuario, no tiene ninguna dependencia
    /// de framework que la obligue a estar aqui, es una decision de organizacion por
    /// tema.
    ///
    /// Hereda AuditableEntity para que el interceptor de auditoria deje rastro (quien
    /// y cuando) de cada fila que toca; ResueltaPorUsuarioId es distinto de
    /// ModificadoPorId porque este ultimo lo pisa cualquier guardado posterior
    /// (incluida la transicion a Usada), mientras que ResueltaPorUsuarioId es un
    /// hecho de negocio fijo: que Administrador decidio Aprobar o Ignorar.
    /// </summary>
    public class SolicitudPasswordReset : AuditableEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UsuarioId { get; set; } = string.Empty;

        public DateTime FechaSolicitud { get; set; }

        public EstadoSolicitudPasswordReset Estado { get; set; } = EstadoSolicitudPasswordReset.Pendiente;

        /// <summary>
        /// Token de reseteo de ASP.NET Identity, generado recien al Aprobar (no al
        /// solicitar) para que su vencimiento natural (1 dia por defecto) cuente
        /// desde que el Administrador habilito el cambio, no desde que el usuario lo
        /// pidio -- que puede haber sido dias antes.
        /// </summary>
        public string? TokenReseteo { get; set; }

        /// <summary>
        /// Hash SHA-256 (hex) de un secreto aleatorio de un solo uso, generado al
        /// Aprobar. El secreto en si NUNCA se guarda ni se le entrega a quien pidio
        /// el restablecimiento -- solo al Administrador, para que lo mande por Teams.
        /// Sin este secreto, el Id de la solicitud (que el solicitante SI conoce,
        /// porque se lo devuelve la propia pantalla al pedirlo) no alcanza para
        /// completar el cambio de contrasena.
        /// </summary>
        public string? HashSecreto { get; set; }

        /// <summary>
        /// El enlace deja de servir 12 horas despues de Aprobar, sin importar que el
        /// token de Identity (que vence por su cuenta) todavia sea valido -- es una
        /// segunda ventana, mas corta y bajo control nuestro.
        /// </summary>
        public DateTime? FechaExpiracionSecreto { get; set; }

        public DateTime? FechaResolucion { get; set; }

        /// <summary>Administrador que aprobo o ignoro la solicitud.</summary>
        public string? ResueltaPorUsuarioId { get; set; }
    }
}

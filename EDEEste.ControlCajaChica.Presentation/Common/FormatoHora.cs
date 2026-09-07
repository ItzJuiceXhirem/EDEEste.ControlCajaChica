using System;
using System.Globalization;

namespace EDEEste.ControlCajaChica.Presentation.Common
{
    /// <summary>
    /// Formato de hora compartido entre pantallas que muestran fecha/hora de forma
    /// distinta (Reposiciones, Usuarios y ManageLayout arman su propia cadena final:
    /// con coma o sin ella, con "Hoy"/"Ayer" o sin eso), pero todas necesitan la MISMA
    /// hora de 12 horas con sufijo a.m./p.m. Vive aca y no repetido en cada una porque
    /// es la unica parte de verdad identica entre las tres, y la que mas facil se
    /// rompe sin darse cuenta.
    /// </summary>
    public static class FormatoHora
    {
        /// <summary>
        /// "3:45 p.m.". Recibe la hora YA convertida a local -- este helper no sabe de
        /// zonas horarias, solo de como se escribe una hora una vez decidida cual es.
        ///
        /// CultureInfo.InvariantCulture y no la del servidor: con la cultura del SO,
        /// "h:mm tt" puede salir como "3:45 PM" (ingles) o con otro separador segun la
        /// maquina donde corra la app -- para una pantalla que siempre habla en
        /// espanol dominicano, el AM/PM no deberia depender de esa configuracion.
        /// </summary>
        public static string HoraCorta(DateTime horaLocal)
        {
            var sufijo = horaLocal.Hour < 12 ? "a.m." : "p.m.";
            return $"{horaLocal.ToString("h:mm", CultureInfo.InvariantCulture)} {sufijo}";
        }
    }
}

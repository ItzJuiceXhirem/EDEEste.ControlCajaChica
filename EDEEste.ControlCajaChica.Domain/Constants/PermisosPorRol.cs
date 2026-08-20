using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Qué permisos trae cada rol. Es la única fuente de verdad de la matriz de
    /// accesos, y vive en Domain porque es una regla de negocio (segregación de
    /// funciones), no un detalle de la capa web.
    ///
    /// Dos criterios que explican el reparto:
    ///
    /// 1. "Ver" viene con la acción: quien registra gastos necesita la pantalla de
    ///    gastos para trabajar. El historial además lo ven Custodio y Gerente por
    ///    transparencia, para que puedan comprobar algo sin pedírselo al Auditor.
    ///
    /// 2. El Administrador NO tiene permisos operativos. Configura fondos, límites,
    ///    categorías y usuarios, pero no registra gastos ni aprueba reposiciones:
    ///    quien fija el límite no debe poder gastarse el fondo. Es lo que exige una
    ///    auditoría de caja chica y por eso el Auditor tampoco tiene ninguna acción,
    ///    solo lectura.
    ///
    /// 3. Anular un gasto son dos permisos y no uno: el Custodio solo puede
    ///    <see cref="Permisos.SolicitarAnulacionGasto"/> (deja el gasto pendiente, sin
    ///    devolver el dinero) y el Gerente tiene <see cref="Permisos.AnularGasto"/>,
    ///    que es el que efectivamente devuelve el efectivo al fondo. Con un solo
    ///    permiso, quien registra el gasto podría deshacerlo sin que nadie lo revise.
    /// </summary>
    public static class PermisosPorRol
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Mapa =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [RolesApp.Custodio] = new HashSet<string>
                {
                    Permisos.VerGastos,
                    Permisos.RegistrarGasto,
                    Permisos.SolicitarAnulacionGasto,
                    Permisos.VerArqueos,
                    Permisos.EjecutarArqueo,
                    Permisos.VerReposiciones,
                    Permisos.SolicitarReposicion,
                    Permisos.DescargarExpediente,
                    Permisos.ConsultarHistorial
                },

                [RolesApp.Administrador] = new HashSet<string>
                {
                    Permisos.ConfigurarFondos,
                    Permisos.ConfigurarCategorias,
                    Permisos.AdministrarUsuarios
                },

                [RolesApp.Gerente] = new HashSet<string>
                {
                    Permisos.VerGastos,
                    Permisos.RevisarGastos,
                    Permisos.AnularGasto,
                    Permisos.VerArqueos,
                    Permisos.VerReposiciones,
                    Permisos.AprobarReposicion,
                    Permisos.DescargarExpediente,
                    Permisos.ConsultarHistorial
                },

                [RolesApp.Finanzas] = new HashSet<string>
                {
                    Permisos.VerReposiciones,
                    Permisos.PagarReposicion,
                    Permisos.DescargarExpediente
                },

                [RolesApp.Auditor] = new HashSet<string>
                {
                    Permisos.VerGastos,
                    Permisos.VerArqueos,
                    Permisos.VerReposiciones,
                    Permisos.DescargarExpediente,
                    Permisos.ConsultarHistorial
                }
            };

        /// <summary>
        /// Un rol desconocido (o vacío) no otorga nada. Es deliberado: un usuario sin
        /// rol asignado todavía no fue aprobado por un Administrador, y lo correcto
        /// mientras tanto es que no pueda hacer nada.
        /// </summary>
        public static bool RolTienePermiso(string? rol, string permiso) =>
            !string.IsNullOrWhiteSpace(rol)
            && Mapa.TryGetValue(rol, out var permisos)
            && permisos.Contains(permiso);

        public static IReadOnlySet<string> Obtener(string? rol) =>
            !string.IsNullOrWhiteSpace(rol) && Mapa.TryGetValue(rol, out var permisos)
                ? permisos
                : new HashSet<string>();
    }
}

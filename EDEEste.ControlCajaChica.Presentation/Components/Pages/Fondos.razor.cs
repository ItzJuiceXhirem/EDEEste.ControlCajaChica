using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Application.Features.Fondos;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Fondos
    {
        /// <summary>
        /// La escala grafica de "cuanto puede costar un gasto" llega a esta fraccion
        /// del monto fijo. Es una decision de presentacion, no de negocio: el tope
        /// maximo permitido es 30%, pero una regla dibujada hasta ahi dejaria los
        /// valores reales (2-3%) aplastados contra el extremo izquierdo.
        /// </summary>
        private const decimal FraccionEscalaGasto = 0.10m;

        [Inject]
        private IFondoRepository RepositorioFondos { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private CrearFondoHandler HandlerCrear { get; set; } = default!;

        [Inject]
        private ActualizarParametrosFondoHandler HandlerActualizar { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<UsuarioResumenDto>? custodios;

        // CustodioId guarda el Id de Identity (un GUID). Se guarda el DTO completo y
        // no solo el nombre porque la tarjeta muestra el usuario Y el nombre real.
        private Dictionary<string, UsuarioResumenDto> usuariosPorId = new();

        private Vista vista = Vista.Lista;

        // ── Lista ────────────────────────────────────────────────────────────────
        private string busqueda = string.Empty;
        private Orden orden = Orden.Todos;

        // ── Alta ─────────────────────────────────────────────────────────────────
        private int paso = 1;
        private readonly EntradaFondo entrada = new();

        // ── Edicion ──────────────────────────────────────────────────────────────
        private FondoCajaChica? fondoEditando;
        private EntradaEdicionFondo? entradaEdicion;

        // Custodio elegido en el desplegable que todavia no se ha confirmado. Se
        // guarda aparte de entradaEdicion.CustodioId para que el cambio no quede
        // aplicado hasta que el Administrador acepte el modal.
        private string? custodioPropuesto;

        private bool guardando;
        private readonly List<string> errores = new();
        private string? exito;

        protected override async Task OnInitializedAsync()
        {
            await RecargarAsync();

            var usuarios = await Identidad.ListarUsuariosAsync();
            usuariosPorId = usuarios.ToDictionary(u => u.Id);

            // Solo cuentas ya aprobadas y con el rol Custodio: es la lista de la que
            // el Administrador puede elegir para asignar un fondo.
            custodios = usuarios
                .Where(u => u.EstadoAcceso == EstadoAccesoUsuario.Aprobado
                            && string.Equals(u.Rol, RolesApp.Custodio, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private async Task RecargarAsync() => fondos = await RepositorioFondos.ListarAsync();

        // ── Nombres ──────────────────────────────────────────────────────────────

        private string UsuarioDe(string? custodioId) =>
            string.IsNullOrWhiteSpace(custodioId)
                ? "(sin asignar)"
                : usuariosPorId.TryGetValue(custodioId, out var u) ? u.Usuario : custodioId;

        private string NombreDe(string? custodioId) =>
            string.IsNullOrWhiteSpace(custodioId)
                ? string.Empty
                : usuariosPorId.TryGetValue(custodioId, out var u) ? u.Nombre : string.Empty;

        // ── Cifras derivadas ─────────────────────────────────────────────────────

        private static decimal PorcentajeUso(FondoCajaChica fondo) =>
            fondo.MontoFijo > 0 ? fondo.BalanceActual / fondo.MontoFijo * 100m : 0m;

        private static decimal UmbralPorcentaje(FondoCajaChica fondo) =>
            fondo.PorcentajeAlertaReposicion > 0 ? fondo.PorcentajeAlertaReposicion : LimitesFondo.AlertaReposicionPorDefecto;

        private static decimal UmbralPesos(FondoCajaChica fondo) =>
            fondo.MontoFijo * (UmbralPorcentaje(fondo) / 100m);

        /// <summary>
        /// El limite efectivo por gasto: el mas estricto entre el tope porcentual y
        /// el limite absoluto, si lo hay. Misma formula que RegistrarGastoHandler.
        /// </summary>
        private static decimal LimiteEfectivo(decimal montoFijo, decimal porcentaje, decimal limite)
        {
            var tope = montoFijo * (porcentaje / 100m);
            return limite > 0 ? Math.Min(limite, tope) : tope;
        }

        private static decimal LimiteEfectivo(FondoCajaChica fondo) =>
            LimiteEfectivo(fondo.MontoFijo, fondo.PorcentajeMaximoPorGasto, fondo.LimitePorGasto);

        /// <summary>
        /// El color del medidor lo decide el NIVEL, no el estado: verde por encima
        /// del 50%, ambar entre el 50% y el umbral, rojo al tocar el umbral o por
        /// debajo. Un fondo inactivo se apaga en gris, que es otro eje — no dice
        /// "nivel", dice "retirado de circulacion".
        /// </summary>
        private static string ClaseMedidor(FondoCajaChica fondo)
        {
            if (fondo.Estado == EstadoFondo.Inactivo)
            {
                return "fnd-mute";
            }

            var uso = PorcentajeUso(fondo);

            if (uso <= UmbralPorcentaje(fondo))
            {
                return "fnd-low";
            }

            return uso <= 50m ? "fnd-warn" : string.Empty;
        }

        /// <summary>Ancho de una barra en porcentaje, acotado a [0, 100].</summary>
        private static string Ancho(decimal porcentaje) =>
            Math.Clamp(porcentaje, 0m, 100m).ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// Hasta donde llega la escala de "un gasto".
        ///
        /// El caso normal es el <see cref="FraccionEscalaGasto"/> del monto fijo, que
        /// es lo que hace legibles los valores reales (topes del 2-3%). Pero el tope
        /// permitido llega al 30%, asi que un tope legitimo del 15% se saldria de esa
        /// regla y su marca desapareceria del dibujo. Cuando eso pasa, la escala crece
        /// para que quepa: mas vale una regla ancha que una marca invisible.
        /// </summary>
        private static decimal EscalaGasto(decimal montoFijo, decimal tope, decimal limite) =>
            Math.Max(montoFijo * FraccionEscalaGasto, Math.Max(tope, limite));

        /// <summary>
        /// Posicion de una marca dentro de la escala, en porcentaje. Devuelve null
        /// para un valor que no se dibuja (cero o negativo).
        /// </summary>
        private static decimal? PosicionEnEscala(decimal escala, decimal valor)
        {
            if (escala <= 0m || valor <= 0m)
            {
                return null;
            }

            return valor / escala * 100m;
        }

        // ── Lista: busqueda y orden ──────────────────────────────────────────────

        private IEnumerable<FondoCajaChica> FondosFiltrados
        {
            get
            {
                if (fondos is null)
                {
                    return [];
                }

                var filtrados = string.IsNullOrWhiteSpace(busqueda)
                    ? fondos
                    : fondos.Where(f => Coincide(f, busqueda.Trim()));

                // El orden va por MONTO FIJO (el tamano del fondo), no por el balance
                // en caja: la cifra grande de la tarjeta es el balance, asi que es
                // facil equivocarse y ordenar por la que se ve.
                return orden switch
                {
                    Orden.MayorAMenor => filtrados.OrderByDescending(f => f.MontoFijo),
                    Orden.MenorAMayor => filtrados.OrderBy(f => f.MontoFijo),
                    _ => filtrados
                };
            }
        }

        private bool Coincide(FondoCajaChica fondo, string texto) =>
            ContieneSinAcentos(UsuarioDe(fondo.CustodioId), texto)
            || ContieneSinAcentos(NombreDe(fondo.CustodioId), texto);

        private static bool ContieneSinAcentos(string origen, string busqueda) =>
            !string.IsNullOrEmpty(origen)
            && Normalizar(origen).Contains(Normalizar(busqueda), StringComparison.OrdinalIgnoreCase);

        private static string Normalizar(string valor)
        {
            var descompuesto = valor.Normalize(NormalizationForm.FormD);
            var sinDiacriticos = descompuesto.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            return new string(sinDiacriticos.ToArray()).Normalize(NormalizationForm.FormC);
        }

        private void CambiarOrden(ChangeEventArgs e) =>
            orden = Enum.TryParse<Orden>(e.Value?.ToString(), out var valor) ? valor : Orden.Todos;

        /// <summary>
        /// Custodios aprobados que todavia no tienen ningun fondo asignado. Es el dato
        /// que explica por que un Administrador entraria a esta pantalla, y hoy no
        /// existe en ninguna parte de la app.
        /// </summary>
        private int CustodiosSinFondo
        {
            get
            {
                if (custodios is null || fondos is null)
                {
                    return 0;
                }

                var conFondo = fondos.Select(f => f.CustodioId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return custodios.Count(c => !conFondo.Contains(c.Id));
            }
        }

        private bool HayCustodios => custodios is { Count: > 0 };

        /// <summary>
        /// Si ese custodio ya responde por otro fondo. Es la misma regla que aplican
        /// los handlers; aqui se repite sobre la lista ya cargada para avisar al
        /// instante en vez de esperar al viaje de ida y vuelta. La autoridad sigue
        /// siendo el handler, que la comprueba contra la BDD.
        /// </summary>
        private bool CustodioOcupado(string? custodioId, Guid? excluirFondoId = null) =>
            !string.IsNullOrWhiteSpace(custodioId)
            && fondos is not null
            && fondos.Any(f => string.Equals(f.CustodioId, custodioId, StringComparison.OrdinalIgnoreCase)
                               && f.Id != excluirFondoId);

        private string FondoDe(string? custodioId) =>
            fondos?.FirstOrDefault(f => string.Equals(f.CustodioId, custodioId, StringComparison.OrdinalIgnoreCase))
                is { } f
                ? $"RD$ {f.MontoFijo:N2}"
                : string.Empty;

        // ── Navegacion entre vistas ──────────────────────────────────────────────

        private void AbrirLista()
        {
            vista = Vista.Lista;
            fondoEditando = null;
            entradaEdicion = null;
            custodioPropuesto = null;
            errores.Clear();
        }

        private void AbrirNuevo()
        {
            errores.Clear();
            exito = null;
            entrada.Limpiar();

            // Arranca en un custodio que aun no tenga fondo, para no abrir el
            // formulario ya en estado de error. Si todos tienen, cae al primero y el
            // aviso aparece de una vez -- que tambien es informacion util.
            entrada.CustodioId = custodios?.FirstOrDefault(c => !CustodioOcupado(c.Id))?.Id
                                 ?? custodios?.FirstOrDefault()?.Id;
            paso = 1;
            vista = Vista.Nuevo;
        }

        private void AbrirEdicion(FondoCajaChica fondo)
        {
            errores.Clear();
            exito = null;
            fondoEditando = fondo;
            entradaEdicion = new EntradaEdicionFondo
            {
                LimitePorGasto = fondo.LimitePorGasto,
                PorcentajeMaximoPorGasto = fondo.PorcentajeMaximoPorGasto,
                PorcentajeAlertaReposicion = fondo.PorcentajeAlertaReposicion,
                CustodioId = fondo.CustodioId,
                Estado = fondo.Estado
            };
            custodioPropuesto = null;
            vista = Vista.Editar;
        }

        // ── Alta: pasos ──────────────────────────────────────────────────────────

        private void PasoSiguiente()
        {
            errores.Clear();

            var problemas = paso switch
            {
                1 => ValidarPaso1(),
                2 => ValidarPaso2(),
                _ => []
            };

            if (problemas.Count > 0)
            {
                errores.AddRange(problemas);
                return;
            }

            paso = Math.Min(paso + 1, 3);
        }

        private void PasoAnterior()
        {
            errores.Clear();
            paso = Math.Max(paso - 1, 1);
        }

        private List<string> ValidarPaso1()
        {
            var problemas = new List<string>();

            if (entrada.MontoFijo <= 0)
            {
                problemas.Add("El monto fijo debe ser mayor que cero.");
            }

            if (string.IsNullOrWhiteSpace(entrada.CustodioId))
            {
                problemas.Add("Debe asignar un custodio.");
            }
            else if (CustodioOcupado(entrada.CustodioId))
            {
                problemas.Add(
                    $"{UsuarioDe(entrada.CustodioId)} ya tiene un fondo asignado: sólo se permite " +
                    "un custodio por fondo y un fondo por custodio.");
            }

            return problemas;
        }

        /// <summary>
        /// Adelanta las reglas que el handler volvera a comprobar, para no dejar
        /// avanzar hasta el resumen con valores que se van a rechazar. El handler
        /// sigue siendo la autoridad: esto solo evita el viaje de ida y vuelta.
        /// </summary>
        private List<string> ValidarPaso2() =>
            ValidarReglas(entrada.MontoFijo, entrada.PorcentajeMaximoPorGasto,
                          entrada.LimitePorGasto, entrada.PorcentajeAlertaReposicion);

        private static List<string> ValidarReglas(
            decimal montoFijo, decimal porcentaje, decimal limite, decimal alerta)
        {
            var problemas = new List<string>();

            if (porcentaje < LimitesFondo.TopePorGastoMinimo || porcentaje > LimitesFondo.TopePorGastoMaximo)
            {
                problemas.Add(
                    $"El tope por gasto debe estar entre {LimitesFondo.TopePorGastoMinimo:N0}% y " +
                    $"{LimitesFondo.TopePorGastoMaximo:N0}%.");
            }

            if (limite < 0)
            {
                problemas.Add("El límite por gasto no puede ser negativo.");
            }
            else if (limite > 0)
            {
                var tope = montoFijo * (porcentaje / 100m);
                if (limite > tope)
                {
                    problemas.Add(
                        $"El límite por gasto (RD$ {limite:N2}) no puede superar el tope reglamentario " +
                        $"de RD$ {tope:N2}.");
                }
            }

            if (alerta < LimitesFondo.AlertaReposicionMinima || alerta > LimitesFondo.AlertaReposicionMaxima)
            {
                problemas.Add(
                    $"El porcentaje de alerta para reposición debe estar entre " +
                    $"{LimitesFondo.AlertaReposicionMinima:N0}% y {LimitesFondo.AlertaReposicionMaxima:N0}%.");
            }

            return problemas;
        }

        private async Task CrearAsync()
        {
            errores.Clear();
            exito = null;
            guardando = true;

            try
            {
                var resultado = await HandlerCrear.EjecutarAsync(new CrearFondoCommand
                {
                    MontoFijo = entrada.MontoFijo,
                    LimitePorGasto = entrada.LimitePorGasto,
                    PorcentajeMaximoPorGasto = entrada.PorcentajeMaximoPorGasto,
                    PorcentajeAlertaReposicion = entrada.PorcentajeAlertaReposicion,
                    CustodioId = entrada.CustodioId ?? string.Empty
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = $"Fondo creado con RD$ {entrada.MontoFijo:N2}.";
                await RecargarAsync();
                AbrirLista();
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                guardando = false;
            }
        }

        // ── Edicion ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Cambiar de custodio traspasa una caja con dinero dentro de una persona a
        /// otra, asi que el desplegable no aplica el cambio: lo deja propuesto y abre
        /// la confirmacion. Cualquier otro parametro se edita sin ceremonia.
        /// </summary>
        private void ProponerCustodio(ChangeEventArgs e)
        {
            var elegido = e.Value?.ToString() ?? string.Empty;

            if (entradaEdicion is null || string.Equals(elegido, entradaEdicion.CustodioId, StringComparison.Ordinal))
            {
                return;
            }

            custodioPropuesto = elegido;
        }

        private void ConfirmarCambioDeCustodio()
        {
            if (entradaEdicion is not null && custodioPropuesto is not null)
            {
                entradaEdicion.CustodioId = custodioPropuesto;
            }

            custodioPropuesto = null;
        }

        private void CancelarCambioDeCustodio() => custodioPropuesto = null;

        private async Task GuardarEdicionAsync()
        {
            if (fondoEditando is not { } fondo || entradaEdicion is null)
            {
                return;
            }

            errores.Clear();
            exito = null;

            var problemas = ValidarReglas(fondo.MontoFijo, entradaEdicion.PorcentajeMaximoPorGasto,
                                          entradaEdicion.LimitePorGasto, entradaEdicion.PorcentajeAlertaReposicion);

            if (CustodioOcupado(entradaEdicion.CustodioId, fondo.Id))
            {
                problemas.Add(
                    $"{UsuarioDe(entradaEdicion.CustodioId)} ya tiene un fondo asignado: sólo se permite " +
                    "un custodio por fondo y un fondo por custodio.");
            }

            if (problemas.Count > 0)
            {
                errores.AddRange(problemas);
                return;
            }

            guardando = true;

            try
            {
                var resultado = await HandlerActualizar.EjecutarAsync(new ActualizarParametrosFondoCommand
                {
                    FondoCajaChicaId = fondo.Id,
                    LimitePorGasto = entradaEdicion.LimitePorGasto,
                    PorcentajeMaximoPorGasto = entradaEdicion.PorcentajeMaximoPorGasto,
                    PorcentajeAlertaReposicion = entradaEdicion.PorcentajeAlertaReposicion,
                    CustodioId = entradaEdicion.CustodioId,
                    Estado = entradaEdicion.Estado
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Fondo actualizado.";
                await RecargarAsync();
                AbrirLista();
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                guardando = false;
            }
        }

        // ── Etiquetas ────────────────────────────────────────────────────────────

        private static string ClaseEstado(EstadoFondo estado) => estado switch
        {
            EstadoFondo.Activo => "fnd-p-ok",
            EstadoFondo.EnReposicion => "fnd-p-info",
            EstadoFondo.BloqueadaPorArqueo => "fnd-p-warn",
            _ => "fnd-p-dark"
        };

        private static string EtiquetaEstado(EstadoFondo estado) => estado switch
        {
            EstadoFondo.Activo => "Activo",
            EstadoFondo.EnReposicion => "En reposición",
            EstadoFondo.BloqueadaPorArqueo => "Bloqueado por arqueo",
            EstadoFondo.Inactivo => "Inactivo",
            _ => estado.ToString()
        };

        /// <summary>
        /// La nota que explica un estado. Un fondo Activo y por encima del umbral no
        /// lleva ninguna: si todos los estados explicaran algo, la explicacion
        /// dejaria de leerse como una senal.
        /// </summary>
        private static string? NotaEstado(FondoCajaChica fondo) => fondo.Estado switch
        {
            EstadoFondo.EnReposicion => "Está en proceso de reposición de fondos.",
            EstadoFondo.BloqueadaPorArqueo =>
                "Un arqueo dejó el fondo detenido: no admite gastos nuevos hasta completar el arqueo.",
            EstadoFondo.Inactivo => "Retirado de circulación. Su histórico se conserva.",
            _ when PorcentajeUso(fondo) <= UmbralPorcentaje(fondo) =>
                "Bajo el umbral: el custodio ya puede pedir reposición.",
            _ => null
        };

        private enum Vista
        {
            Lista,
            Nuevo,
            Editar
        }

        private enum Orden
        {
            Todos,
            MayorAMenor,
            MenorAMayor
        }

        /// <summary>
        /// En cero, el campo del monto fijo va vacío con el 0 de marca de agua --
        /// mismo patrón que el conteo de denominaciones de Arqueos: así se escribe
        /// la cifra directamente, sin tener que borrar antes el cero que había.
        /// </summary>
        private static string MontoFijoTexto(decimal monto) =>
            monto == 0 ? string.Empty : monto.ToString(CultureInfo.InvariantCulture);

        private static void FijarMontoFijo(EntradaFondo entrada, string? valor) =>
            entrada.MontoFijo = decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var monto)
                ? Math.Max(0, monto)
                : 0;

        private sealed class EntradaFondo
        {
            public decimal MontoFijo { get; set; }
            public decimal LimitePorGasto { get; set; }
            public decimal PorcentajeMaximoPorGasto { get; set; } = LimitesFondo.TopePorGastoPorDefecto;
            public decimal PorcentajeAlertaReposicion { get; set; } = LimitesFondo.AlertaReposicionPorDefecto;
            public string? CustodioId { get; set; }

            public void Limpiar()
            {
                MontoFijo = 0;
                LimitePorGasto = 0;
                PorcentajeMaximoPorGasto = LimitesFondo.TopePorGastoPorDefecto;
                PorcentajeAlertaReposicion = LimitesFondo.AlertaReposicionPorDefecto;
                CustodioId = null;
            }
        }

        private sealed class EntradaEdicionFondo
        {
            public decimal LimitePorGasto { get; set; }
            public decimal PorcentajeMaximoPorGasto { get; set; }
            public decimal PorcentajeAlertaReposicion { get; set; }
            public string CustodioId { get; set; } = string.Empty;
            public EstadoFondo Estado { get; set; }
        }
    }
}

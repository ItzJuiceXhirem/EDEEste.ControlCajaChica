using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Features.Auditoria
{
    /// <summary>
    /// Verifica la cadena de la bitácora: el lado de lectura de lo que
    /// AuditoriaInterceptor.FirmarCadenaDeLogs escribe. Recorre las filas en el orden
    /// de la cadena (Secuencia) y en cada una comprueba dos cosas por separado:
    ///  - su firma: ninguna fila fue editada después de escribirse;
    ///  - su enlace: HashAnterior es la firma de la fila anterior (o GENESIS en la
    ///    primera). Un enlace roto delata filas borradas o reordenadas justo antes.
    ///
    /// Tras una fila editada se sigue encadenando contra el HashFirma que tiene
    /// guardado, así una sola edición no se reporta también como un enlace roto en la
    /// fila siguiente: cada ruptura se cuenta una vez, en el lugar donde ocurrió.
    /// </summary>
    public sealed class VerificarCadenaBitacoraHandler
    {
        // Filas por consulta: suficiente para que el recorrido vaya rápido sin traer
        // la tabla entera a memoria de una vez.
        public const int TamanoLote = 500;

        // Una cadena muy dañada no necesita listar miles de rupturas para ser
        // evidente; el total se informa igual.
        public const int MaximoRupturasListadas = 100;

        private readonly IBitacoraRepository _bitacora;
        private readonly ICriptografiaService _criptografia;
        private readonly IAutorizacionService _autorizacion;

        public VerificarCadenaBitacoraHandler(
            IBitacoraRepository bitacora,
            ICriptografiaService criptografia,
            IAutorizacionService autorizacion)
        {
            _bitacora = bitacora;
            _criptografia = criptografia;
            _autorizacion = autorizacion;
        }

        public async Task<ResultadoOperacion<ResultadoVerificacionCadena>> EjecutarAsync(
            VerificarCadenaBitacoraCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.ConsultarBitacora, cancellationToken))
            {
                return ResultadoOperacion<ResultadoVerificacionCadena>.Fallo(
                    "No tiene permiso para revisar la integridad de la bitácora.");
            }

            var huellaAnterior = string.IsNullOrWhiteSpace(comando.HuellaAnterior) ? null : comando.HuellaAnterior.Trim();
            bool? huellaAnteriorEncontrada = huellaAnterior is null ? null : false;

            var rupturas = new List<RupturaDeCadena>();
            var totalRupturas = 0;
            long totalFilas = 0;
            long? primeraSecuencia = null;
            LogAuditoria? ultima = null;

            var firmaEsperada = LogAuditoria.HashGenesis;
            long? despuesDeSecuencia = null;

            while (true)
            {
                var lote = await _bitacora.ListarLoteEnOrdenAsync(despuesDeSecuencia, TamanoLote, cancellationToken);

                foreach (var log in lote)
                {
                    totalFilas++;
                    primeraSecuencia ??= log.Secuencia;

                    if (!string.Equals(log.HashAnterior, firmaEsperada, StringComparison.Ordinal))
                    {
                        Registrar(log, TipoRupturaCadena.EnlaceRoto);
                    }

                    if (!_criptografia.ValidarFirma(log.ObtenerCadenaParaHash(), log.HashFirma))
                    {
                        Registrar(log, TipoRupturaCadena.FirmaInvalida);
                    }

                    // La huella es un hash hexadecimal: se compara sin distinguir
                    // mayúsculas, por si se anotó a mano.
                    if (huellaAnterior is not null
                        && string.Equals(log.HashFirma, huellaAnterior, StringComparison.OrdinalIgnoreCase))
                    {
                        huellaAnteriorEncontrada = true;
                    }

                    firmaEsperada = log.HashFirma;
                    ultima = log;
                }

                if (lote.Count < TamanoLote)
                {
                    break;
                }

                despuesDeSecuencia = lote[^1].Secuencia;
            }

            return ResultadoOperacion<ResultadoVerificacionCadena>.Ok(new ResultadoVerificacionCadena
            {
                TotalFilas = totalFilas,
                PrimeraSecuencia = primeraSecuencia,
                UltimaSecuencia = ultima?.Secuencia,
                FechaUltimaFila = ultima?.FechaEjecucion,
                HuellaActual = ultima?.HashFirma,
                Rupturas = rupturas,
                TotalRupturas = totalRupturas,
                HuellaAnteriorEncontrada = huellaAnteriorEncontrada
            });

            void Registrar(LogAuditoria log, TipoRupturaCadena tipo)
            {
                totalRupturas++;
                if (rupturas.Count < MaximoRupturasListadas)
                {
                    rupturas.Add(new RupturaDeCadena(log.Secuencia, log.Id, tipo, log.FechaEjecucion));
                }
            }
        }
    }
}

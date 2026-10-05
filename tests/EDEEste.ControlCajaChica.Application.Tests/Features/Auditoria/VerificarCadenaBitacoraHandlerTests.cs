using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.Features.Auditoria;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Auditoria
{
    /// <summary>
    /// Cada prueba arma una cadena firmada exactamente como la firma
    /// AuditoriaInterceptor (cada fila sobre la firma de la anterior, la primera sobre
    /// GENESIS) y la "manipula" como lo haría alguien con acceso directo a la base de
    /// datos: editando una fila o quitándola.
    /// </summary>
    public class VerificarCadenaBitacoraHandlerTests
    {
        private static readonly DateTime Inicio = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

        private sealed class Escenario
        {
            public FakeCriptografiaService Criptografia { get; } = new();
            public FakeBitacoraRepository Bitacora { get; } = new();
            public List<LogAuditoria> Cadena { get; } = new();

            private readonly bool _tienePermiso;

            public Escenario(int filas, bool tienePermiso = true)
            {
                _tienePermiso = tienePermiso;

                var anterior = LogAuditoria.HashGenesis;
                for (var i = 0; i < filas; i++)
                {
                    var log = new LogAuditoria
                    {
                        Secuencia = i + 1,
                        UsuarioId = "usuario-prueba",
                        TipoAccion = "Added",
                        NombreTabla = "Gastos",
                        RegistroId = Guid.NewGuid().ToString(),
                        ValoresNuevos = $$"""{"MontoTotal":{{100 + i}}}""",
                        FechaEjecucion = Inicio.AddMinutes(i),
                        HashAnterior = anterior
                    };
                    log.HashFirma = Criptografia.CalcularHMAC(log.ObtenerCadenaParaHash());
                    anterior = log.HashFirma;
                    Cadena.Add(log);
                }

                Bitacora.Agregar(Cadena);
            }

            public async Task<ResultadoVerificacionCadena> VerificarAsync(string? huellaAnterior = null)
            {
                var resultado = await EjecutarAsync(huellaAnterior);
                Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
                return resultado.Valor!;
            }

            public Task<ResultadoOperacion<ResultadoVerificacionCadena>> EjecutarAsync(string? huellaAnterior = null) =>
                new VerificarCadenaBitacoraHandler(Bitacora, Criptografia, new FakeAutorizacionService(_tienePermiso))
                    .EjecutarAsync(new VerificarCadenaBitacoraCommand { HuellaAnterior = huellaAnterior });
        }

        [Fact]
        public async Task CadenaIntacta_NoReportaRupturasYDevuelveLaHuellaDeLaUltimaFila()
        {
            var escenario = new Escenario(filas: 10);

            var resultado = await escenario.VerificarAsync();

            Assert.True(resultado.CadenaIntegra);
            Assert.Empty(resultado.Rupturas);
            Assert.Equal(10, resultado.TotalFilas);
            Assert.Equal(1, resultado.PrimeraSecuencia);
            Assert.Equal(10, resultado.UltimaSecuencia);
            Assert.Equal(escenario.Cadena[^1].HashFirma, resultado.HuellaActual);
            Assert.Null(resultado.HuellaAnteriorEncontrada);
        }

        [Fact]
        public async Task CadenaVacia_EsValidaYNoTieneHuella()
        {
            var escenario = new Escenario(filas: 0);

            var resultado = await escenario.VerificarAsync();

            Assert.True(resultado.CadenaIntegra);
            Assert.Equal(0, resultado.TotalFilas);
            Assert.Null(resultado.HuellaActual);
        }

        [Fact]
        public async Task FilaEditada_DaFirmaInvalidaSoloEnEsaFila()
        {
            var escenario = new Escenario(filas: 10);
            escenario.Cadena[4].ValoresNuevos = """{"MontoTotal":1}""";

            var resultado = await escenario.VerificarAsync();

            // La fila siguiente sigue apuntando al HashFirma guardado de la editada: la
            // edición no se cuenta también como un enlace roto.
            var ruptura = Assert.Single(resultado.Rupturas);
            Assert.Equal(TipoRupturaCadena.FirmaInvalida, ruptura.Tipo);
            Assert.Equal(5, ruptura.Secuencia);
            Assert.False(resultado.CadenaIntegra);
        }

        [Fact]
        public async Task FilaBorradaEnMedio_DaEnlaceRotoEnLaFilaSiguiente()
        {
            var escenario = new Escenario(filas: 10);
            escenario.Bitacora.Quitar(escenario.Cadena[4]);

            var resultado = await escenario.VerificarAsync();

            var ruptura = Assert.Single(resultado.Rupturas);
            Assert.Equal(TipoRupturaCadena.EnlaceRoto, ruptura.Tipo);
            Assert.Equal(6, ruptura.Secuencia);
            Assert.Equal(9, resultado.TotalFilas);
        }

        [Fact]
        public async Task PrincipioBorrado_LaPrimeraFilaQueQuedaNoApuntaAGenesis()
        {
            var escenario = new Escenario(filas: 10);
            escenario.Bitacora.Quitar(escenario.Cadena[0]);
            escenario.Bitacora.Quitar(escenario.Cadena[1]);

            var resultado = await escenario.VerificarAsync();

            var ruptura = Assert.Single(resultado.Rupturas);
            Assert.Equal(TipoRupturaCadena.EnlaceRoto, ruptura.Tipo);
            Assert.Equal(3, ruptura.Secuencia);
        }

        [Fact]
        public async Task CadenaMasLargaQueUnLote_NoReportaRupturasFalsasEntreLotes()
        {
            var filas = VerificarCadenaBitacoraHandler.TamanoLote * 2 + 7;
            var escenario = new Escenario(filas);

            var resultado = await escenario.VerificarAsync();

            Assert.True(resultado.CadenaIntegra);
            Assert.Equal(filas, resultado.TotalFilas);
        }

        [Fact]
        public async Task MuchasRupturas_SeListanHastaElMaximoPeroSeCuentanTodas()
        {
            var editadas = VerificarCadenaBitacoraHandler.MaximoRupturasListadas + 5;
            var escenario = new Escenario(filas: editadas);
            foreach (var log in escenario.Cadena)
            {
                log.UsuarioId = "otro";
            }

            var resultado = await escenario.VerificarAsync();

            Assert.Equal(editadas, resultado.TotalRupturas);
            Assert.Equal(VerificarCadenaBitacoraHandler.MaximoRupturasListadas, resultado.Rupturas.Count);
            Assert.True(resultado.RupturasRecortadas);
        }

        [Fact]
        public async Task HuellaAnteriorQueSigueEnLaCadena_SeEncuentraSinDistinguirMayusculas()
        {
            var escenario = new Escenario(filas: 10);
            var huellaAnotada = escenario.Cadena[6].HashFirma.ToLowerInvariant();

            var resultado = await escenario.VerificarAsync($"  {huellaAnotada}  ");

            Assert.True(resultado.HuellaAnteriorEncontrada);
        }

        [Fact]
        public async Task ColaBorrada_LaCadenaSigueValidaPeroLaHuellaAnteriorNoSeEncuentra()
        {
            // Es el límite de la cadena: borrar las últimas filas deja una cadena válida.
            // Solo la huella anotada antes lo delata.
            var escenario = new Escenario(filas: 10);
            var huellaAnotada = escenario.Cadena[^1].HashFirma;
            escenario.Bitacora.Quitar(escenario.Cadena[^1]);
            escenario.Bitacora.Quitar(escenario.Cadena[^2]);

            var resultado = await escenario.VerificarAsync(huellaAnotada);

            Assert.True(resultado.CadenaIntegra);
            Assert.False(resultado.HuellaAnteriorEncontrada);
        }

        [Fact]
        public async Task SinPermiso_Falla()
        {
            var escenario = new Escenario(filas: 3, tienePermiso: false);

            var resultado = await escenario.EjecutarAsync();

            Assert.False(resultado.Exitoso);
        }
    }
}

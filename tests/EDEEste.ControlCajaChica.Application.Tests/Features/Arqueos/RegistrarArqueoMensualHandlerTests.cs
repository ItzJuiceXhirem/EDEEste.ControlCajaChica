using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Arqueos;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Arqueos
{
    public class RegistrarArqueoMensualHandlerTests
    {
        private static (FondoCajaChica fondo, FakeGastoRepository gastos, FakeArqueoRepository arqueos, FakeApplicationDbContext contexto, RegistrarArqueoMensualHandler handler)
            CrearEscenario(decimal balanceActual = 10000m, decimal montoFijo = 10000m)
        {
            var fondo = new FondoCajaChica { MontoFijo = montoFijo, BalanceActual = balanceActual };

            var fondos = new FakeFondoRepository();
            fondos.Agregar(fondo);

            var gastos = new FakeGastoRepository();
            var arqueos = new FakeArqueoRepository();
            var contexto = new FakeApplicationDbContext();
            var usuarioActual = new FakeCurrentUserService();

            var handler = new RegistrarArqueoMensualHandler(fondos, gastos, arqueos, usuarioActual, contexto);

            return (fondo, gastos, arqueos, contexto, handler);
        }

        private static List<RegistrarArqueoMensualCommand.ConteoDenominacion> Conteo(params (decimal valor, int cantidad)[] filas)
        {
            var lista = new List<RegistrarArqueoMensualCommand.ConteoDenominacion>();
            foreach (var (valor, cantidad) in filas)
            {
                lista.Add(new RegistrarArqueoMensualCommand.ConteoDenominacion { ValorDenominacion = valor, Cantidad = cantidad });
            }

            return lista;
        }

        [Fact]
        public async Task ConteoIgualAlBalance_DaCuadrado_YNoTocaElFondo()
        {
            var (fondo, _, arqueos, contexto, handler) = CrearEscenario(balanceActual: 5000m);

            var resultado = await handler.EjecutarAsync(new RegistrarArqueoMensualCommand
            {
                FondoCajaChicaId = fondo.Id,
                Denominaciones = Conteo((2000m, 2), (1000m, 1))
            });

            Assert.True(resultado.Exitoso);
            Assert.Equal(5000m, fondo.BalanceActual);
            Assert.Equal(1, contexto.VecesGuardado);

            var arqueo = Assert.Single(await arqueos.ListarPorFondoAsync(fondo.Id));
            Assert.Equal(ResultadoArqueo.Cuadrado, arqueo.Resultado);
            Assert.Equal(0m, arqueo.Diferencia);
        }

        [Fact]
        public async Task ConteoPorEncimaDelBalance_DaSobrante_YNoTocaElFondo()
        {
            var (fondo, _, arqueos, contexto, handler) = CrearEscenario(balanceActual: 5000m);

            var resultado = await handler.EjecutarAsync(new RegistrarArqueoMensualCommand
            {
                FondoCajaChicaId = fondo.Id,
                Denominaciones = Conteo((2000m, 2), (1000m, 1), (500m, 1))
            });

            Assert.True(resultado.Exitoso);
            Assert.Equal(5000m, fondo.BalanceActual);

            var arqueo = Assert.Single(await arqueos.ListarPorFondoAsync(fondo.Id));
            Assert.Equal(ResultadoArqueo.Sobrante, arqueo.Resultado);
            Assert.Equal(500m, arqueo.Diferencia);
        }

        [Fact]
        public async Task ConteoPorDebajoDelBalance_DaFaltante_YNoTocaElFondo()
        {
            var (fondo, _, arqueos, contexto, handler) = CrearEscenario(balanceActual: 5000m);

            var resultado = await handler.EjecutarAsync(new RegistrarArqueoMensualCommand
            {
                FondoCajaChicaId = fondo.Id,
                Denominaciones = Conteo((2000m, 2))
            });

            Assert.True(resultado.Exitoso);
            Assert.Equal(5000m, fondo.BalanceActual);

            var arqueo = Assert.Single(await arqueos.ListarPorFondoAsync(fondo.Id));
            Assert.Equal(ResultadoArqueo.Faltante, arqueo.Resultado);
            Assert.Equal(-1000m, arqueo.Diferencia);
        }

        [Fact]
        public async Task DenominacionInexistente_Falla()
        {
            var (fondo, _, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new RegistrarArqueoMensualCommand
            {
                FondoCajaChicaId = fondo.Id,
                Denominaciones = Conteo((300m, 1))
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task DenominacionRepetida_Falla()
        {
            var (fondo, _, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new RegistrarArqueoMensualCommand
            {
                FondoCajaChicaId = fondo.Id,
                Denominaciones = Conteo((500m, 1), (500m, 2))
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task CantidadNegativaYDenominacionInexistente_AcumulaLosDosErroresEnLaMismaRespuesta()
        {
            var (fondo, _, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new RegistrarArqueoMensualCommand
            {
                FondoCajaChicaId = fondo.Id,
                Denominaciones = Conteo((300m, -1), (2000m, 1))
            });

            Assert.False(resultado.Exitoso);
            Assert.True(resultado.Errores.Count >= 2);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task ConteoEnCero_EsUnResultadoLegitimo()
        {
            var (fondo, _, arqueos, contexto, handler) = CrearEscenario(balanceActual: 0m);

            var resultado = await handler.EjecutarAsync(new RegistrarArqueoMensualCommand
            {
                FondoCajaChicaId = fondo.Id,
                Denominaciones = Conteo((2000m, 0), (1000m, 0))
            });

            Assert.True(resultado.Exitoso);
            var arqueo = Assert.Single(await arqueos.ListarPorFondoAsync(fondo.Id));
            Assert.Equal(ResultadoArqueo.Cuadrado, arqueo.Resultado);
            Assert.Empty(arqueo.DetallesDenominacion);
        }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests
{
    /// <summary>
    /// Recorre el ciclo completo del dinero: registrar gasto -> solicitar reposicion
    /// -> aprobar -> pagar. Es la invariante del README ("efectivo restante + lo
    /// gastado = fondo fijo") expresada como una sola asercion ejecutable al final,
    /// y la prueba que mas vale del lote porque encadena los cuatro handlers tal
    /// como los encadena un usuario real.
    /// </summary>
    public class CicloCompletoTests
    {
        [Fact]
        public async Task RegistrarSolicitarAprobarYPagar_DejaElBalanceIgualAlMontoFijo()
        {
            var fondo = new FondoCajaChica
            {
                MontoFijo = 10000m,
                BalanceActual = 10000m,
                // 100% para que este escenario no choque con el tope por gasto: lo
                // que se quiere probar aqui es el ciclo completo del dinero, no las
                // reglas de RegistrarGastoHandler (esas ya tienen su propia cobertura).
                PorcentajeMaximoPorGasto = 100m
            };

            var categoria = new CategoriaGasto { Nombre = "Combustible", RequiereNCF = false, Activo = true };

            var fondos = new FakeFondoRepository();
            fondos.Agregar(fondo);

            var categorias = new FakeCategoriaGastoRepository();
            categorias.Agregar(categoria);

            var gastos = new FakeGastoRepository();
            var reposiciones = new FakeReposicionRepository();
            var contexto = new FakeApplicationDbContext();
            var usuarioActual = new FakeCurrentUserService();

            var autorizacion = new FakeAutorizacionService();
            var identidad = new FakeIdentityService();

            var registrarGasto = new RegistrarGastoHandler(
                fondos, categorias, gastos, new FakeFileStorageService(), usuarioActual, identidad, autorizacion, contexto);

            var crearSolicitud = new CrearSolicitudReposicionHandler(
                fondos, gastos, reposiciones, new FakePdfConsolidadorService(), new FakeFileStorageService(), usuarioActual, identidad, autorizacion, contexto);

            var aprobar = new AprobarReposicionHandler(reposiciones, usuarioActual, autorizacion, contexto);
            var pagar = new ProcesarPagoReposicionHandler(reposiciones, usuarioActual, autorizacion, contexto);

            // 1. Registrar un gasto que deja el fondo por debajo del 30% (umbral de
            //    alerta por defecto), para que la reposicion se pueda solicitar.
            var resultadoGasto = await registrarGasto.EjecutarAsync(new RegistrarGastoCommand
            {
                FondoCajaChicaId = fondo.Id,
                CategoriaGastoId = categoria.Id,
                Proveedor = "Proveedor de Prueba SRL",
                RNCProveedor = "123456789",
                Subtotal = 7500m,
                MontoITBIS = 0m,
                MontoTotal = 7500m,
                FechaGasto = DateTime.Today,
                Comprobantes =
                [
                    new RegistrarGastoCommand.ComprobanteEntrada
                    {
                        NombreOriginal = "factura.pdf",
                        TipoMime = "application/pdf",
                        TamanoBytes = 3,
                        Descripcion = "Factura de prueba",
                        Contenido = new MemoryStream("%PDF-1.4"u8.ToArray())
                    }
                ]
            });

            Assert.True(resultadoGasto.Exitoso, string.Join("; ", resultadoGasto.Errores));
            Assert.Equal(2500m, fondo.BalanceActual);

            // 2. Solicitar la reposicion de ese gasto.
            var resultadoSolicitud = await crearSolicitud.EjecutarAsync(new CrearSolicitudReposicionCommand
            {
                FondoCajaChicaId = fondo.Id
            });

            Assert.True(resultadoSolicitud.Exitoso, string.Join("; ", resultadoSolicitud.Errores));
            Assert.Equal(2500m, fondo.BalanceActual); // solicitar no mueve dinero

            var solicitudId = resultadoSolicitud.Valor;

            // 3. Aprobarla.
            var resultadoAprobacion = await aprobar.EjecutarAsync(new AprobarReposicionCommand
            {
                ReposicionId = solicitudId,
                Aprobar = true
            });

            Assert.True(resultadoAprobacion.Exitoso, string.Join("; ", resultadoAprobacion.Errores));
            Assert.Equal(2500m, fondo.BalanceActual); // aprobar tampoco mueve dinero

            // 4. Pagarla: aqui es donde el efectivo vuelve al fondo.
            var resultadoPago = await pagar.EjecutarAsync(new ProcesarPagoReposicionCommand
            {
                ReposicionId = solicitudId,
                ReferenciaPago = "TRF-CICLO-COMPLETO"
            });

            Assert.True(resultadoPago.Exitoso, string.Join("; ", resultadoPago.Errores));

            // La invariante del README: al cerrar el ciclo sin gastos pendientes, el
            // balance vuelve exactamente al fondo fijo.
            Assert.Equal(fondo.MontoFijo, fondo.BalanceActual);
        }
    }
}

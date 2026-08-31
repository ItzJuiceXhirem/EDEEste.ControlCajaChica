using System;
using System.IO;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Gastos
{
    /// <summary>
    /// Cubre puntualmente las validaciones de RNC/Cedula y NCF: que se rechacen
    /// caracteres invalidos en vez de descartarlos en silencio, y que el NCF exija el
    /// formato exacto de la DGII.
    /// </summary>
    public class RegistrarGastoHandlerTests
    {
        private static (FondoCajaChica fondo, CategoriaGasto categoria, FakeApplicationDbContext contexto, RegistrarGastoHandler handler)
            CrearEscenario()
        {
            var fondo = new FondoCajaChica { MontoFijo = 10000m, BalanceActual = 10000m, PorcentajeMaximoPorGasto = 100m };
            var categoria = new CategoriaGasto { Nombre = "Combustible", RequiereNCF = false, Activo = true };

            var fondos = new FakeFondoRepository();
            fondos.Agregar(fondo);

            var categorias = new FakeCategoriaGastoRepository();
            categorias.Agregar(categoria);

            var contexto = new FakeApplicationDbContext();

            var handler = new RegistrarGastoHandler(
                fondos, categorias, new FakeGastoRepository(), new FakeFileStorageService(),
                new FakeCurrentUserService(), new FakeIdentityService(), new FakeAutorizacionService(), contexto);

            return (fondo, categoria, contexto, handler);
        }

        private static RegistrarGastoCommand ComandoBase(FondoCajaChica fondo, CategoriaGasto categoria) => new()
        {
            FondoCajaChicaId = fondo.Id,
            CategoriaGastoId = categoria.Id,
            Proveedor = "Proveedor de Prueba",
            RNCProveedor = "123456789",
            NCF = string.Empty,
            Subtotal = 100m,
            MontoITBIS = 0m,
            MontoTotal = 100m,
            FechaGasto = DateTime.Today,
            Comprobantes =
            [
                new RegistrarGastoCommand.ComprobanteEntrada
                {
                    NombreOriginal = "factura.pdf",
                    TipoMime = "application/pdf",
                    TamanoBytes = 3,
                    Descripcion = "Factura de prueba",
                    // Firma real de PDF: sin esto, la comprobacion de magic bytes lo
                    // rechaza antes de llegar a las validaciones que este archivo prueba.
                    Contenido = new MemoryStream("%PDF-1.4"u8.ToArray())
                }
            ]
        };

        [Fact]
        public async Task Rnc_ConLetras_Falla()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.RNCProveedor = "12A456789";

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("solo puede contener numeros"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rnc_SoloDigitosConLongitudValida_Pasa()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.RNCProveedor = "12345678901"; // 11 digitos: cedula

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rnc_ConGuionesDeCedula_Pasa()
        {
            // Los guiones son formato que agrega la pantalla (XXX-XXXXXXX-X), no algo
            // que el usuario escriba a mano; el handler los acepta y los descarta.
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.RNCProveedor = "123-4567890-1";

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ConPrefijoInvalido_Falla()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.NCF = "A0100000001"; // no empieza con B ni E

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("El NCF no es valido"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ConLongitudInvalida_Falla()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.NCF = "B010000000"; // 10 caracteres, deberia ser 11

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ValidoTipoPapel_Pasa()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.NCF = "B0100000001"; // 11 caracteres

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ValidoTipoElectronico_Pasa()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.NCF = "E312345678901"; // 13 caracteres

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Itbis_IgualAlSubtotal_Falla()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.Subtotal = 100m;
            comando.MontoITBIS = 100m;
            comando.MontoTotal = 200m;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("El ITBIS debe ser menor que el subtotal"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Itbis_MayorQueElSubtotal_Falla()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.Subtotal = 100m;
            comando.MontoITBIS = 150m;
            comando.MontoTotal = 250m;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("El ITBIS debe ser menor que el subtotal"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Comprobante_ConContenidoQueNoCoincideConTipoDeclarado_Falla()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            // Declara ser un PDF, pero el contenido real no trae la firma "%PDF": basta
            // con renombrar un archivo para pasar el filtro de extension/MIME, asi que
            // la firma es la unica comprobacion que un simple renombrado no evade.
            comando.Comprobantes[0].Contenido = new MemoryStream([1, 2, 3]);

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("no coincide con su tipo declarado"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Itbis_MenorQueElSubtotal_Pasa()
        {
            var (fondo, categoria, contexto, handler) = CrearEscenario();
            var comando = ComandoBase(fondo, categoria);
            comando.Subtotal = 100m;
            comando.MontoITBIS = 18m;
            comando.MontoTotal = 118m;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }
    }
}

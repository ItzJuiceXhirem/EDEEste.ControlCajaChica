using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IReporteGastosService
    {
        Task<byte[]> GenerarReporteCierreCajaPdfAsync(IEnumerable<Gasto> gastos, decimal balanceFinal);
    }
}

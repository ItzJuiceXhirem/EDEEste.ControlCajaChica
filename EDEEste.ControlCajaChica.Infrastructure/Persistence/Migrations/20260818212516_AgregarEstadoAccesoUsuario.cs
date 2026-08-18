using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEstadoAccesoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Activo nunca se leyo en ningun lado: lo sustituye EstadoAcceso, que si
            // decide quien puede entrar.
            migrationBuilder.DropColumn(
                name: "Activo",
                table: "AspNetUsers");

            // 1 = EstadoAccesoUsuario.Pendiente. El scaffold generaba 0, que no
            // corresponde a ningun miembro del enum y habria dejado a las cuentas ya
            // existentes en un estado invalido. Pendiente ademas es lo correcto por
            // seguridad: toda cuenta anterior a este cambio pasa por la revision de
            // un Administrador antes de poder entrar.
            migrationBuilder.AddColumn<int>(
                name: "EstadoAcceso",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoAcceso",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                // true era el valor por defecto de la entidad antes de este cambio.
                defaultValue: true);
        }
    }
}

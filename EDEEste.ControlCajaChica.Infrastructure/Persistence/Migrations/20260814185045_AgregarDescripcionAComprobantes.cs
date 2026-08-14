using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDescripcionAComprobantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoArqueo",
                table: "Arqueos");

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "Comprobantes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "Comprobantes");

            migrationBuilder.AddColumn<string>(
                name: "CodigoArqueo",
                table: "Arqueos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

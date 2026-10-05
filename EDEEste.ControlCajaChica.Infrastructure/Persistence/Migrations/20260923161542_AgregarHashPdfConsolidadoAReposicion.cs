using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHashPdfConsolidadoAReposicion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HashPdfConsolidado",
                table: "Reposiciones",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HashPdfConsolidado",
                table: "Reposiciones");
        }
    }
}

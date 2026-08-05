using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PatrimonioOpcionalEmPerifericos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_AssetTag",
                table: "Dispositivos");

            migrationBuilder.AlterColumn<string>(
                name: "AssetTag",
                table: "Dispositivos",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            // Periférico deixa de ter patrimônio: limpa o que já estava cadastrado (Tipo 2 = Periferico).
            migrationBuilder.Sql("UPDATE [Dispositivos] SET [AssetTag] = NULL WHERE [Tipo] = 2;");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_AssetTag",
                table: "Dispositivos",
                column: "AssetTag",
                unique: true,
                filter: "[AssetTag] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_AssetTag",
                table: "Dispositivos");

            migrationBuilder.AlterColumn<string>(
                name: "AssetTag",
                table: "Dispositivos",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_AssetTag",
                table: "Dispositivos",
                column: "AssetTag",
                unique: true);
        }
    }
}

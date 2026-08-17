using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtualizacaoOtaAgenteAndroid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgenteVersaoCode",
                table: "Dispositivos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PacotesAgenteAndroid",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoCode = table.Column<int>(type: "int", nullable: false),
                    VersaoNome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NomeArquivoOriginal = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    CaminhoRelativo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    HashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    EnviadoPorAdmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacotesAgenteAndroid", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PacotesAgenteAndroid_VersaoCode",
                table: "PacotesAgenteAndroid",
                column: "VersaoCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PacotesAgenteAndroid");

            migrationBuilder.DropColumn(
                name: "AgenteVersaoCode",
                table: "Dispositivos");
        }
    }
}

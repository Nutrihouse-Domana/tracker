using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AcessosEAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true — o EF gera 'false' por padrão, mas o C# usa Ativo = true. Sem esta
            // correção, TODO login já existente nasceria DESATIVADO no backfill (trancaria a TI fora).
            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "LogsAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdmNome = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Acao = table.Column<int>(type: "int", nullable: false),
                    EntidadeTipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EntidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Detalhes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnderecoIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogsAuditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_AdmId",
                table: "LogsAuditoria",
                column: "AdmId");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_CriadoEm",
                table: "LogsAuditoria",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_EntidadeId",
                table: "LogsAuditoria",
                column: "EntidadeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogsAuditoria");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "AspNetUsers");
        }
    }
}

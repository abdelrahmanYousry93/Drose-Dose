using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dose.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5RetentionAndKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FileDeletedAtUtc",
                table: "PaymentProofs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "FileDeletedAtUtc",
                table: "PaymentProofs");
        }
    }
}

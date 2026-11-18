using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dose.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingJoinCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "Bookings",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SessionId_JoinCode",
                table: "Bookings",
                columns: new[] { "SessionId", "JoinCode" },
                unique: true,
                filter: "[JoinCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_SessionId_JoinCode",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "Bookings");
        }
    }
}

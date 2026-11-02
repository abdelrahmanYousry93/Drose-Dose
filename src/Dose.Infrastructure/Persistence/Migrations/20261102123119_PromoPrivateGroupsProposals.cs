using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dose.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromoPrivateGroupsProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Sessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedByTeacherId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "Sessions",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupSize",
                table: "SessionRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultDurationMinutes",
                table: "ServiceTypes",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Bookings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Bookings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Bookings",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                table: "Bookings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoCodeId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatCount",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SponsorBookingId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxTotalUses = table.Column<int>(type: "int", nullable: true),
                    MaxUsesPerPhone = table.Column<int>(type: "int", nullable: true),
                    MaxUsesPerSession = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Id);
                    table.CheckConstraint("CK_Promo_Value", "[Value] > 0 AND ([Kind] <> 0 OR [Value] <= 100)");
                });

            migrationBuilder.CreateTable(
                name: "TeacherSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherSubjects_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_InviteCode",
                table: "Bookings",
                column: "InviteCode",
                unique: true,
                filter: "[InviteCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PromoCodeId",
                table: "Bookings",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SponsorBookingId",
                table: "Bookings",
                column: "SponsorBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjects_SubjectId",
                table: "TeacherSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjects_TeacherId_SubjectId",
                table: "TeacherSubjects",
                columns: new[] { "TeacherId", "SubjectId" },
                unique: true);
        
            // Existing data: list price = what was quoted; standard lengths per service type (admins can change them).
            migrationBuilder.Sql("UPDATE [Bookings] SET [ListPrice] = [QuotedPrice] WHERE [ListPrice] = 0;");
            migrationBuilder.Sql("UPDATE [ServiceTypes] SET [DefaultDurationMinutes] = 90 WHERE [Code] = 'chapter_package';");
            migrationBuilder.Sql("UPDATE [ServiceTypes] SET [DefaultDurationMinutes] = 120 WHERE [Code] = 'general_revision';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromoCodes");

            migrationBuilder.DropTable(
                name: "TeacherSubjects");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_InviteCode",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PromoCodeId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_SponsorBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProposedByTeacherId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "GroupSize",
                table: "SessionRequests");

            migrationBuilder.DropColumn(
                name: "DefaultDurationMinutes",
                table: "ServiceTypes");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ListPrice",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PromoCodeId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SeatCount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SponsorBookingId",
                table: "Bookings");
        }
    }
}

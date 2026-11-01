using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dose.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3GroupDecisionAndRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DecisionConfirmedCount",
                table: "Sessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TierDecidedAtUtc",
                table: "Sessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdjustmentId",
                table: "PaymentProofs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Bookings",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpResolvedAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancialAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StudentChoice = table.Column<int>(type: "int", nullable: true),
                    ChoiceAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondByUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAdjustments", x => x.Id);
                    table.CheckConstraint("CK_Adjustment_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactPhoneE164 = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GuardianName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuardianPhoneE164 = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyTrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreferredDays = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreferredTimes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    PreferredTierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FulfilledBySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionRequests_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionRequests_ServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionRequests_Sessions_FulfilledBySessionId",
                        column: x => x.FulfilledBySessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionRequests_StudyTracks_StudyTrackId",
                        column: x => x.StudyTrackId,
                        principalTable: "StudyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionRequests_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_AdjustmentId",
                table: "PaymentProofs",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_BookingId_Type_Status",
                table: "FinancialAdjustments",
                columns: new[] { "BookingId", "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_Type_Status",
                table: "FinancialAdjustments",
                columns: new[] { "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_ContactPhoneE164",
                table: "SessionRequests",
                column: "ContactPhoneE164");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_FulfilledBySessionId",
                table: "SessionRequests",
                column: "FulfilledBySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_GradeId",
                table: "SessionRequests",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_Reference",
                table: "SessionRequests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_ServiceTypeId",
                table: "SessionRequests",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_Status_CreatedAtUtc",
                table: "SessionRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_StudyTrackId",
                table: "SessionRequests",
                column: "StudyTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_SubjectId",
                table: "SessionRequests",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentProofs_FinancialAdjustments_AdjustmentId",
                table: "PaymentProofs",
                column: "AdjustmentId",
                principalTable: "FinancialAdjustments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentProofs_FinancialAdjustments_AdjustmentId",
                table: "PaymentProofs");

            migrationBuilder.DropTable(
                name: "FinancialAdjustments");

            migrationBuilder.DropTable(
                name: "SessionRequests");

            migrationBuilder.DropIndex(
                name: "IX_PaymentProofs_AdjustmentId",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "DecisionConfirmedCount",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TierDecidedAtUtc",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "AdjustmentId",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FollowUpResolvedAtUtc",
                table: "Bookings");
        }
    }
}

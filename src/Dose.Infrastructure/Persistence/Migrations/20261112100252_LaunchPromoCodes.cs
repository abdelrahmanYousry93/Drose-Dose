using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dose.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Launch offer: 15% off the first booking, one code per channel so bookings can be traced to it
    /// (FB15 Facebook, IG15 Instagram, TT15 TikTok, WA15 WhatsApp). One use per WhatsApp number, no end date.
    /// Inserted only if missing, so codes already created or edited in the admin panel are left alone.
    /// </summary>
    public partial class LaunchPromoCodes : Migration
    {
        static readonly (string Code, string Channel)[] Codes =
            [("FB15", "فيسبوك"), ("IG15", "إنستجرام"), ("TT15", "تيك توك"), ("WA15", "واتساب")];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (code, channel) in Codes)
                migrationBuilder.Sql($"""
                    IF NOT EXISTS (SELECT 1 FROM [PromoCodes] WHERE [Code] = N'{code}')
                    INSERT INTO [PromoCodes] ([Id], [Code], [Description], [Kind], [Value], [MaxUsesPerPhone], [IsActive], [CreatedBy], [CreatedAtUtc])
                    VALUES (NEWID(), N'{code}', N'عرض الإطلاق: خصم 15% على أول حجز — {channel}', 0, 15, 1, 1, N'marketing-launch', SYSUTCDATETIME());
                    """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Codes may already have been used by bookings; deactivate instead of deleting.
            migrationBuilder.Sql("UPDATE [PromoCodes] SET [IsActive] = 0 WHERE [CreatedBy] = N'marketing-launch';");
        }
    }
}

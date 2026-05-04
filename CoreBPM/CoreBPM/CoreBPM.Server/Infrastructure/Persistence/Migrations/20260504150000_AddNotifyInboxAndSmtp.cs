using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations;

/// <summary>Миграция: таблицы in-app уведомлений и настроек SMTP (FR-MSG-02.1, FR-ADM-02.1).</summary>
public partial class AddNotifyInboxAndSmtp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── notify_inbox ────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "notify_inbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: true),
                Link = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notify_inbox", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_notify_inbox_UserId_IsRead",
            table: "notify_inbox",
            columns: new[] { "UserId", "IsRead" });

        migrationBuilder.CreateIndex(
            name: "IX_notify_inbox_CreatedAt",
            table: "notify_inbox",
            column: "CreatedAt");

        // ─── admin_smtp_settings ─────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "admin_smtp_settings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                Port = table.Column<int>(type: "integer", nullable: false, defaultValue: 587),
                UseSsl = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                Username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                FromAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "Core BPM"),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_smtp_settings", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "notify_inbox");
        migrationBuilder.DropTable(name: "admin_smtp_settings");
    }
}

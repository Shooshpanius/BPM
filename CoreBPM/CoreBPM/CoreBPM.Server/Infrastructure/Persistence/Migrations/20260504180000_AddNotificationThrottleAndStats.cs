using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationThrottleAndStats : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── Настройки ограничения частоты уведомлений (throttle) ────────────
        migrationBuilder.CreateTable(
            name: "notify_throttle_settings",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                user_id = t.Column<Guid>(nullable: false),
                event_type = t.Column<string>(maxLength: 100, nullable: false),
                channel = t.Column<int>(nullable: false),
                min_interval_minutes = t.Column<int>(nullable: false, defaultValue: 0),
                updated_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_notify_throttle_settings", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_notify_throttle_settings_user_event_channel",
            table: "notify_throttle_settings",
            columns: ["user_id", "event_type", "channel"],
            unique: true);

        // ─── Журнал последних отправок для throttle ───────────────────────────
        migrationBuilder.CreateTable(
            name: "notify_throttle_log",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                user_id = t.Column<Guid>(nullable: false),
                event_type = t.Column<string>(maxLength: 100, nullable: false),
                channel = t.Column<int>(nullable: false),
                last_sent_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_notify_throttle_log", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_notify_throttle_log_user_event_channel",
            table: "notify_throttle_log",
            columns: ["user_id", "event_type", "channel"],
            unique: true);

        // ─── Настройки хранения журнала доставки (retention) ─────────────────
        migrationBuilder.CreateTable(
            name: "admin_notification_log_settings",
            columns: t => new
            {
                id = t.Column<int>(nullable: false),
                retention_days = t.Column<int>(nullable: false, defaultValue: 90),
                updated_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_admin_notification_log_settings", x => x.id));

        // Вставляем singleton-запись с умолчаниями
        migrationBuilder.InsertData(
            table: "admin_notification_log_settings",
            columns: ["id", "retention_days", "updated_at"],
            columnTypes: ["integer", "integer", "timestamp with time zone"],
            values: [1, 90, DateTimeOffset.UtcNow]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "notify_throttle_settings");
        migrationBuilder.DropTable(name: "notify_throttle_log");
        migrationBuilder.DropTable(name: "admin_notification_log_settings");
    }
}

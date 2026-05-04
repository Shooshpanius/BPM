using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationSettingsV2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── Расширение матрицы каналов (SMS + Push) ─────────────────────────
        migrationBuilder.AddColumn<bool>(
            name: "sms",
            table: "user_task_notification_settings",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "push",
            table: "user_task_notification_settings",
            nullable: false,
            defaultValue: false);

        // ─── Настройки режима «Не беспокоить» ────────────────────────────────
        migrationBuilder.CreateTable(
            name: "notify_dnd_settings",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                user_id = t.Column<Guid>(nullable: false),
                is_enabled = t.Column<bool>(nullable: false, defaultValue: false),
                start_hour = t.Column<int>(nullable: false, defaultValue: 22),
                end_hour = t.Column<int>(nullable: false, defaultValue: 8),
                disabled_days = t.Column<string>(maxLength: 20, nullable: false, defaultValue: ""),
                time_zone = t.Column<string>(maxLength: 100, nullable: false, defaultValue: "UTC"),
                apply_to_push = t.Column<bool>(nullable: false, defaultValue: true),
                apply_to_sms = t.Column<bool>(nullable: false, defaultValue: true),
                updated_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_notify_dnd_settings", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_notify_dnd_settings_user_id",
            table: "notify_dnd_settings",
            column: "user_id",
            unique: true);

        // ─── Журнал доставки уведомлений ──────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "notify_delivery_log",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                user_id = t.Column<Guid>(nullable: false),
                event_type = t.Column<string>(maxLength: 100, nullable: false),
                channel = t.Column<int>(nullable: false),
                status = t.Column<int>(nullable: false),
                error = t.Column<string>(maxLength: 2000, nullable: true),
                created_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_notify_delivery_log", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_notify_delivery_log_user_id_created_at",
            table: "notify_delivery_log",
            columns: ["user_id", "created_at"]);

        migrationBuilder.CreateIndex(
            name: "ix_notify_delivery_log_event_type_channel_status",
            table: "notify_delivery_log",
            columns: ["event_type", "channel", "status"]);

        // ─── Глобальные шаблоны уведомлений ──────────────────────────────────
        migrationBuilder.CreateTable(
            name: "admin_notification_templates",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                event_type = t.Column<string>(maxLength: 100, nullable: false),
                event_label = t.Column<string>(maxLength: 200, nullable: false),
                email_subject_template = t.Column<string>(maxLength: 500, nullable: false),
                email_body_template = t.Column<string>(nullable: false),
                short_template = t.Column<string>(maxLength: 500, nullable: false),
                is_mandatory_in_app = t.Column<bool>(nullable: false, defaultValue: false),
                is_mandatory_email = t.Column<bool>(nullable: false, defaultValue: false),
                is_mandatory_sms = t.Column<bool>(nullable: false, defaultValue: false),
                is_mandatory_push = t.Column<bool>(nullable: false, defaultValue: false),
                is_active = t.Column<bool>(nullable: false, defaultValue: true),
                created_at = t.Column<DateTimeOffset>(nullable: false),
                updated_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_admin_notification_templates", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_admin_notification_templates_event_type",
            table: "admin_notification_templates",
            column: "event_type",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "admin_notification_templates");
        migrationBuilder.DropTable(name: "notify_delivery_log");
        migrationBuilder.DropTable(name: "notify_dnd_settings");

        migrationBuilder.DropColumn(name: "push", table: "user_task_notification_settings");
        migrationBuilder.DropColumn(name: "sms", table: "user_task_notification_settings");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations;

/// <summary>Миграция: rich email шаблоны, actionable-токены, SMS, Web Push (FR-MSG-02.1).</summary>
public partial class AddEmailTemplatesSmsAndPush : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── Rich email templates ─────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "admin_email_templates",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                event_type = t.Column<string>(maxLength: 100, nullable: false),
                subject = t.Column<string>(maxLength: 500, nullable: false),
                html_template = t.Column<string>(nullable: false),
                is_active = t.Column<bool>(nullable: false, defaultValue: true),
                created_at = t.Column<DateTimeOffset>(nullable: false),
                updated_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_admin_email_templates", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_admin_email_templates_event_type",
            table: "admin_email_templates",
            column: "event_type",
            unique: true);

        // ─── Actionable email tokens ──────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "notify_action_tokens",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                user_id = t.Column<Guid>(nullable: false),
                event_type = t.Column<string>(maxLength: 100, nullable: false),
                action_type = t.Column<string>(maxLength: 50, nullable: false),
                token = t.Column<Guid>(nullable: false),
                entity_id = t.Column<Guid>(nullable: true),
                expires_at = t.Column<DateTimeOffset>(nullable: false),
                used_at = t.Column<DateTimeOffset>(nullable: true),
                created_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_notify_action_tokens", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_notify_action_tokens_token",
            table: "notify_action_tokens",
            column: "token",
            unique: true);

        // ─── SMS settings ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "admin_sms_settings",
            columns: t => new
            {
                id = t.Column<int>(nullable: false),
                provider_url = t.Column<string>(maxLength: 500, nullable: true),
                api_key = t.Column<string>(maxLength: 500, nullable: true),
                from_number = t.Column<string>(maxLength: 50, nullable: true),
                is_enabled = t.Column<bool>(nullable: false),
                phone_param_name = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "to"),
                message_param_name = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "msg"),
                api_key_param_name = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "api_id"),
            },
            constraints: t => t.PrimaryKey("pk_admin_sms_settings", x => x.id));

        // ─── SMS log ──────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "notify_sms_log",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                user_id = t.Column<Guid>(nullable: true),
                phone_number = t.Column<string>(maxLength: 50, nullable: false),
                event_type = t.Column<string>(maxLength: 100, nullable: false),
                message = t.Column<string>(maxLength: 1000, nullable: false),
                status = t.Column<string>(maxLength: 20, nullable: false),
                provider_response = t.Column<string>(maxLength: 1000, nullable: true),
                error_message = t.Column<string>(maxLength: 1000, nullable: true),
                sent_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_notify_sms_log", x => x.id));

        // ─── Push subscriptions ───────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "notify_push_subscriptions",
            columns: t => new
            {
                id = t.Column<Guid>(nullable: false),
                user_id = t.Column<Guid>(nullable: false),
                endpoint = t.Column<string>(maxLength: 2000, nullable: false),
                p256_dh = t.Column<string>(maxLength: 500, nullable: false),
                auth = t.Column<string>(maxLength: 200, nullable: false),
                user_agent = t.Column<string>(maxLength: 500, nullable: true),
                created_at = t.Column<DateTimeOffset>(nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_notify_push_subscriptions", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_notify_push_subscriptions_user_id_endpoint",
            table: "notify_push_subscriptions",
            columns: ["user_id", "endpoint"],
            unique: true);

        // ─── VAPID settings ───────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "admin_vapid_settings",
            columns: t => new
            {
                id = t.Column<int>(nullable: false),
                public_key = t.Column<string>(maxLength: 200, nullable: true),
                private_key = t.Column<string>(maxLength: 200, nullable: true),
                subject = t.Column<string>(maxLength: 200, nullable: false),
            },
            constraints: t => t.PrimaryKey("pk_admin_vapid_settings", x => x.id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("admin_email_templates");
        migrationBuilder.DropTable("notify_action_tokens");
        migrationBuilder.DropTable("admin_sms_settings");
        migrationBuilder.DropTable("notify_sms_log");
        migrationBuilder.DropTable("notify_push_subscriptions");
        migrationBuilder.DropTable("admin_vapid_settings");
    }
}

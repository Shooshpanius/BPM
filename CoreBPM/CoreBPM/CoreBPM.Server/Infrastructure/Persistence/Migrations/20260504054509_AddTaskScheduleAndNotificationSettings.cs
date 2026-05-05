using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskScheduleAndNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Добавляем колонку scheduled_at к существующей таблице task_items
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_at",
                table: "task_items",
                type: "timestamp with time zone",
                nullable: true);

            // Новая таблица настроек уведомлений пользователя (впервые создаётся здесь)
            migrationBuilder.CreateTable(
                name: "user_task_notification_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    in_app = table.Column<bool>(type: "boolean", nullable: false),
                    email = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_task_notification_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_task_notification_settings_user_id_event_type",
                table: "user_task_notification_settings",
                columns: new[] { "user_id", "event_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_task_notification_settings");

            migrationBuilder.DropColumn(
                name: "scheduled_at",
                table: "task_items");
        }
    }
}

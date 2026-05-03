using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskControlSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_control_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    default_control_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_effort_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_activity_type_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_control_settings", x => x.id);
                });

            // Seed default row
            migrationBuilder.InsertData(
                table: "task_control_settings",
                columns: new[] { "id", "default_control_type", "is_effort_required", "is_activity_type_required", "updated_at" },
                values: new object[] { 1, 0, false, false, DateTimeOffset.UtcNow });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "task_control_settings");
        }
    }
}

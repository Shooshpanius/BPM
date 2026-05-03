using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskKindAndRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Добавляем поле kind в task_items (FR-TASK-01.5)
            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "task_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Добавляем поле document_id в task_items (для задачи по резолюции, FR-TASK-01.5.3)
            migrationBuilder.AddColumn<Guid>(
                name: "document_id",
                table: "task_items",
                type: "uuid",
                nullable: true);

            // Добавляем поле series_id в task_items (для периодических задач, FR-TASK-01.5.1)
            migrationBuilder.AddColumn<Guid>(
                name: "series_id",
                table: "task_items",
                type: "uuid",
                nullable: true);

            // Таблица конфигурации серий периодических задач (FR-TASK-01.5.1)
            migrationBuilder.CreateTable(
                name: "task_recurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    root_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodicity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    end_condition = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    look_ahead_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 480),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_recurrences", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_recurrences_task_items_root_task_id",
                        column: x => x.root_task_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_task_recurrences_root_task_id",
                table: "task_recurrences",
                column: "root_task_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_items_kind",
                table: "task_items",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "ix_task_items_series_id",
                table: "task_items",
                column: "series_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "task_recurrences");
            migrationBuilder.DropIndex(name: "ix_task_items_kind", table: "task_items");
            migrationBuilder.DropIndex(name: "ix_task_items_series_id", table: "task_items");
            migrationBuilder.DropColumn(name: "kind", table: "task_items");
            migrationBuilder.DropColumn(name: "document_id", table: "task_items");
            migrationBuilder.DropColumn(name: "series_id", table: "task_items");
        }
    }
}

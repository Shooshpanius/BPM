using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmExecutionJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "bpm_scheduler_jobs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "bpm_scheduler_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "bpm_scheduler_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "duration_ms",
                table: "bpm_instance_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "element_id",
                table: "bpm_instance_history",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "element_name",
                table: "bpm_instance_history",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bpm_execution_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    process_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    element_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    server_host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_timer = table.Column<bool>(type: "boolean", nullable: false),
                    timer_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_execution_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_execution_jobs_bpm_instances_instance_id",
                        column: x => x.instance_id,
                        principalTable: "bpm_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bpm_execution_jobs_bpm_process_versions_process_version_id",
                        column: x => x.process_version_id,
                        principalTable: "bpm_process_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpm_execution_jobs_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instance_history_element_id_occurred_at",
                table: "bpm_instance_history",
                columns: new[] { "element_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_execution_jobs_instance_id",
                table: "bpm_execution_jobs",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_execution_jobs_process_id",
                table: "bpm_execution_jobs",
                column: "process_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_execution_jobs_process_version_id",
                table: "bpm_execution_jobs",
                column: "process_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_execution_jobs_status",
                table: "bpm_execution_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_execution_jobs_status_next_run_at",
                table: "bpm_execution_jobs",
                columns: new[] { "status", "next_run_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_execution_jobs");

            migrationBuilder.DropIndex(
                name: "ix_bpm_instance_history_element_id_occurred_at",
                table: "bpm_instance_history");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "bpm_scheduler_jobs");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "bpm_scheduler_jobs");

            migrationBuilder.DropColumn(
                name: "status",
                table: "bpm_scheduler_jobs");

            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "bpm_instance_history");

            migrationBuilder.DropColumn(
                name: "element_id",
                table: "bpm_instance_history");

            migrationBuilder.DropColumn(
                name: "element_name",
                table: "bpm_instance_history");
        }
    }
}

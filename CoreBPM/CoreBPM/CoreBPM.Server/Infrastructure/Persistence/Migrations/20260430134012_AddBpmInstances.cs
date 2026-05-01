using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    launch_source = table.Column<int>(type: "integer", nullable: false),
                    initiator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsible_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_instances", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_instances_bpm_process_versions_process_version_id",
                        column: x => x.process_version_id,
                        principalTable: "bpm_process_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpm_instances_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bpm_scheduler_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    timer_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    timer_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_fired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_fire_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_scheduler_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_scheduler_jobs_bpm_process_versions_process_version_id",
                        column: x => x.process_version_id,
                        principalTable: "bpm_process_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpm_scheduler_jobs_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bpm_instance_variables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_variable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value_json = table.Column<string>(type: "text", nullable: true),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_instance_variables", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_instance_variables_bpm_instances_instance_id",
                        column: x => x.instance_id,
                        principalTable: "bpm_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instance_variables_instance_id",
                table: "bpm_instance_variables",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instances_initiator_user_id",
                table: "bpm_instances",
                column: "initiator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instances_process_id",
                table: "bpm_instances",
                column: "process_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instances_process_id_started_at",
                table: "bpm_instances",
                columns: new[] { "process_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instances_process_version_id",
                table: "bpm_instances",
                column: "process_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instances_state",
                table: "bpm_instances",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_scheduler_jobs_is_active_next_fire_at",
                table: "bpm_scheduler_jobs",
                columns: new[] { "is_active", "next_fire_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_scheduler_jobs_process_id",
                table: "bpm_scheduler_jobs",
                column: "process_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_scheduler_jobs_process_version_id",
                table: "bpm_scheduler_jobs",
                column: "process_version_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_instance_variables");

            migrationBuilder.DropTable(
                name: "bpm_scheduler_jobs");

            migrationBuilder.DropTable(
                name: "bpm_instances");
        }
    }
}

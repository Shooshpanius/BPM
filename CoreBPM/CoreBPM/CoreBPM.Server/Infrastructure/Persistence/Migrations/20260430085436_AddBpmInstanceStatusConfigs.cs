using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmInstanceStatusConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_instance_status_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_variable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    on_interrupt_action = table.Column<int>(type: "integer", nullable: false),
                    on_interrupt_script_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_instance_status_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_instance_status_configs_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bpm_instance_status_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_instance_status_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_instance_status_options_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instance_status_configs_process_id",
                table: "bpm_instance_status_configs",
                column: "process_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instance_status_options_process_id_code",
                table: "bpm_instance_status_options",
                columns: new[] { "process_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_instance_status_configs");

            migrationBuilder.DropTable(
                name: "bpm_instance_status_options");
        }
    }
}

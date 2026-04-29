using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmElementConfigsVariablesRaci : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_element_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_element_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_element_configs_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bpm_process_variables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    variable_type = table.Column<int>(type: "integer", nullable: false),
                    default_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_key_variable = table.Column<bool>(type: "boolean", nullable: false),
                    is_input = table.Column<bool>(type: "boolean", nullable: false),
                    is_output = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_process_variables", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_process_variables_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bpm_raci_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    role = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    raci_type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_raci_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_raci_entries_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_element_configs_process_id_element_id",
                table: "bpm_element_configs",
                columns: new[] { "process_id", "element_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bpm_process_variables_process_id_name",
                table: "bpm_process_variables",
                columns: new[] { "process_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bpm_raci_entries_process_id",
                table: "bpm_raci_entries",
                column: "process_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_element_configs");

            migrationBuilder.DropTable(
                name: "bpm_process_variables");

            migrationBuilder.DropTable(
                name: "bpm_raci_entries");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmProcessRoleConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_process_role_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_type = table.Column<int>(type: "integer", nullable: false),
                    assignee_type = table.Column<int>(type: "integer", nullable: false),
                    assignee_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_process_role_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_process_role_configs_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_process_role_configs_process_id",
                table: "bpm_process_role_configs",
                column: "process_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_process_role_configs");
        }
    }
}

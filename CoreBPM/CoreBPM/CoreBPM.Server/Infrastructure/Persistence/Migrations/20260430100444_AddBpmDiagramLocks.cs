using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmDiagramLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_diagram_locks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locked_by_display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_diagram_locks", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_diagram_locks_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_diagram_locks_locked_until",
                table: "bpm_diagram_locks",
                column: "locked_until");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_diagram_locks_process_id",
                table: "bpm_diagram_locks",
                column: "process_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_diagram_locks");
        }
    }
}

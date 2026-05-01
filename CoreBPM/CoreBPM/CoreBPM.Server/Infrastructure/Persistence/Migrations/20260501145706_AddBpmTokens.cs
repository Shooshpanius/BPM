using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    element_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    element_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    signal_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    message_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correlation_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_tokens_bpm_instances_instance_id",
                        column: x => x.instance_id,
                        principalTable: "bpm_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_tokens_instance_id",
                table: "bpm_tokens",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_tokens_status_message_code",
                table: "bpm_tokens",
                columns: new[] { "status", "message_code" });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_tokens_status_signal_code",
                table: "bpm_tokens",
                columns: new[] { "status", "signal_code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_tokens");
        }
    }
}

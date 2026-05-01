using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmInstanceManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_instance_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    meta_json = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_instance_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_instance_history_bpm_instances_instance_id",
                        column: x => x.instance_id,
                        principalTable: "bpm_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bpm_instance_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    added_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_instance_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_instance_participants_bpm_instances_instance_id",
                        column: x => x.instance_id,
                        principalTable: "bpm_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instance_history_instance_id",
                table: "bpm_instance_history",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instance_history_instance_id_occurred_at",
                table: "bpm_instance_history",
                columns: new[] { "instance_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_instance_participants_instance_id_user_id",
                table: "bpm_instance_participants",
                columns: new[] { "instance_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_instance_history");

            migrationBuilder.DropTable(
                name: "bpm_instance_participants");
        }
    }
}

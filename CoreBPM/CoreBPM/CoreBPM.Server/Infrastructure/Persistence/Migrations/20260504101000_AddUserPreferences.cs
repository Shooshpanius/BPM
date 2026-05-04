using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "org_user_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "ru"),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "system"),
                    date_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    page_size = table.Column<int>(type: "integer", nullable: false, defaultValue: 25),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_user_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_user_preferences_org_users_user_id",
                        column: x => x.user_id,
                        principalTable: "org_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_org_user_preferences_user_id",
                table: "org_user_preferences",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_user_preferences");
        }
    }
}

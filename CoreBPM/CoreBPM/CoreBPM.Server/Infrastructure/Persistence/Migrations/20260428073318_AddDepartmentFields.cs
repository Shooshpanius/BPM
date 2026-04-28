using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_org_departments_organization_id",
                table: "org_departments");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "org_departments");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "org_departments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "path",
                table: "org_departments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "short_name",
                table: "org_departments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "org_departments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "org_department_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    change_type = table.Column<int>(type: "integer", nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_department_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_department_history_org_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "org_departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_org_department_history_org_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "org_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_org_departments_organization_id_code",
                table: "org_departments",
                columns: new[] { "organization_id", "code" },
                unique: true,
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_org_department_history_changed_by_user_id",
                table: "org_department_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_department_history_department_id",
                table: "org_department_history",
                column: "department_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_department_history");

            migrationBuilder.DropIndex(
                name: "ix_org_departments_organization_id_code",
                table: "org_departments");

            migrationBuilder.DropColumn(
                name: "code",
                table: "org_departments");

            migrationBuilder.DropColumn(
                name: "path",
                table: "org_departments");

            migrationBuilder.DropColumn(
                name: "short_name",
                table: "org_departments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "org_departments");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "org_departments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_org_departments_organization_id",
                table: "org_departments",
                column: "organization_id");
        }
    }
}

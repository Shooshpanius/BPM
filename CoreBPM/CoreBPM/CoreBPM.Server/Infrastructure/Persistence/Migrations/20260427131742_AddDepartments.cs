using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "org_employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "org_departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_departments", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_departments_org_departments_parent_id",
                        column: x => x.parent_id,
                        principalTable: "org_departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_departments_org_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "org_organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_org_employees_department_id",
                table: "org_employees",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_departments_organization_id",
                table: "org_departments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_departments_parent_id",
                table: "org_departments",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "fk_org_employees_org_departments_department_id",
                table: "org_employees",
                column: "department_id",
                principalTable: "org_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_employees_org_departments_department_id",
                table: "org_employees");

            migrationBuilder.DropTable(
                name: "org_departments");

            migrationBuilder.DropIndex(
                name: "ix_org_employees_department_id",
                table: "org_employees");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "org_employees");
        }
    }
}

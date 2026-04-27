using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsAndEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "org_organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_employees", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_employees_org_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "org_organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_employees_org_users_user_id",
                        column: x => x.user_id,
                        principalTable: "org_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_org_employees_organization_id",
                table: "org_employees",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_employees_user_id_organization_id",
                table: "org_employees",
                columns: new[] { "user_id", "organization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_organizations_is_primary",
                table: "org_organizations",
                column: "is_primary",
                unique: true,
                filter: "is_primary = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_employees");

            migrationBuilder.DropTable(
                name: "org_organizations");
        }
    }
}

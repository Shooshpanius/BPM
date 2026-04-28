using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions");

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "org_positions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_org_positions_department_id",
                table: "org_positions",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_positions_organization_id_code",
                table: "org_positions",
                columns: new[] { "organization_id", "code" },
                unique: true,
                filter: "code IS NOT NULL AND is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "fk_org_positions_org_organizations_organization_id",
                table: "org_positions",
                column: "organization_id",
                principalTable: "org_organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_positions_org_organizations_organization_id",
                table: "org_positions");

            migrationBuilder.DropIndex(
                name: "ix_org_positions_department_id",
                table: "org_positions");

            migrationBuilder.DropIndex(
                name: "ix_org_positions_organization_id_code",
                table: "org_positions");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "org_positions");

            migrationBuilder.CreateIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions",
                columns: new[] { "department_id", "code" },
                unique: true,
                filter: "department_id IS NOT NULL AND code IS NOT NULL AND is_deleted = false");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mobile_phone",
                table: "org_users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_phone",
                table: "org_users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "personal_email",
                table: "org_users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bio",
                table: "org_users",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "birth_date",
                table: "org_users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "birth_date_visibility",
                table: "org_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "all");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mobile_phone",
                table: "org_users");

            migrationBuilder.DropColumn(
                name: "internal_phone",
                table: "org_users");

            migrationBuilder.DropColumn(
                name: "personal_email",
                table: "org_users");

            migrationBuilder.DropColumn(
                name: "bio",
                table: "org_users");

            migrationBuilder.DropColumn(
                name: "birth_date",
                table: "org_users");

            migrationBuilder.DropColumn(
                name: "birth_date_visibility",
                table: "org_users");
        }
    }
}

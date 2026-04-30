using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmProcessSettingsAndVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "data_class_name",
                table: "bpm_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "data_table_name",
                table: "bpm_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "external_start_allowed_ips",
                table: "bpm_processes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "external_start_enabled",
                table: "bpm_processes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "external_start_methods_json",
                table: "bpm_processes",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "external_start_token_hash",
                table: "bpm_processes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_start_token_preview",
                table: "bpm_processes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "external_start_token_updated_at",
                table: "bpm_processes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instance_metrics_class_name",
                table: "bpm_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "instance_metrics_table_name",
                table: "bpm_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "instance_name_mode",
                table: "bpm_processes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "instance_name_template",
                table: "bpm_processes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "launch_from_portal_enabled",
                table: "bpm_processes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "process_metrics_class_name",
                table: "bpm_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "process_metrics_table_name",
                table: "bpm_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "request_instance_name_on_start",
                table: "bpm_processes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "second_runtime_enabled",
                table: "bpm_processes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "second_runtime_upgraded_at",
                table: "bpm_processes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_in_start_list",
                table: "bpm_processes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at",
                table: "bpm_process_versions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_class_name",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "data_table_name",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "external_start_allowed_ips",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "external_start_enabled",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "external_start_methods_json",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "external_start_token_hash",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "external_start_token_preview",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "external_start_token_updated_at",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "instance_metrics_class_name",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "instance_metrics_table_name",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "instance_name_mode",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "instance_name_template",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "launch_from_portal_enabled",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "process_metrics_class_name",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "process_metrics_table_name",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "request_instance_name_on_start",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "second_runtime_enabled",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "second_runtime_upgraded_at",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "show_in_start_list",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "bpm_process_versions");
        }
    }
}

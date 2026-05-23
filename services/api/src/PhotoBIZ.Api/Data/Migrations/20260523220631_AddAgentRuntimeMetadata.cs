using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRuntimeMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "agent_api_reachable",
                table: "booths",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "agent_chrome_launched",
                table: "booths",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_health_status",
                table: "booths",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "UNKNOWN");

            migrationBuilder.AddColumn<bool>(
                name: "agent_kiosk_running",
                table: "booths",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_luma_booth_mode",
                table: "booths",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "agent_luma_booth_reachable",
                table: "booths",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "agent_metadata_updated_at",
                table: "booths",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_runtime_kind",
                table: "booths",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "agent_trigger_listener_running",
                table: "booths",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_version",
                table: "booths",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agent_api_reachable",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_chrome_launched",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_health_status",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_kiosk_running",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_luma_booth_mode",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_luma_booth_reachable",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_metadata_updated_at",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_runtime_kind",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_trigger_listener_running",
                table: "booths");

            migrationBuilder.DropColumn(
                name: "agent_version",
                table: "booths");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoothUiTerminalAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "terminal_notice_acknowledged_at",
                table: "transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completion_thank_you_message",
                table: "booth_appearance_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Thanks for sharing your smile.");

            migrationBuilder.Sql(
                """
                UPDATE booth_appearance_configs
                SET completion_thank_you_message = 'Thanks for sharing your smile.'
                WHERE completion_thank_you_message = '';

                UPDATE transactions
                SET terminal_notice_acknowledged_at = now()
                WHERE status IN ('EXPIRED', 'CANCELLED', 'PAYMENT_FAILED');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "terminal_notice_acknowledged_at",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "completion_thank_you_message",
                table: "booth_appearance_configs");
        }
    }
}

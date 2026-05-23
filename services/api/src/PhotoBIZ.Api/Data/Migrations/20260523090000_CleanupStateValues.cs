using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PhotoBizDbContext))]
    [Migration("20260523090000_CleanupStateValues")]
    public partial class CleanupStateValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE client_subscriptions
                SET status = 'SUSPENDED'
                WHERE status = 'PAST_DUE';
                """);

            migrationBuilder.DropIndex(
                name: "ix_print_entitlements_client_account_id_status",
                table: "print_entitlements");

            migrationBuilder.DropColumn(
                name: "status",
                table: "print_entitlements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "print_entitlements",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.CreateIndex(
                name: "ix_print_entitlements_client_account_id_status",
                table: "print_entitlements",
                columns: new[] { "client_account_id", "status" });
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayMongoQrPhPaymentResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encrypted_webhook_secret",
                table: "client_payment_provider_configs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_mode",
                table: "client_payment_provider_configs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encrypted_webhook_secret",
                table: "client_payment_provider_configs");

            migrationBuilder.DropColumn(
                name: "payment_mode",
                table: "client_payment_provider_configs");
        }
    }
}

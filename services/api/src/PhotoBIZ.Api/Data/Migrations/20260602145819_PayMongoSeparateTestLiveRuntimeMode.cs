using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayMongoSeparateTestLiveRuntimeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_configs_client_provider_type",
                table: "client_payment_provider_configs");

            migrationBuilder.AddColumn<Guid>(
                name: "client_payment_provider_config_id",
                table: "payment_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_payment_url",
                table: "payment_attempts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "runtime_active",
                table: "client_payment_provider_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE client_payment_provider_configs
                SET payment_mode = 'test'
                WHERE provider = 'PAYMONGO'
                    AND integration_type = 'PAYMONGO_QRPH'
                    AND payment_mode IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE client_payment_provider_configs
                SET runtime_active = TRUE
                WHERE provider = 'PAYMONGO'
                    AND integration_type = 'PAYMONGO_QRPH'
                    AND status = 'VERIFIED';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_payment_attempts_client_payment_provider_config_id",
                table: "payment_attempts",
                column: "client_payment_provider_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_configs_client_provider_type",
                table: "client_payment_provider_configs",
                columns: new[] { "client_account_id", "provider", "integration_type", "payment_mode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_attempts_client_payment_provider_configs_client_pay~",
                table: "payment_attempts",
                column: "client_payment_provider_config_id",
                principalTable: "client_payment_provider_configs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payment_attempts_client_payment_provider_configs_client_pay~",
                table: "payment_attempts");

            migrationBuilder.DropIndex(
                name: "ix_payment_attempts_client_payment_provider_config_id",
                table: "payment_attempts");

            migrationBuilder.DropIndex(
                name: "ix_payment_configs_client_provider_type",
                table: "client_payment_provider_configs");

            migrationBuilder.DropColumn(
                name: "client_payment_provider_config_id",
                table: "payment_attempts");

            migrationBuilder.DropColumn(
                name: "test_payment_url",
                table: "payment_attempts");

            migrationBuilder.DropColumn(
                name: "runtime_active",
                table: "client_payment_provider_configs");

            migrationBuilder.CreateIndex(
                name: "ix_payment_configs_client_provider_type",
                table: "client_payment_provider_configs",
                columns: new[] { "client_account_id", "provider", "integration_type" },
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCancellationContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_previous_status",
                table: "transactions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_source",
                table: "transactions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_by_actor_type",
                table: "transactions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_user_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE transactions AS t
                SET
                    cancelled_by_actor_type = CASE
                        WHEN t.failure_reason = 'Customer cancelled at the booth.' THEN 'BOOTH_USER'
                        WHEN t.failure_reason IN (
                            'Payment request was cancelled by the cashier.',
                            'Manual booth recovery returned the booth to welcome.'
                        ) THEN 'CASHIER'
                        WHEN t.failure_reason = 'Extra print workflow timed out before completion.' THEN 'SYSTEM'
                        ELSE NULL
                    END,
                    cancelled_by_user_id = (
                        SELECT a.user_id
                        FROM audit_logs AS a
                        WHERE a.entity_type = 'Transaction'
                            AND a.entity_id = t.id
                            AND a.action = 'transaction.cancelled'
                            AND a.user_id IS NOT NULL
                        ORDER BY a.created_at DESC
                        LIMIT 1
                    ),
                    cancellation_source = CASE
                        WHEN t.failure_reason = 'Customer cancelled at the booth.'
                            AND t.terminal_notice_acknowledged_at IS NULL
                            THEN 'BOOTH_UI_WAITING_FOR_PAYMENT_BACK'
                        WHEN t.failure_reason = 'Payment request was cancelled by the cashier.'
                            THEN 'CASHIER_POS_CANCEL_TRANSACTION'
                        WHEN t.failure_reason = 'Manual booth recovery returned the booth to welcome.'
                            THEN 'CASHIER_POS_RETURN_TO_WELCOME'
                        WHEN t.failure_reason = 'Extra print workflow timed out before completion.'
                            THEN 'SYSTEM_EXTRA_PRINT_TIMEOUT'
                        ELSE NULL
                    END,
                    cancellation_previous_status = CASE
                        WHEN t.failure_reason = 'Customer cancelled at the booth.'
                            AND t.terminal_notice_acknowledged_at IS NULL
                            THEN 'PENDING_CASH'
                        WHEN t.failure_reason = 'Payment request was cancelled by the cashier.'
                            THEN 'PENDING_CASH'
                        WHEN t.failure_reason = 'Manual booth recovery returned the booth to welcome.'
                            THEN (
                                SELECT a.metadata ->> 'PreviousStatus'
                                FROM audit_logs AS a
                                WHERE a.entity_type = 'Transaction'
                                    AND a.entity_id = t.id
                                    AND a.action = 'transaction.cancelled'
                                ORDER BY a.created_at DESC
                                LIMIT 1
                            )
                        WHEN t.failure_reason = 'Extra print workflow timed out before completion.'
                            THEN 'STARTING_SESSION'
                        ELSE NULL
                    END
                WHERE t.status = 'CANCELLED';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_cancelled_by_user_id",
                table: "transactions",
                column: "cancelled_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_users_cancelled_by_user_id",
                table: "transactions",
                column: "cancelled_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_users_cancelled_by_user_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_cancelled_by_user_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "cancellation_previous_status",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "cancellation_source",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "cancelled_by_actor_type",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                table: "transactions");
        }
    }
}

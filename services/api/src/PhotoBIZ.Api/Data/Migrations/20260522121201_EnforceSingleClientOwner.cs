using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleClientOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked_owners AS (
                    SELECT
                        id,
                        ROW_NUMBER() OVER (
                            PARTITION BY client_account_id
                            ORDER BY created_at, id
                        ) AS owner_rank
                    FROM users
                    WHERE role = 'CLIENT_OWNER'
                      AND client_account_id IS NOT NULL
                )
                UPDATE users
                SET role = 'CLIENT_ADMIN',
                    can_approve_cash = TRUE,
                    can_return_booth_to_welcome = TRUE,
                    can_cancel_transaction = TRUE
                WHERE id IN (
                    SELECT id
                    FROM ranked_owners
                    WHERE owner_rank > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "ix_users_one_client_owner_per_client",
                table: "users",
                column: "client_account_id",
                unique: true,
                filter: "role = 'CLIENT_OWNER' AND client_account_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_one_client_owner_per_client",
                table: "users");
        }
    }
}

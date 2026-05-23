using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PhotoBizDbContext))]
    [Migration("20260523131500_BackfillDefaultPrintEntitlements")]
    public partial class BackfillDefaultPrintEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO print_entitlements (id, client_account_id, name, created_at)
                SELECT
                    (substring(seed.hash from 1 for 8) || '-' ||
                     substring(seed.hash from 9 for 4) || '-' ||
                     substring(seed.hash from 13 for 4) || '-' ||
                     substring(seed.hash from 17 for 4) || '-' ||
                     substring(seed.hash from 21 for 12))::uuid,
                    seed.client_account_id,
                    seed.name,
                    now()
                FROM (
                    SELECT
                        client_accounts.id AS client_account_id,
                        entitlement.name,
                        md5(client_accounts.id::text || ':' || entitlement.seed || ':backfill') AS hash
                    FROM client_accounts
                    CROSS JOIN (
                        VALUES
                            ('2 pcs 6x2 or 1 pc 6x4', 'combo'),
                            ('2 pcs 6x2', 'two-by-six'),
                            ('1 pc 6x4', 'one-by-four')
                    ) AS entitlement(name, seed)
                ) AS seed
                ON CONFLICT (client_account_id, name) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM print_entitlements
                WHERE name IN ('2 pcs 6x2', '1 pc 6x4')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM booth_offers
                      WHERE booth_offers.client_account_id = print_entitlements.client_account_id
                        AND booth_offers.included_print_entitlement = print_entitlements.name
                  );
                """);
        }
    }
}

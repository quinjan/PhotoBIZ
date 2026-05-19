using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "print_entitlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_entitlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_print_entitlements_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_print_entitlements_client_account_id_name",
                table: "print_entitlements",
                columns: new[] { "client_account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_entitlements_client_account_id_status",
                table: "print_entitlements",
                columns: new[] { "client_account_id", "status" });

            migrationBuilder.Sql(
                """
                INSERT INTO print_entitlements (id, client_account_id, name, status, created_at)
                SELECT
                    (substring(seed.hash from 1 for 8) || '-' ||
                     substring(seed.hash from 9 for 4) || '-' ||
                     substring(seed.hash from 13 for 4) || '-' ||
                     substring(seed.hash from 17 for 4) || '-' ||
                     substring(seed.hash from 21 for 12))::uuid,
                    seed.client_account_id,
                    seed.name,
                    'ACTIVE',
                    now()
                FROM (
                    SELECT
                        client_accounts.id AS client_account_id,
                        entitlement.name,
                        md5(client_accounts.id::text || ':' || entitlement.seed) AS hash
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
            migrationBuilder.DropTable(
                name: "print_entitlements");
        }
    }
}

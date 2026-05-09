using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoBIZ.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMvpDataFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    price_per_booth_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "client_booth_themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    theme_preset = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    primary_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accent_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    background_image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    default_welcome_headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    default_welcome_subtitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_booth_themes", x => x.id);
                    table.ForeignKey(
                        name: "fk_client_booth_themes_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_payment_provider_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    integration_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_account_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    public_key_masked = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    encrypted_secret_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    webhook_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_payment_provider_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_configs_client_account",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_locations_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    print_count = table.Column<int>(type: "integer", nullable: false),
                    paper_size = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    lumabooth_preset_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_packages", x => x.id);
                    table.ForeignKey(
                        name: "fk_packages_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    active_booth_allowance = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_client_subscriptions_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_client_subscriptions_subscription_plans_subscription_plan_id",
                        column: x => x.subscription_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booths",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    current_state = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    last_heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    kiosk_token_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    agent_credential_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booths", x => x.id);
                    table.ForeignKey(
                        name: "fk_booths_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booths_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booth_packages",
                columns: table => new
                {
                    booth_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booth_packages", x => new { x.booth_id, x.package_id });
                    table.ForeignKey(
                        name: "fk_booth_packages_booths_booth_id",
                        column: x => x.booth_id,
                        principalTable: "booths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booth_packages_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booth_terminal_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booth_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    terminal_model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    terminal_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    serial_or_asset_tag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    com_port = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_connection_test_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booth_terminal_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_booth_terminal_configs_booths_booth_id",
                        column: x => x.booth_id,
                        principalTable: "booths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_booth_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    role = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_booths_assigned_booth_id",
                        column: x => x.assigned_booth_id,
                        principalTable: "booths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booth_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    amount_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    package_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_transactions_booths_booth_id",
                        column: x => x.booth_id,
                        principalTable: "booths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_client_accounts_client_account_id",
                        column: x => x.client_account_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booth_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booth_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lumabooth_session_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    welcome_headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    welcome_subtitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    session_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assigned_package_ids = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_booth_sessions_booths_booth_id",
                        column: x => x.booth_id,
                        principalTable: "booths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booth_sessions_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_attempts_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_client_account_id_created_at",
                table: "audit_logs",
                columns: new[] { "client_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_booth_packages_package_id",
                table: "booth_packages",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_booth_sessions_booth_id_status",
                table: "booth_sessions",
                columns: new[] { "booth_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_booth_sessions_transaction_id",
                table: "booth_sessions",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_booth_terminal_configs_booth_id_provider",
                table: "booth_terminal_configs",
                columns: new[] { "booth_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booths_client_account_id_code",
                table: "booths",
                columns: new[] { "client_account_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booths_client_account_id_current_state",
                table: "booths",
                columns: new[] { "client_account_id", "current_state" });

            migrationBuilder.CreateIndex(
                name: "ix_booths_client_account_id_status",
                table: "booths",
                columns: new[] { "client_account_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_booths_location_id",
                table: "booths",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_accounts_name",
                table: "client_accounts",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_client_booth_themes_client_account_id",
                table: "client_booth_themes",
                column: "client_account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_configs_client_provider_type",
                table: "client_payment_provider_configs",
                columns: new[] { "client_account_id", "provider", "integration_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_subscriptions_client_account_id_status",
                table: "client_subscriptions",
                columns: new[] { "client_account_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_client_subscriptions_subscription_plan_id",
                table: "client_subscriptions",
                column: "subscription_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_locations_client_account_id_name",
                table: "locations",
                columns: new[] { "client_account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_packages_client_account_id_name",
                table: "packages",
                columns: new[] { "client_account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_attempts_provider_provider_reference",
                table: "payment_attempts",
                columns: new[] { "provider", "provider_reference" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_attempts_transaction_id",
                table: "payment_attempts",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_name",
                table: "subscription_plans",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_approved_by_user_id",
                table: "transactions",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_booth_id_status",
                table: "transactions",
                columns: new[] { "booth_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_client_account_id_status",
                table: "transactions",
                columns: new[] { "client_account_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_expires_at",
                table: "transactions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_location_id",
                table: "transactions",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_package_id",
                table: "transactions",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_transaction_number",
                table: "transactions",
                column: "transaction_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_assigned_booth_id",
                table: "users",
                column: "assigned_booth_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_client_account_id_role",
                table: "users",
                columns: new[] { "client_account_id", "role" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "booth_packages");

            migrationBuilder.DropTable(
                name: "booth_sessions");

            migrationBuilder.DropTable(
                name: "booth_terminal_configs");

            migrationBuilder.DropTable(
                name: "client_booth_themes");

            migrationBuilder.DropTable(
                name: "client_payment_provider_configs");

            migrationBuilder.DropTable(
                name: "client_subscriptions");

            migrationBuilder.DropTable(
                name: "payment_attempts");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "packages");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "booths");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "client_accounts");
        }
    }
}

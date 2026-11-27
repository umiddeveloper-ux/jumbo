using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_orders",
                columns: table => new
                {
                    payment_order_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    plan = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amount_sum = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plan_before = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    plan_until_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plan_until_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_orders", x => x.payment_order_id);
                    table.ForeignKey(
                        name: "fk_payment_orders_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "payme_transactions",
                columns: table => new
                {
                    payme_transaction_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payme_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    time_ms = table.Column<long>(type: "bigint", nullable: false),
                    amount_tiyin = table.Column<long>(type: "bigint", nullable: false),
                    payment_order_id = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<int>(type: "integer", nullable: true),
                    create_time_ms = table.Column<long>(type: "bigint", nullable: false),
                    perform_time_ms = table.Column<long>(type: "bigint", nullable: false),
                    cancel_time_ms = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payme_transactions", x => x.payme_transaction_id);
                    table.ForeignKey(
                        name: "fk_payme_transactions_payment_orders_payment_order_id",
                        column: x => x.payment_order_id,
                        principalTable: "payment_orders",
                        principalColumn: "payment_order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payme_transactions_payme_id",
                table: "payme_transactions",
                column: "payme_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payme_transactions_payment_order_id",
                table: "payme_transactions",
                column: "payment_order_id",
                unique: true,
                filter: "state IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "ix_payme_transactions_state_create_time_ms",
                table: "payme_transactions",
                columns: new[] { "state", "create_time_ms" });

            migrationBuilder.CreateIndex(
                name: "ix_payme_transactions_time_ms",
                table: "payme_transactions",
                column: "time_ms");

            migrationBuilder.CreateIndex(
                name: "ix_payment_orders_user_id_payment_order_id",
                table: "payment_orders",
                columns: new[] { "user_id", "payment_order_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payme_transactions");

            migrationBuilder.DropTable(
                name: "payment_orders");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacementResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "placement_results",
                columns: table => new
                {
                    placement_result_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    taken_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    stage = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    recommended_book = table.Column<int>(type: "integer", nullable: false),
                    share_a1 = table.Column<double>(type: "double precision", nullable: true),
                    share_a2 = table.Column<double>(type: "double precision", nullable: true),
                    share_b1 = table.Column<double>(type: "double precision", nullable: true),
                    share_b2 = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_results", x => x.placement_result_id);
                    table.ForeignKey(
                        name: "fk_placement_results_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_placement_results_user_id_taken_at",
                table: "placement_results",
                columns: new[] { "user_id", "taken_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "placement_results");
        }
    }
}

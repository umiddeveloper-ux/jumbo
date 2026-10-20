using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesSchoolsStaffRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "role",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "school_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "schools",
                columns: table => new
                {
                    school_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    region = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schools", x => x.school_id);
                });

            migrationBuilder.CreateTable(
                name: "staff_requests",
                columns: table => new
                {
                    staff_request_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    requested_role = table.Column<int>(type: "integer", nullable: false),
                    region = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    school_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    admin_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    school_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_requests", x => x.staff_request_id);
                    table.ForeignKey(
                        name: "fk_staff_requests_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_school_id",
                table: "users",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_schools_region_name",
                table: "schools",
                columns: new[] { "region", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_requests_school_id",
                table: "staff_requests",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_requests_user_id_status",
                table: "staff_requests",
                columns: new[] { "user_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_users_schools_school_id",
                table: "users",
                column: "school_id",
                principalTable: "schools",
                principalColumn: "school_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_schools_school_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "staff_requests");

            migrationBuilder.DropTable(
                name: "schools");

            migrationBuilder.DropIndex(
                name: "ix_users_school_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "school_id",
                table: "users");
        }
    }
}

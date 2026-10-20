using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    language_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    shor_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_languages", x => x.language_id);
                });

            migrationBuilder.CreateTable(
                name: "svg_images",
                columns: table => new
                {
                    svg_image_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    svg = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_svg_images", x => x.svg_image_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<int>(type: "integer", nullable: false),
                    app_language_id = table.Column<int>(type: "integer", nullable: false),
                    study_language_id = table.Column<int>(type: "integer", nullable: false),
                    translation_language_id = table.Column<int>(type: "integer", nullable: false),
                    translation_language_id2 = table.Column<int>(type: "integer", nullable: true),
                    life_count = table.Column<int>(type: "integer", nullable: false),
                    current_book_id = table.Column<int>(type: "integer", nullable: false),
                    life_expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lp_points = table.Column<int>(type: "integer", nullable: false),
                    current_language_level = table.Column<string>(type: "text", nullable: true),
                    hide_tooltips = table.Column<bool>(type: "boolean", nullable: false),
                    app_theme = table.Column<string>(type: "text", nullable: false, defaultValue: "Light"),
                    access_token = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_users_languages_app_language_id",
                        column: x => x.app_language_id,
                        principalTable: "languages",
                        principalColumn: "language_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_languages_study_language_id",
                        column: x => x.study_language_id,
                        principalTable: "languages",
                        principalColumn: "language_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_languages_translation_language_id",
                        column: x => x.translation_language_id,
                        principalTable: "languages",
                        principalColumn: "language_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_languages_translation_language_id2",
                        column: x => x.translation_language_id2,
                        principalTable: "languages",
                        principalColumn: "language_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "awards",
                columns: table => new
                {
                    award_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    language_id = table.Column<int>(type: "integer", nullable: false),
                    award_name = table.Column<string>(type: "text", nullable: false),
                    svg_image_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_awards", x => x.award_id);
                    table.ForeignKey(
                        name: "fk_awards_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "language_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_awards_svg_images_svg_image_id",
                        column: x => x.svg_image_id,
                        principalTable: "svg_images",
                        principalColumn: "svg_image_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "markets",
                columns: table => new
                {
                    market_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    language_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    svg_image_id = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_markets", x => x.market_id);
                    table.ForeignKey(
                        name: "fk_markets_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "language_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_markets_svg_images_svg_image_id",
                        column: x => x.svg_image_id,
                        principalTable: "svg_images",
                        principalColumn: "svg_image_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    subject_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    book_id = table.Column<int>(type: "integer", nullable: false),
                    subject_name = table.Column<string>(type: "text", nullable: false),
                    svg_image_id = table.Column<int>(type: "integer", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    is_passed = table.Column<bool>(type: "boolean", nullable: false),
                    video_url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.subject_id);
                    table.ForeignKey(
                        name: "fk_subjects_svg_images_svg_image_id",
                        column: x => x.svg_image_id,
                        principalTable: "svg_images",
                        principalColumn: "svg_image_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_awards",
                columns: table => new
                {
                    user_awards_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    awards_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_awards", x => x.user_awards_id);
                    table.ForeignKey(
                        name: "fk_user_awards_awards_awards_id",
                        column: x => x.awards_id,
                        principalTable: "awards",
                        principalColumn: "award_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_awards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_markets",
                columns: table => new
                {
                    user_markets_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    market_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_markets", x => x.user_markets_id);
                    table.ForeignKey(
                        name: "fk_user_markets_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "market_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_markets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_subject_progresses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    subject_id = table.Column<int>(type: "integer", nullable: false),
                    find_me_won = table.Column<bool>(type: "boolean", nullable: false),
                    sequence_won = table.Column<bool>(type: "boolean", nullable: false),
                    listen_won = table.Column<bool>(type: "boolean", nullable: false),
                    steps_won = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_subject_progresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_subject_progresses_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "subject_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_subject_progresses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "words",
                columns: table => new
                {
                    word_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subject_id = table.Column<int>(type: "integer", nullable: false),
                    language_id = table.Column<int>(type: "integer", nullable: false),
                    svg_image_id = table.Column<int>(type: "integer", nullable: false),
                    word_name = table.Column<string>(type: "text", nullable: false),
                    first_symbol = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_words", x => x.word_id);
                    table.ForeignKey(
                        name: "fk_words_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "language_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_words_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "subject_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_words_svg_images_svg_image_id",
                        column: x => x.svg_image_id,
                        principalTable: "svg_images",
                        principalColumn: "svg_image_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users_words",
                columns: table => new
                {
                    users_word_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    word_id = table.Column<int>(type: "integer", nullable: false),
                    lesson_count = table.Column<int>(type: "integer", nullable: false),
                    repeating_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users_words", x => x.users_word_id);
                    table.ForeignKey(
                        name: "fk_users_words_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_users_words_words_word_id",
                        column: x => x.word_id,
                        principalTable: "words",
                        principalColumn: "word_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_awards_language_id",
                table: "awards",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "ix_awards_svg_image_id",
                table: "awards",
                column: "svg_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_languages_name",
                table: "languages",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_markets_language_id",
                table: "markets",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "ix_markets_svg_image_id",
                table: "markets",
                column: "svg_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_subjects_svg_image_id",
                table: "subjects",
                column: "svg_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_awards_awards_id",
                table: "user_awards",
                column: "awards_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_awards_user_id_awards_id",
                table: "user_awards",
                columns: new[] { "user_id", "awards_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_markets_market_id",
                table: "user_markets",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_markets_user_id_market_id",
                table: "user_markets",
                columns: new[] { "user_id", "market_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_subject_progresses_subject_id",
                table: "user_subject_progresses",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_subject_progresses_user_id_subject_id",
                table: "user_subject_progresses",
                columns: new[] { "user_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_access_token",
                table: "users",
                column: "access_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_app_language_id",
                table: "users",
                column: "app_language_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_study_language_id",
                table: "users",
                column: "study_language_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_translation_language_id",
                table: "users",
                column: "translation_language_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_translation_language_id2",
                table: "users",
                column: "translation_language_id2");

            migrationBuilder.CreateIndex(
                name: "ix_users_words_user_id_word_id",
                table: "users_words",
                columns: new[] { "user_id", "word_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_words_word_id",
                table: "users_words",
                column: "word_id");

            migrationBuilder.CreateIndex(
                name: "ix_words_first_symbol",
                table: "words",
                column: "first_symbol");

            migrationBuilder.CreateIndex(
                name: "ix_words_language_id",
                table: "words",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "ix_words_subject_id",
                table: "words",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_words_svg_image_id",
                table: "words",
                column: "svg_image_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_awards");

            migrationBuilder.DropTable(
                name: "user_markets");

            migrationBuilder.DropTable(
                name: "user_subject_progresses");

            migrationBuilder.DropTable(
                name: "users_words");

            migrationBuilder.DropTable(
                name: "awards");

            migrationBuilder.DropTable(
                name: "markets");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "words");

            migrationBuilder.DropTable(
                name: "languages");

            migrationBuilder.DropTable(
                name: "subjects");

            migrationBuilder.DropTable(
                name: "svg_images");
        }
    }
}

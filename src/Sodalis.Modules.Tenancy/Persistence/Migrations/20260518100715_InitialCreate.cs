using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sodalis.Modules.Tenancy.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "games",
                schema: "tenancy",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.game_id);
                });

            migrationBuilder.CreateTable(
                name: "game_api_keys",
                schema: "tenancy",
                columns: table => new
                {
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_api_keys", x => x.key_hash);
                    table.ForeignKey(
                        name: "fk_game_api_keys_games_game_id",
                        column: x => x.game_id,
                        principalSchema: "tenancy",
                        principalTable: "games",
                        principalColumn: "game_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_api_keys_active",
                schema: "tenancy",
                table: "game_api_keys",
                column: "key_hash",
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_game_api_keys_game_id",
                schema: "tenancy",
                table: "game_api_keys",
                column: "game_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_api_keys",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "games",
                schema: "tenancy");
        }
    }
}

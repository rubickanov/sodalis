using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sodalis.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationAndResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                schema: "identity",
                columns: table => new
                {
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_verification_tokens", x => x.token_hash);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "identity",
                columns: table => new
                {
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.token_hash);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_player_id_game_id",
                schema: "identity",
                table: "email_verification_tokens",
                columns: new[] { "player_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_player_id_game_id",
                schema: "identity",
                table: "password_reset_tokens",
                columns: new[] { "player_id", "game_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_verification_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "identity");
        }
    }
}

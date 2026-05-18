using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sodalis.Modules.Messaging.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "game_email_brandings",
                schema: "messaging",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    from_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reply_to = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    primary_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    support_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    footer_text = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_email_brandings", x => x.game_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_email_brandings",
                schema: "messaging");
        }
    }
}

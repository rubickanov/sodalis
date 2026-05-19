using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sodalis.Modules.Profile.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompositeProfilePk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_profiles",
                schema: "profile",
                table: "profiles");

            migrationBuilder.DropIndex(
                name: "ix_profiles_game_id",
                schema: "profile",
                table: "profiles");

            migrationBuilder.AddPrimaryKey(
                name: "pk_profiles",
                schema: "profile",
                table: "profiles",
                columns: new[] { "player_id", "game_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_profiles",
                schema: "profile",
                table: "profiles");

            migrationBuilder.AddPrimaryKey(
                name: "pk_profiles",
                schema: "profile",
                table: "profiles",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_profiles_game_id",
                schema: "profile",
                table: "profiles",
                column: "game_id");
        }
    }
}

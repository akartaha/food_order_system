using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace food_order_system1.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueEmail1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carts_UserId_RestaurantId",
                table: "carts");

            migrationBuilder.CreateIndex(
                name: "IX_carts_UserId",
                table: "carts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carts_UserId",
                table: "carts");

            migrationBuilder.CreateIndex(
                name: "IX_carts_UserId_RestaurantId",
                table: "carts",
                columns: new[] { "UserId", "RestaurantId" },
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailLab.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductArchiveAndAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InventoryAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuantityChange = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultingQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActorIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAdjustments", x => x.Id);
                    table.CheckConstraint("CK_InventoryAdjustments_QuantityChange", "QuantityChange <> 0");
                    table.CheckConstraint("CK_InventoryAdjustments_ResultingQuantity", "ResultingQuantity >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryAdjustments_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_ProductId_CreatedAtUtc",
                table: "InventoryAdjustments",
                columns: new[] { "ProductId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryAdjustments");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailLab.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncServerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncServerProperties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncServerProperties", x => x.Id);
                    table.CheckConstraint("CK_SyncServerProperties_Singleton", "Id = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncChangeFeedEntries_ProductId_ServerRevision",
                table: "SyncChangeFeedEntries",
                columns: new[] { "ProductId", "ServerRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncServerProperties_ServerInstanceId",
                table: "SyncServerProperties",
                column: "ServerInstanceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncServerProperties");

            migrationBuilder.DropIndex(
                name: "IX_SyncChangeFeedEntries_ProductId_ServerRevision",
                table: "SyncChangeFeedEntries");
        }
    }
}

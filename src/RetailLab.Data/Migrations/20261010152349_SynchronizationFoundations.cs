using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailLab.Data.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizationFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ServerRevision",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "SyncChangeFeedEntries",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncChangeFeedEntries", x => x.Sequence);
                    table.CheckConstraint("CK_SyncChangeFeedEntries_ServerRevision", "ServerRevision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SyncClientStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PairedServerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastPulledSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    PairedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSyncAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncClientStates", x => x.Id);
                    table.CheckConstraint("CK_SyncClientStates_LastPulledSequence", "LastPulledSequence >= 0");
                    table.CheckConstraint("CK_SyncClientStates_Singleton", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "SyncInboxReceipts",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedResult = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncInboxReceipts", x => x.OperationId);
                });

            migrationBuilder.CreateTable(
                name: "SyncOutboxEntries",
                columns: table => new
                {
                    LocalSequence = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    BaseServerRevision = table.Column<long>(type: "INTEGER", nullable: true),
                    PredecessorOperationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedResult = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOutboxEntries", x => x.LocalSequence);
                    table.CheckConstraint("CK_SyncOutboxEntries_BaseServerRevision", "BaseServerRevision IS NULL OR BaseServerRevision >= 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ServerRevision",
                table: "Products",
                sql: "ServerRevision >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SyncChangeFeedEntries_ProductId",
                table: "SyncChangeFeedEntries",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutboxEntries_OperationId",
                table: "SyncOutboxEntries",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutboxEntries_ProductId",
                table: "SyncOutboxEntries",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutboxEntries_State_LocalSequence",
                table: "SyncOutboxEntries",
                columns: new[] { "State", "LocalSequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncChangeFeedEntries");

            migrationBuilder.DropTable(
                name: "SyncClientStates");

            migrationBuilder.DropTable(
                name: "SyncInboxReceipts");

            migrationBuilder.DropTable(
                name: "SyncOutboxEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ServerRevision",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ServerRevision",
                table: "Products");
        }
    }
}

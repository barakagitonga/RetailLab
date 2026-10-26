using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailLab.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuthenticatedDesktopUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorDisplayName",
                table: "SyncInboxReceipts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceDisplayName",
                table: "SyncInboxReceipts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "SyncInboxReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceRole",
                table: "SyncInboxReceipts",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationHash",
                table: "SyncInboxReceipts",
                type: "TEXT",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ResultServerRevision",
                table: "SyncInboxReceipts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultSnapshot",
                table: "SyncInboxReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SyncDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StaffDisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDevices", x => x.Id);
                    table.CheckConstraint("CK_SyncDevices_Role", "Role = 'staff'");
                    table.CheckConstraint("CK_SyncDevices_Status", "Status IN ('Active', 'Revoked')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncInboxReceipts_DeviceId_ProcessedAtUtc",
                table: "SyncInboxReceipts",
                columns: new[] { "DeviceId", "ProcessedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_SyncInboxReceipts_SyncDevices_DeviceId",
                table: "SyncInboxReceipts",
                column: "DeviceId",
                principalTable: "SyncDevices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SyncInboxReceipts_SyncDevices_DeviceId",
                table: "SyncInboxReceipts");

            migrationBuilder.DropTable(
                name: "SyncDevices");

            migrationBuilder.DropIndex(
                name: "IX_SyncInboxReceipts_DeviceId_ProcessedAtUtc",
                table: "SyncInboxReceipts");

            migrationBuilder.DropColumn(
                name: "ActorDisplayName",
                table: "SyncInboxReceipts");

            migrationBuilder.DropColumn(
                name: "DeviceDisplayName",
                table: "SyncInboxReceipts");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "SyncInboxReceipts");

            migrationBuilder.DropColumn(
                name: "DeviceRole",
                table: "SyncInboxReceipts");

            migrationBuilder.DropColumn(
                name: "OperationHash",
                table: "SyncInboxReceipts");

            migrationBuilder.DropColumn(
                name: "ResultServerRevision",
                table: "SyncInboxReceipts");

            migrationBuilder.DropColumn(
                name: "ResultSnapshot",
                table: "SyncInboxReceipts");
        }
    }
}

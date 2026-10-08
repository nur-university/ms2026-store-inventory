using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxAndSourceUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inboxMessage",
                columns: table => new
                {
                    messageId = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inboxMessage", x => new { x.messageId, x.consumer });
                });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_sourceId_sourceType",
                table: "transaction",
                columns: new[] { "sourceId", "sourceType" },
                unique: true,
                filter: "\"sourceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_transaction_sourceId_sourceType",
                table: "transaction");
        }
    }
}

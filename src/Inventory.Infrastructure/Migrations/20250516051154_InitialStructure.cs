using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "outbox");

            migrationBuilder.CreateTable(
                name: "item",
                columns: table => new
                {
                    itemId = table.Column<Guid>(type: "uuid", nullable: false),
                    itemName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    stock = table.Column<int>(type: "integer", nullable: false),
                    unitaryCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item", x => x.itemId);
                });

            migrationBuilder.CreateTable(
                name: "outboxMessage",
                schema: "outbox",
                columns: table => new
                {
                    outboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed = table.Column<bool>(type: "boolean", nullable: false),
                    processedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outboxMessage", x => x.outboxId);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    userId = table.Column<Guid>(type: "uuid", nullable: false),
                    fullName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.userId);
                });

            migrationBuilder.CreateTable(
                name: "transaction",
                columns: table => new
                {
                    transactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    userCreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    transactionType = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    creationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    totalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction", x => x.transactionId);
                    table.ForeignKey(
                        name: "FK_transaction_user_userCreatorId",
                        column: x => x.userCreatorId,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactionItem",
                columns: table => new
                {
                    transactionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    itemId = table.Column<Guid>(type: "uuid", nullable: false),
                    transactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unitaryCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    subTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactionItem", x => x.transactionItemId);
                    table.ForeignKey(
                        name: "FK_transactionItem_item_itemId",
                        column: x => x.itemId,
                        principalTable: "item",
                        principalColumn: "itemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transactionItem_transaction_transactionId",
                        column: x => x.transactionId,
                        principalTable: "transaction",
                        principalColumn: "transactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_userCreatorId",
                table: "transaction",
                column: "userCreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_transactionItem_itemId",
                table: "transactionItem",
                column: "itemId");

            migrationBuilder.CreateIndex(
                name: "IX_transactionItem_transactionId",
                table: "transactionItem",
                column: "transactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outboxMessage",
                schema: "outbox");

            migrationBuilder.DropTable(
                name: "transactionItem");

            migrationBuilder.DropTable(
                name: "item");

            migrationBuilder.DropTable(
                name: "transaction");

            migrationBuilder.DropTable(
                name: "user");
        }

    }
}

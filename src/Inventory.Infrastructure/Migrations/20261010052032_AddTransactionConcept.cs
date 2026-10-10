using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionConcept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "concept",
                table: "transaction",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "transaction",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejectionReason",
                table: "transaction",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewedBy",
                table: "transaction",
                type: "uuid",
                nullable: true);

            // The reservations of sales notes are sales; every other existing transaction keeps no concept (history).
            migrationBuilder.Sql("UPDATE \"transaction\" SET \"concept\" = 'Sale' WHERE \"sourceType\" = 'orders' AND \"transactionType\" = 'Exit';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concept",
                table: "transaction");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "transaction");

            migrationBuilder.DropColumn(
                name: "rejectionReason",
                table: "transaction");

            migrationBuilder.DropColumn(
                name: "reviewedBy",
                table: "transaction");
        }
    }
}

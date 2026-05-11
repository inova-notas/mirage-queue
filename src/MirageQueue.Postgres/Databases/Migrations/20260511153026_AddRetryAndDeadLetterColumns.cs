using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryAndDeadLetterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_Status_NextRetryAt",
                schema: "mirage_queue",
                table: "OutboundMessage",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_Status_ProcessingStartedAt",
                schema: "mirage_queue",
                table: "OutboundMessage",
                columns: new[] { "Status", "ProcessingStartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_Status_NextRetryAt",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_Status_ProcessingStartedAt",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                schema: "mirage_queue",
                table: "OutboundMessage");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKeysAndDedupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_InboundMessageId",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "mirage_queue",
                table: "InboundMessage",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledInboundMessage_IdempotencyKey",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            // PRODUCTION NOTE: this CREATE UNIQUE INDEX will fail if any duplicate
            // (InboundMessageId, ConsumerEndpoint) rows already exist. Validate before
            // applying:
            //   SELECT "InboundMessageId", "ConsumerEndpoint", COUNT(*)
            //   FROM mirage_queue."OutboundMessage"
            //   GROUP BY 1, 2
            //   HAVING COUNT(*) > 1;
            // Any rows returned must be reconciled (typically keep the most-recent
            // row per pair and delete the rest) before this migration can run.
            // It also replaces the previous non-unique IX_OutboundMessage_InboundMessageId
            // (dropped above) — single-column lookups by InboundMessageId still use
            // this composite since InboundMessageId is the leading column.
            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_InboundMessageId_ConsumerEndpoint",
                schema: "mirage_queue",
                table: "OutboundMessage",
                columns: new[] { "InboundMessageId", "ConsumerEndpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundMessage_IdempotencyKey",
                schema: "mirage_queue",
                table: "InboundMessage",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduledInboundMessage_IdempotencyKey",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_InboundMessageId_ConsumerEndpoint",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_InboundMessage_IdempotencyKey",
                schema: "mirage_queue",
                table: "InboundMessage");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "mirage_queue",
                table: "InboundMessage");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_InboundMessageId",
                schema: "mirage_queue",
                table: "OutboundMessage",
                column: "InboundMessageId");
        }
    }
}

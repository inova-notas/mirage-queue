using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionCleanupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ScheduledInboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage",
                columns: new[] { "Status", "UpdateAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "OutboundMessage",
                columns: new[] { "Status", "UpdateAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "InboundMessage",
                columns: new[] { "Status", "UpdateAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduledInboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_InboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "InboundMessage");
        }
    }
}

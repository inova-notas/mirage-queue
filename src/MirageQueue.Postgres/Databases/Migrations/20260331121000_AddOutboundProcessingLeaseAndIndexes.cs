using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundProcessingLeaseAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<System.Guid>(
                name: "ProcessingToken",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundMessage_Status",
                schema: "mirage_queue",
                table: "InboundMessage",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_InboundMessageId_ConsumerEndpoint",
                schema: "mirage_queue",
                table: "OutboundMessage",
                columns: new[] { "InboundMessageId", "ConsumerEndpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_Status",
                schema: "mirage_queue",
                table: "OutboundMessage",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_Status_CreateAt_Id",
                schema: "mirage_queue",
                table: "OutboundMessage",
                columns: new[] { "Status", "CreateAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "OutboundMessage",
                columns: new[] { "Status", "UpdateAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledInboundMessage_Status_ExecuteAt",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage",
                columns: new[] { "Status", "ExecuteAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboundMessage_Status",
                schema: "mirage_queue",
                table: "InboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_InboundMessageId_ConsumerEndpoint",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_Status",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_Status_CreateAt_Id",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboundMessage_Status_UpdateAt",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledInboundMessage_Status_ExecuteAt",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage");

            migrationBuilder.DropColumn(
                name: "ProcessingToken",
                schema: "mirage_queue",
                table: "OutboundMessage");
        }
    }
}

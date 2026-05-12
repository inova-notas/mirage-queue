using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceContextColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage",
                type: "character varying(55)",
                maxLength: 55,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceState",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "character varying(55)",
                maxLength: 55,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceState",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "mirage_queue",
                table: "InboundMessage",
                type: "character varying(55)",
                maxLength: 55,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceState",
                schema: "mirage_queue",
                table: "InboundMessage",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage");

            migrationBuilder.DropColumn(
                name: "TraceState",
                schema: "mirage_queue",
                table: "ScheduledInboundMessage");

            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropColumn(
                name: "TraceState",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "mirage_queue",
                table: "InboundMessage");

            migrationBuilder.DropColumn(
                name: "TraceState",
                schema: "mirage_queue",
                table: "InboundMessage");
        }
    }
}

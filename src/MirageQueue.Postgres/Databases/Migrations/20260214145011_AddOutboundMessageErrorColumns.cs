using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundMessageErrorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionType",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StackTrace",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropColumn(
                name: "ExceptionType",
                schema: "mirage_queue",
                table: "OutboundMessage");

            migrationBuilder.DropColumn(
                name: "StackTrace",
                schema: "mirage_queue",
                table: "OutboundMessage");
        }
    }
}

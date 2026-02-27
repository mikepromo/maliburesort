using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class Next2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Tables_TableId",
                table: "Players");

            migrationBuilder.RenameColumn(
                name: "TableId",
                table: "Players",
                newName: "CurrentTableId");

            migrationBuilder.RenameIndex(
                name: "IX_Players_TableId",
                table: "Players",
                newName: "IX_Players_CurrentTableId");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActiveAt",
                table: "Players",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Tables_CurrentTableId",
                table: "Players",
                column: "CurrentTableId",
                principalTable: "Tables",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Tables_CurrentTableId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastActiveAt",
                table: "Players");

            migrationBuilder.RenameColumn(
                name: "CurrentTableId",
                table: "Players",
                newName: "TableId");

            migrationBuilder.RenameIndex(
                name: "IX_Players_CurrentTableId",
                table: "Players",
                newName: "IX_Players_TableId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Tables_TableId",
                table: "Players",
                column: "TableId",
                principalTable: "Tables",
                principalColumn: "Id");
        }
    }
}

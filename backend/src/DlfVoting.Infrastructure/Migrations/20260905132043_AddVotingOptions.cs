using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DlfVoting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVotingOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VotingOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotingOptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VotingOptions_Name",
                table: "VotingOptions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VotingOptions");
        }
    }
}

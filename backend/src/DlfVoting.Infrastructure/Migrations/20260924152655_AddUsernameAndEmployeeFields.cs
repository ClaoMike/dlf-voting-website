using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DlfVoting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameAndEmployeeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:case_insensitive", "und-u-ks-level2,und-u-ks-level2,icu,False");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCode",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Electability",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EmploymentDate",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Administrators",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                collation: "case_insensitive");

            // Backfill before the unique indexes: admins keep their email as username; users get their
            // email when it fits the 5-20 limit, otherwise the first 20 hex characters of their id.
            migrationBuilder.Sql("""
                UPDATE "Administrators" SET "Username" = "Email";
                UPDATE "Users" SET "Username" = CASE
                    WHEN length("Email") BETWEEN 5 AND 20 THEN "Email"
                    ELSE left(replace("Id"::text, '-', ''), 20)
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Administrators_Username",
                table: "Administrators",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Administrators_Username",
                table: "Administrators");

            migrationBuilder.DropColumn(
                name: "CompanyCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Electability",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmploymentDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Administrators");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:CollationDefinition:case_insensitive", "und-u-ks-level2,und-u-ks-level2,icu,False");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);
        }
    }
}

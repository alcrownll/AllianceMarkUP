using Microsoft.EntityFrameworkCore.Migrations;

namespace ASI.Basecode.Data.Migrations
{
    public partial class AssignedCourse_Program_To_ProgramId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgramId",
                table: "AssignedCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
INSERT INTO ""Programs"" (""ProgramCode"", ""ProgramName"", ""IsActive"")
SELECT DISTINCT UPPER(TRIM(ac.""Program"")) AS code,
       UPPER(TRIM(ac.""Program"")) AS name,
       TRUE
FROM ""AssignedCourses"" ac
LEFT JOIN ""Programs"" p ON UPPER(TRIM(ac.""Program"")) = UPPER(TRIM(p.""ProgramCode""))
WHERE ac.""Program"" IS NOT NULL AND TRIM(ac.""Program"") <> '' AND p.""ProgramId"" IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE ""AssignedCourses"" ac
SET ""ProgramId"" = p.""ProgramId""
FROM ""Programs"" p
WHERE ac.""Program"" IS NOT NULL
  AND TRIM(ac.""Program"") <> ''
  AND UPPER(TRIM(ac.""Program"")) = UPPER(TRIM(p.""ProgramCode""));
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ""AssignedCourses"" WHERE ""ProgramId"" IS NULL) THEN
        RAISE EXCEPTION 'Cannot set ProgramId NOT NULL: some rows have no matching Program.';
    END IF;
END $$;
");

            migrationBuilder.AlterColumn<int>(
                name: "ProgramId",
                table: "AssignedCourses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignedCourses_ProgramId",
                table: "AssignedCourses",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignedCourses_Programs_ProgramId",
                table: "AssignedCourses",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "ProgramId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Program",
                table: "AssignedCourses");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Program",
                table: "AssignedCourses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE ""AssignedCourses"" ac
SET ""Program"" = p.""ProgramCode""
FROM ""Programs"" p
WHERE ac.""ProgramId"" = p.""ProgramId"";
");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignedCourses_Programs_ProgramId",
                table: "AssignedCourses");

            migrationBuilder.DropIndex(
                name: "IX_AssignedCourses_ProgramId",
                table: "AssignedCourses");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "AssignedCourses");
        }
    }
}

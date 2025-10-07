using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ASI.Basecode.Data.Migrations
{
    public partial class AddCalendarNotif : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // If you truly need to change StudentId/TeacherId value generation, keep these.
            // If these already match DB, they are harmless; if they error, you can remove them.
            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Teachers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Students",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // --- DO NOT add UserId columns if they already exist ---
            // Remove the AddColumn<UserId> blocks that EF scaffolded:
            //   migrationBuilder.AddColumn<int>("UserId", "Teachers", ...);
            //   migrationBuilder.AddColumn<int>("UserId", "Students", ...);
            // Instead, ensure the indexes exist using Postgres IF NOT EXISTS:

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE c.relname = 'IX_Teachers_UserId' AND n.nspname = 'public'
                    ) THEN
                        CREATE UNIQUE INDEX "IX_Teachers_UserId" ON "Teachers" ("UserId");
                    END IF;
                END$$;
            """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE c.relname = 'IX_Students_UserId' AND n.nspname = 'public'
                    ) THEN
                        CREATE UNIQUE INDEX "IX_Students_UserId" ON "Students" ("UserId");
                    END IF;
                END$$;
            """);

            // --- Create new tables ---

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    CalendarEventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),   // nullable FK (global events allowed)
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "timestamp", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp", nullable: false),
                    IsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.CalendarEventId);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull); // 👈 important: nullable FK → SET NULL
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_UserId",
                table: "CalendarEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            // --- Add FKs with your stable names for Students/Teachers (columns already exist) ---

            // First, ensure old, wrong-named FKs are dropped safely (won't error if they don't exist)
            migrationBuilder.Sql("ALTER TABLE \"Students\" DROP CONSTRAINT IF EXISTS \"FK_Students_Users_StudentId\";");
            migrationBuilder.Sql("ALTER TABLE \"Teachers\" DROP CONSTRAINT IF EXISTS \"FK_Teachers_Users_TeacherId\";");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Users_UserId",
                table: "Students",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teachers_Users_UserId",
                table: "Teachers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the new FKs we added
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Users_UserId",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Teachers_Users_UserId",
                table: "Teachers");

            // Drop the new tables
            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "Notifications");

            // Drop indexes we may have created (guarded, in case they already existed before)
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Teachers_UserId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Students_UserId\";");

            // Revert the identity annotations if Up() added them
            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Teachers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Students",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Do NOT drop UserId columns here (they existed before this migration).
            // Also, do NOT try to recreate the old wrong-named FKs.
        }
    }
}

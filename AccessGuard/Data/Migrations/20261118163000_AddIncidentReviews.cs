using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccessGuard.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261118163000_AddIncidentReviews")]
public sealed class AddIncidentReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("IncidentReviews", columns: t => new
        {
            Id = t.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
            AlertId = t.Column<long>(type: "INTEGER", nullable: false),
            Time = t.Column<DateTime>(type: "TEXT", nullable: false),
            Actor = t.Column<string>(type: "TEXT", nullable: false),
            Status = t.Column<string>(type: "TEXT", nullable: false),
            Severity = t.Column<string>(type: "TEXT", nullable: false),
            OwnerId = t.Column<string>(type: "TEXT", nullable: true),
            Owner = t.Column<string>(type: "TEXT", nullable: true),
            Note = t.Column<string>(type: "TEXT", nullable: false)
        }, constraints: c =>
        {
            c.PrimaryKey("PK_IncidentReviews", x => x.Id);
            c.ForeignKey("FK_IncidentReviews_AuditEvents_AlertId", x => x.AlertId, "AuditEvents", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_IncidentReviews_AlertId_Id", "IncidentReviews", new[] { "AlertId", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("IncidentReviews");
}

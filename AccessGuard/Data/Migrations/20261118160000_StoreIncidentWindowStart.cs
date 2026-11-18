using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccessGuard.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261118160000_StoreIncidentWindowStart")]
public sealed class StoreIncidentWindowStart : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<DateTime>(
        name: "IncidentWindowStart", table: "AuditEvents", type: "TEXT", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "IncidentWindowStart", table: "AuditEvents");
}

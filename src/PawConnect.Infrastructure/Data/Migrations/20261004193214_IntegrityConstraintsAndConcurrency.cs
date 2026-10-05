using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrityConstraintsAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HourLogs_AspNetUsers_VolunteerId",
                table: "HourLogs");

            migrationBuilder.DropIndex(
                name: "IX_Donations_ApplicationId",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Animals_BranchId_KennelNumber",
                table: "Animals");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "Animals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "AdoptionApplications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing databases: clean up what can be fixed safely, and stop with a clear message for
            // what needs a person to decide, before the new unique indexes are created. (The whole
            // migration runs in one transaction, so a stop leaves the database exactly as it was.)
            migrationBuilder.Sql(@"
UPDATE ""AnimalPhotos"" p SET ""IsPrimary"" = false
WHERE p.""IsPrimary"" AND EXISTS (
    SELECT 1 FROM ""AnimalPhotos"" q
    WHERE q.""AnimalId"" = p.""AnimalId"" AND q.""IsPrimary""
      AND (q.""UploadedAt"", q.""Id"") < (p.""UploadedAt"", p.""Id""));

DO $$
DECLARE clash text;
BEGIN
    SELECT string_agg('kennel ' || k, ', ') INTO clash FROM (
        SELECT ""KennelNumber"" AS k FROM ""Animals"" WHERE ""Status"" IN ('Available', 'Pending')
        GROUP BY ""BranchId"", ""KennelNumber"" HAVING count(*) > 1) d;
    IF clash IS NOT NULL THEN
        RAISE EXCEPTION 'PawConnect upgrade stopped: more than one animal in care is assigned to %. Give each a different kennel number (Admin > Animals > Edit), then restart.', clash;
    END IF;

    SELECT string_agg(a::text, ', ') INTO clash FROM (
        SELECT ""ApplicationId"" AS a FROM ""Donations"" WHERE ""Type"" = 'AdoptionFee'
        GROUP BY ""ApplicationId"" HAVING count(*) > 1) d;
    IF clash IS NOT NULL THEN
        RAISE EXCEPTION 'PawConnect upgrade stopped: an adoption fee was recorded twice for application(s) %. Cancel the duplicate donation row, then restart.', clash;
    END IF;
END $$;");

            // Give existing rows a real stamp instead of the all-zero default.
            migrationBuilder.Sql("UPDATE \"Animals\" SET \"ConcurrencyStamp\" = gen_random_uuid();");
            migrationBuilder.Sql("UPDATE \"AdoptionApplications\" SET \"ConcurrencyStamp\" = gen_random_uuid();");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shifts_Capacity",
                table: "Shifts",
                sql: "\"Capacity\" BETWEEN 1 AND 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shifts_EndsAfterStart",
                table: "Shifts",
                sql: "\"EndsAt\" > \"StartsAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicalRecords_NextDue",
                table: "MedicalRecords",
                sql: "\"NextDueDate\" IS NULL OR \"NextDueDate\" >= \"RecordDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicalRecords_Type",
                table: "MedicalRecords",
                sql: "\"RecordType\" IN ('Vaccination', 'Treatment', 'Checkup', 'Sterilisation', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HourLogs_Hours",
                table: "HourLogs",
                sql: "\"Hours\" > 0 AND \"Hours\" <= 24");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HourLogs_Status",
                table: "HourLogs",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ApplicationId_AdoptionFee",
                table: "Donations",
                column: "ApplicationId",
                unique: true,
                filter: "\"Type\" = 'AdoptionFee'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donations_AdoptionFeeHasApplication",
                table: "Donations",
                sql: "\"Type\" <> 'AdoptionFee' OR \"ApplicationId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donations_Amount",
                table: "Donations",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donations_Frequency",
                table: "Donations",
                sql: "\"Frequency\" IN ('OnceOff', 'Monthly')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donations_Status",
                table: "Donations",
                sql: "\"Status\" IN ('Pledged', 'Received', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donations_Type",
                table: "Donations",
                sql: "\"Type\" IN ('General', 'Sponsorship', 'AdoptionFee')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationStatusChanges_ToStatus",
                table: "ApplicationStatusChanges",
                sql: "\"ToStatus\" IN ('Submitted', 'UnderReview', 'Approved', 'Rejected', 'Adopted', 'Withdrawn')");

            migrationBuilder.CreateIndex(
                name: "IX_Animals_BranchId_KennelNumber_InCare",
                table: "Animals",
                columns: new[] { "BranchId", "KennelNumber" },
                unique: true,
                filter: "\"Status\" IN ('Available', 'Pending')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Animals_AgeMonths",
                table: "Animals",
                sql: "\"AgeMonthsAtIntake\" BETWEEN 0 AND 360");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Animals_KennelNumber",
                table: "Animals",
                sql: "\"KennelNumber\" BETWEEN 1 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Animals_Size",
                table: "Animals",
                sql: "\"Size\" IN ('Small', 'Medium', 'Large')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Animals_Species",
                table: "Animals",
                sql: "\"Species\" IN ('Dog', 'Cat', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Animals_Status",
                table: "Animals",
                sql: "\"Status\" IN ('Available', 'Pending', 'Adopted', 'Fostered')");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPhotos_AnimalId_Primary",
                table: "AnimalPhotos",
                column: "AnimalId",
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnimalPhotos_Size",
                table: "AnimalPhotos",
                sql: "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 2097152");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdoptionApplications_Status",
                table: "AdoptionApplications",
                sql: "\"Status\" IN ('Submitted', 'UnderReview', 'Approved', 'Rejected', 'Adopted', 'Withdrawn')");

            migrationBuilder.AddForeignKey(
                name: "FK_HourLogs_AspNetUsers_VolunteerId",
                table: "HourLogs",
                column: "VolunteerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HourLogs_AspNetUsers_VolunteerId",
                table: "HourLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shifts_Capacity",
                table: "Shifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shifts_EndsAfterStart",
                table: "Shifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicalRecords_NextDue",
                table: "MedicalRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicalRecords_Type",
                table: "MedicalRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HourLogs_Hours",
                table: "HourLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HourLogs_Status",
                table: "HourLogs");

            migrationBuilder.DropIndex(
                name: "IX_Donations_ApplicationId_AdoptionFee",
                table: "Donations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Donations_AdoptionFeeHasApplication",
                table: "Donations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Donations_Amount",
                table: "Donations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Donations_Frequency",
                table: "Donations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Donations_Status",
                table: "Donations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Donations_Type",
                table: "Donations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationStatusChanges_ToStatus",
                table: "ApplicationStatusChanges");

            migrationBuilder.DropIndex(
                name: "IX_Animals_BranchId_KennelNumber_InCare",
                table: "Animals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Animals_AgeMonths",
                table: "Animals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Animals_KennelNumber",
                table: "Animals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Animals_Size",
                table: "Animals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Animals_Species",
                table: "Animals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Animals_Status",
                table: "Animals");

            migrationBuilder.DropIndex(
                name: "IX_AnimalPhotos_AnimalId_Primary",
                table: "AnimalPhotos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnimalPhotos_Size",
                table: "AnimalPhotos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdoptionApplications_Status",
                table: "AdoptionApplications");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "AdoptionApplications");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ApplicationId",
                table: "Donations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Animals_BranchId_KennelNumber",
                table: "Animals",
                columns: new[] { "BranchId", "KennelNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_HourLogs_AspNetUsers_VolunteerId",
                table: "HourLogs",
                column: "VolunteerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

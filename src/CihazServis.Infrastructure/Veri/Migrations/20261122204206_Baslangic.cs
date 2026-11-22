using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CihazServis.Infrastructure.Veri.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "servis");

            migrationBuilder.CreateTable(
                name: "CihazModelleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SeriNoDeseni = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VarsayilanGarantiAy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CihazModelleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DogrulamaSorgulari",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Zaman = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IpOzeti = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SeriNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Sonuc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TuzakDolu = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DogrulamaSorgulari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kurumlar",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kurumlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cihazlar",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeriNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    KurumId = table.Column<int>(type: "int", nullable: true),
                    KurulumTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    GarantiBitis = table.Column<DateOnly>(type: "date", nullable: true),
                    HizmetSeviyesi = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cihazlar", x => x.Id);
                    table.CheckConstraint("CK_Cihazlar_SeriNo_Normalize", "[SeriNo] = UPPER([SeriNo]) COLLATE Latin1_General_BIN2 AND [SeriNo] NOT LIKE '%[- ]%'");
                    table.ForeignKey(
                        name: "FK_Cihazlar_CihazModelleri_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "servis",
                        principalTable: "CihazModelleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cihazlar_Kurumlar_KurumId",
                        column: x => x.KurumId,
                        principalSchema: "servis",
                        principalTable: "Kurumlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Eposta = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SifreOzeti = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KurumId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kullanicilar_Kurumlar_KurumId",
                        column: x => x.KurumId,
                        principalSchema: "servis",
                        principalTable: "Kurumlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cihazlar_KurumId",
                schema: "servis",
                table: "Cihazlar",
                column: "KurumId");

            migrationBuilder.CreateIndex(
                name: "IX_Cihazlar_ModelId",
                schema: "servis",
                table: "Cihazlar",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Cihazlar_SeriNo",
                schema: "servis",
                table: "Cihazlar",
                column: "SeriNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CihazModelleri_Kod",
                schema: "servis",
                table: "CihazModelleri",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DogrulamaSorgulari_Zaman",
                schema: "servis",
                table: "DogrulamaSorgulari",
                column: "Zaman");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_Eposta",
                schema: "servis",
                table: "Kullanicilar",
                column: "Eposta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KurumId",
                schema: "servis",
                table: "Kullanicilar",
                column: "KurumId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cihazlar",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "DogrulamaSorgulari",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "Kullanicilar",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "CihazModelleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "Kurumlar",
                schema: "servis");
        }
    }
}

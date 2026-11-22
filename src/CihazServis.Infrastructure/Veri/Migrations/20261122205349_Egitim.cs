using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CihazServis.Infrastructure.Veri.Migrations
{
    /// <inheritdoc />
    public partial class Egitim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kurslar",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kurslar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kurslar_CihazModelleri_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "servis",
                        principalTable: "CihazModelleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dersler",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KursId = table.Column<int>(type: "int", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MedyaAnahtari = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SureSaniye = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dersler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dersler_Kurslar_KursId",
                        column: x => x.KursId,
                        principalSchema: "servis",
                        principalTable: "Kurslar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sertifikalar",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    No = table.Column<string>(type: "char(26)", unicode: false, fixedLength: true, maxLength: 26, nullable: false),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    KursId = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VerilisZamani = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sertifikalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sertifikalar_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalSchema: "servis",
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sertifikalar_Kurslar_KursId",
                        column: x => x.KursId,
                        principalSchema: "servis",
                        principalTable: "Kurslar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DersIlerlemeleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    DersId = table.Column<int>(type: "int", nullable: false),
                    IzlenenAraliklar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IzlenenSaniye = table.Column<double>(type: "float", nullable: false),
                    EnIleriSaniye = table.Column<double>(type: "float", nullable: false),
                    VideoTamamlandi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    QuizGecildi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Dongu = table.Column<int>(type: "int", nullable: false),
                    AktifOturum = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OturumBaslangic = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OturumIzlenenSaniye = table.Column<double>(type: "float", nullable: false),
                    SurumDamgasi = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersIlerlemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DersIlerlemeleri_Dersler_DersId",
                        column: x => x.DersId,
                        principalSchema: "servis",
                        principalTable: "Dersler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersIlerlemeleri_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalSchema: "servis",
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuizDenemeleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    DersId = table.Column<int>(type: "int", nullable: false),
                    Dongu = table.Column<int>(type: "int", nullable: false),
                    Baslangic = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Bitis = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SoruDizilimi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DogruSayisi = table.Column<int>(type: "int", nullable: true),
                    SoruSayisi = table.Column<int>(type: "int", nullable: false),
                    Gecti = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizDenemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizDenemeleri_Dersler_DersId",
                        column: x => x.DersId,
                        principalSchema: "servis",
                        principalTable: "Dersler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizDenemeleri_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalSchema: "servis",
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuizSorulari",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DersId = table.Column<int>(type: "int", nullable: false),
                    Metin = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizSorulari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizSorulari_Dersler_DersId",
                        column: x => x.DersId,
                        principalSchema: "servis",
                        principalTable: "Dersler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SertifikaHareketleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SertifikaId = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Zaman = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    YapanKullaniciId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SertifikaHareketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SertifikaHareketleri_Sertifikalar_SertifikaId",
                        column: x => x.SertifikaId,
                        principalSchema: "servis",
                        principalTable: "Sertifikalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuizSecenekleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoruId = table.Column<int>(type: "int", nullable: false),
                    Metin = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Dogru = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizSecenekleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizSecenekleri_QuizSorulari_SoruId",
                        column: x => x.SoruId,
                        principalSchema: "servis",
                        principalTable: "QuizSorulari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DersIlerlemeleri_DersId",
                schema: "servis",
                table: "DersIlerlemeleri",
                column: "DersId");

            migrationBuilder.CreateIndex(
                name: "IX_DersIlerlemeleri_KullaniciId_DersId",
                schema: "servis",
                table: "DersIlerlemeleri",
                columns: new[] { "KullaniciId", "DersId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dersler_KursId_Sira",
                schema: "servis",
                table: "Dersler",
                columns: new[] { "KursId", "Sira" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kurslar_ModelId",
                schema: "servis",
                table: "Kurslar",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizDenemeleri_DersId",
                schema: "servis",
                table: "QuizDenemeleri",
                column: "DersId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizDenemeleri_KullaniciId_DersId_Dongu",
                schema: "servis",
                table: "QuizDenemeleri",
                columns: new[] { "KullaniciId", "DersId", "Dongu" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizSecenekleri_SoruId",
                schema: "servis",
                table: "QuizSecenekleri",
                column: "SoruId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizSorulari_DersId",
                schema: "servis",
                table: "QuizSorulari",
                column: "DersId");

            migrationBuilder.CreateIndex(
                name: "IX_SertifikaHareketleri_SertifikaId",
                schema: "servis",
                table: "SertifikaHareketleri",
                column: "SertifikaId");

            migrationBuilder.CreateIndex(
                name: "IX_Sertifikalar_KullaniciId_KursId",
                schema: "servis",
                table: "Sertifikalar",
                columns: new[] { "KullaniciId", "KursId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sertifikalar_KursId",
                schema: "servis",
                table: "Sertifikalar",
                column: "KursId");

            migrationBuilder.CreateIndex(
                name: "IX_Sertifikalar_No",
                schema: "servis",
                table: "Sertifikalar",
                column: "No",
                unique: true);

            // Sertifika geçmişi yalnız eklenir: güncelleme ve silme veritabanı seviyesinde de reddedilir.
            migrationBuilder.Sql("""
                CREATE TRIGGER [servis].[TR_SertifikaHareketleri_YalnizEkleme]
                ON [servis].[SertifikaHareketleri]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'SertifikaHareketleri yalnız eklemeye açıktır.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [servis].[TR_SertifikaHareketleri_YalnizEkleme];");

            migrationBuilder.DropTable(
                name: "DersIlerlemeleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "QuizDenemeleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "QuizSecenekleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "SertifikaHareketleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "QuizSorulari",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "Sertifikalar",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "Dersler",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "Kurslar",
                schema: "servis");
        }
    }
}

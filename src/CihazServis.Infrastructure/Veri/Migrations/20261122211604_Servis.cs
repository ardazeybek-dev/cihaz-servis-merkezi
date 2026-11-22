using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CihazServis.Infrastructure.Veri.Migrations
{
    /// <inheritdoc />
    public partial class Servis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtamaKurallari",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Oncelik = table.Column<int>(type: "int", nullable: false),
                    Konu = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ModelId = table.Column<int>(type: "int", nullable: true),
                    SorumluId = table.Column<int>(type: "int", nullable: false),
                    Etkin = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtamaKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtamaKurallari_CihazModelleri_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "servis",
                        principalTable: "CihazModelleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtamaKurallari_Kullanicilar_SorumluId",
                        column: x => x.SorumluId,
                        principalSchema: "servis",
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DenetimKayitlari",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Zaman = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    KullaniciId = table.Column<int>(type: "int", nullable: true),
                    Islem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Varlik = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VarlikNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Ayrinti = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DenetimKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DenetimKayitlari_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalSchema: "servis",
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DurumSorguKilitleri",
                schema: "servis",
                columns: table => new
                {
                    TalepNo = table.Column<string>(type: "char(14)", unicode: false, fixedLength: true, maxLength: 14, nullable: false),
                    HataliDeneme = table.Column<int>(type: "int", nullable: false),
                    SonHata = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    KilitBitis = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DurumSorguKilitleri", x => x.TalepNo);
                });

            migrationBuilder.CreateTable(
                name: "ServisTalepleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    No = table.Column<string>(type: "char(14)", unicode: false, fixedLength: true, maxLength: 14, nullable: false),
                    SeriNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CihazId = table.Column<int>(type: "int", nullable: true),
                    Dogrulandi = table.Column<bool>(type: "bit", nullable: false),
                    Konu = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    KurumAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefon = table.Column<string>(type: "char(10)", unicode: false, fixedLength: true, maxLength: 10, nullable: false),
                    Eposta = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    AydinlatmaSurumu = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AydinlatmaOnayZamani = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OlusturmaZamani = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HedefTarih = table.Column<DateOnly>(type: "date", nullable: false),
                    SorumluId = table.Column<int>(type: "int", nullable: false),
                    IpOzeti = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SurumDamgasi = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServisTalepleri", x => x.Id);
                    table.CheckConstraint("CK_ServisTalepleri_No", "[No] LIKE 'SR-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_ServisTalepleri_Telefon", "LEN([Telefon]) = 10 AND [Telefon] NOT LIKE '%[^0-9]%'");
                    table.ForeignKey(
                        name: "FK_ServisTalepleri_Cihazlar_CihazId",
                        column: x => x.CihazId,
                        principalSchema: "servis",
                        principalTable: "Cihazlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServisTalepleri_Kullanicilar_SorumluId",
                        column: x => x.SorumluId,
                        principalSchema: "servis",
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalepSayaclari",
                schema: "servis",
                columns: table => new
                {
                    Yil = table.Column<int>(type: "int", nullable: false),
                    Son = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalepSayaclari", x => x.Yil);
                });

            migrationBuilder.CreateTable(
                name: "TalepEkleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TalepId = table.Column<int>(type: "int", nullable: false),
                    OrijinalAd = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DepoAdi = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    IcerikTuru = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Boyut = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalepEkleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalepEkleri_ServisTalepleri_TalepId",
                        column: x => x.TalepId,
                        principalSchema: "servis",
                        principalTable: "ServisTalepleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalepHareketleri",
                schema: "servis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TalepId = table.Column<int>(type: "int", nullable: false),
                    EskiDurum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    YeniDurum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Zaman = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MusteriNotu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IcNot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    YapanKullaniciId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalepHareketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalepHareketleri_Kullanicilar_YapanKullaniciId",
                        column: x => x.YapanKullaniciId,
                        principalSchema: "servis",
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalepHareketleri_ServisTalepleri_TalepId",
                        column: x => x.TalepId,
                        principalSchema: "servis",
                        principalTable: "ServisTalepleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtamaKurallari_ModelId",
                schema: "servis",
                table: "AtamaKurallari",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_AtamaKurallari_Oncelik",
                schema: "servis",
                table: "AtamaKurallari",
                column: "Oncelik");

            migrationBuilder.CreateIndex(
                name: "IX_AtamaKurallari_SorumluId",
                schema: "servis",
                table: "AtamaKurallari",
                column: "SorumluId");

            migrationBuilder.CreateIndex(
                name: "IX_DenetimKayitlari_KullaniciId",
                schema: "servis",
                table: "DenetimKayitlari",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DenetimKayitlari_Zaman",
                schema: "servis",
                table: "DenetimKayitlari",
                column: "Zaman");

            migrationBuilder.CreateIndex(
                name: "IX_ServisTalepleri_CihazId",
                schema: "servis",
                table: "ServisTalepleri",
                column: "CihazId");

            migrationBuilder.CreateIndex(
                name: "IX_ServisTalepleri_Durum_HedefTarih",
                schema: "servis",
                table: "ServisTalepleri",
                columns: new[] { "Durum", "HedefTarih" });

            migrationBuilder.CreateIndex(
                name: "IX_ServisTalepleri_No",
                schema: "servis",
                table: "ServisTalepleri",
                column: "No",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServisTalepleri_SeriNo",
                schema: "servis",
                table: "ServisTalepleri",
                column: "SeriNo");

            migrationBuilder.CreateIndex(
                name: "IX_ServisTalepleri_SorumluId",
                schema: "servis",
                table: "ServisTalepleri",
                column: "SorumluId");

            migrationBuilder.CreateIndex(
                name: "IX_TalepEkleri_DepoAdi",
                schema: "servis",
                table: "TalepEkleri",
                column: "DepoAdi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalepEkleri_TalepId",
                schema: "servis",
                table: "TalepEkleri",
                column: "TalepId");

            migrationBuilder.CreateIndex(
                name: "IX_TalepHareketleri_TalepId_Zaman",
                schema: "servis",
                table: "TalepHareketleri",
                columns: new[] { "TalepId", "Zaman" });

            migrationBuilder.CreateIndex(
                name: "IX_TalepHareketleri_YapanKullaniciId",
                schema: "servis",
                table: "TalepHareketleri",
                column: "YapanKullaniciId");

            migrationBuilder.Sql("""
                CREATE TRIGGER [servis].[TR_TalepHareketleri_YalnizEkleme]
                ON [servis].[TalepHareketleri]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'TalepHareketleri yalnız eklemeye açıktır.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [servis].[TR_DenetimKayitlari_YalnizEkleme]
                ON [servis].[DenetimKayitlari]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'DenetimKayitlari yalnız eklemeye açıktır.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [servis].[TR_DenetimKayitlari_YalnizEkleme];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [servis].[TR_TalepHareketleri_YalnizEkleme];");

            migrationBuilder.DropTable(
                name: "AtamaKurallari",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "DenetimKayitlari",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "DurumSorguKilitleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "TalepEkleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "TalepHareketleri",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "TalepSayaclari",
                schema: "servis");

            migrationBuilder.DropTable(
                name: "ServisTalepleri",
                schema: "servis");
        }
    }
}

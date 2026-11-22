// Ortak, küçük davranışlar. Satır içi betik yok (içerik güvenliği politikası gereği).
(function () {
  "use strict";

  // Hata özeti varsa odak ona taşınır, ekran okuyucu hemen okur.
  var ozet = document.querySelector("[data-odakla]");
  if (ozet) {
    ozet.focus();
  }

  // Demo hesabı düğmeleri giriş formunu doldurur.
  document.querySelectorAll("[data-demo-hesap]").forEach(function (b) {
    b.addEventListener("click", function () {
      var eposta = document.getElementById("Eposta");
      var sifre = document.getElementById("Sifre");
      if (eposta && sifre) {
        eposta.value = b.getAttribute("data-demo-hesap");
        sifre.value = "Demo.2026!";
        sifre.form.querySelector("button[type=submit]").focus();
      }
    });
  });
})();

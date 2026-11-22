// Ortak, küçük davranışlar. Satır içi betik yok (içerik güvenliği politikası gereği).
(function () {
  "use strict";

  // Hata özeti varsa odak ona taşınır, ekran okuyucu hemen okur.
  var ozet = document.querySelector("[data-odakla]");
  if (ozet) {
    ozet.focus();
  }

  // Ek seçilince sayı ve boyut hemen denetlenir: 30 MB'ı yükleyip sonra hata görmek yerine gönderim engellenir.
  // Asıl denetim sunucuda yapılır (dosya imzası dahil), bu yalnız kullanıcıya erken haber verir.
  var ekAlani = document.querySelector("input[type=file][data-azami-adet]");
  if (ekAlani) {
    var ekHata = ekAlani.parentElement.querySelector(".alan-hata");
    var ekDenetle = function () {
      var adet = parseInt(ekAlani.getAttribute("data-azami-adet"), 10);
      var bayt = parseInt(ekAlani.getAttribute("data-azami-bayt"), 10);
      var mesaj = "";
      if (ekAlani.files.length > adet) {
        mesaj = "En fazla " + adet + " dosya ekleyebilirsiniz.";
      } else {
        for (var i = 0; i < ekAlani.files.length; i++) {
          if (ekAlani.files[i].size > bayt) {
            mesaj = ekAlani.files[i].name + " dosyası " + Math.round(bayt / 1048576) + " MB'tan büyük.";
            break;
          }
        }
      }
      ekAlani.setCustomValidity(mesaj);
      ekAlani.setAttribute("aria-invalid", mesaj ? "true" : "false");
      if (ekHata) {
        ekHata.textContent = mesaj;
      }
      return mesaj === "";
    };
    ekAlani.addEventListener("change", ekDenetle);
    ekAlani.form.addEventListener("submit", function (e) {
      if (!ekDenetle()) {
        e.preventDefault();
        ekAlani.focus();
      }
    });
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

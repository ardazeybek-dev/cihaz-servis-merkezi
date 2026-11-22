// Eğitim oynatıcısı. Kurallar burada kullanıcıyı yönlendirmek için uygulanır,
// asıl karar sunucudadır: izlenen aralıklar sunucuya bildirilir, sunucu ileri
// atlamayı ve gerçek süreden hızlı izlemeyi reddeder, tamamlanmayı kendisi hesaplar.
(function () {
  "use strict";

  var kap = document.querySelector("[data-oynatici]");
  if (!kap) {
    return;
  }

  var video = kap.querySelector("video");
  var dersId = kap.getAttribute("data-ders");
  var csrf = kap.getAttribute("data-csrf");
  var cubuk = kap.querySelector("[data-ilerleme-cubuk]");
  var metin = kap.querySelector("[data-ilerleme-metin]");
  var mesajAlani = kap.querySelector("[data-mesaj]");
  var quizBaglanti = kap.querySelector("[data-quiz-baglanti]");

  var BILDIRIM_ARALIGI_MS = 10000;
  var ILERI_TOLERANS = 1;

  var oturum = null;
  var tamamlandi = kap.getAttribute("data-tamamlandi") === "1";
  var enIleri = 0;        // izlenen en ileri nokta (sunucudan gelen ve kesintisiz izlenen)
  var parcaBas = 0;       // henüz bildirilmemiş parçanın başlangıcı
  var sonKonum = 0;       // kesintisiz oynatmada görülen son konum
  var duzeltiliyor = false;
  var durduruldu = false;
  var kuyruk = Promise.resolve();

  function mesaj(tur, yazi) {
    mesajAlani.textContent = "";
    if (!yazi) {
      return;
    }
    var d = document.createElement("div");
    d.className = "bildirim " + tur;
    d.setAttribute("data-mesaj-tur", tur);
    d.textContent = yazi;
    mesajAlani.appendChild(d);
  }

  function ilerlemeGoster(oran) {
    var yuzde = Math.min(100, Math.floor(oran * 100));
    cubuk.style.width = yuzde + "%";
    cubuk.parentElement.setAttribute("aria-valuenow", String(yuzde));
    metin.textContent = "İzlenen kısım %" + yuzde;
  }

  function istek(yol, govde) {
    return fetch("/api/v1/egitim/dersler/" + dersId + yol, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
      body: JSON.stringify(govde || {})
    }).then(function (r) {
      return r.json().catch(function () { return {}; }).then(function (j) {
        return { durum: r.status, veri: j };
      });
    }).catch(function () {
      return { durum: 0, veri: {} };
    });
  }

  function tamamlandiIsaretle() {
    if (tamamlandi) {
      return;
    }
    tamamlandi = true;
    quizBaglanti.hidden = false;
    mesaj("basari", "Video tamamlandı. Artık bu dersin sorularını çözebilirsiniz.");
  }

  function yanitIsle(y) {
    var v = y.veri || {};
    if (y.durum === 200) {
      enIleri = Math.max(enIleri, v.enIleriSaniye || 0);
      ilerlemeGoster(v.izlenenOran || 0);
      if (v.tamamlandi) {
        tamamlandiIsaretle();
      }
    } else if (y.durum === 409 && v.hata === "baskaOturum") {
      durduruldu = true;
      video.pause();
      mesaj("uyari", v.mesaj);
    } else if (y.durum === 422) {
      if (v.durum) {
        enIleri = v.durum.enIleriSaniye;
        ilerlemeGoster(v.durum.izlenenOran || 0);
      }
      mesaj("uyari", v.mesaj || "Bildirim kabul edilmedi.");
      if (v.hata === "ileriAtlama" && video.currentTime > enIleri) {
        duzeltiliyor = true;
        video.currentTime = enIleri;
      }
    }
  }

  // Bekleyen parçayı [parcaBas, bitis] olarak sıraya koyar. Bildirimler sırayla gider,
  // böylece sunucu aralıkları oynatmadaki sırayla görür.
  function parcaGonder(bitis) {
    if (!oturum || durduruldu || tamamlandi) {
      return;
    }
    var bas = parcaBas;
    if (video.duration) {
      bitis = Math.min(bitis, video.duration);
    }
    if (bitis - bas < 0.25) {
      return;
    }
    parcaBas = bitis;
    var govde = { oturumId: oturum, baslangic: Math.round(bas * 100) / 100, bitis: Math.round(bitis * 100) / 100 };
    kuyruk = kuyruk.then(function () {
      return istek("/izleme", govde).then(yanitIsle);
    });
  }

  video.addEventListener("timeupdate", function () {
    if (video.seeking || duzeltiliyor) {
      return;
    }
    var t = video.currentTime;
    if (t >= sonKonum && t - sonKonum < 2) {
      enIleri = Math.max(enIleri, t);
    }
    sonKonum = t;
  });

  video.addEventListener("seeking", function () {
    var hedef = video.currentTime;
    if (duzeltiliyor) {
      duzeltiliyor = false;
      parcaBas = sonKonum = hedef;
      return;
    }
    if (!tamamlandi && hedef > enIleri + ILERI_TOLERANS) {
      parcaGonder(sonKonum);
      duzeltiliyor = true;
      video.currentTime = enIleri;
      mesaj("uyari", "Video ileri sarılamaz. İzlediğiniz en ileri noktaya dönüldü.");
      return;
    }
    // Geri sarma serbest: o ana kadarki parça gönderilir, yeni parça buradan başlar.
    parcaGonder(sonKonum);
    parcaBas = sonKonum = hedef;
  });

  video.addEventListener("ratechange", function () {
    if (video.playbackRate !== 1) {
      video.playbackRate = 1;
      mesaj("uyari", "Oynatma hızı değiştirilemez.");
    }
  });

  video.addEventListener("play", function () {
    if (durduruldu) {
      video.pause();
    }
  });

  video.addEventListener("pause", function () {
    parcaGonder(sonKonum);
  });

  video.addEventListener("ended", function () {
    parcaGonder(video.duration);
  });

  document.addEventListener("visibilitychange", function () {
    if (document.hidden && !video.paused) {
      video.pause();
    }
  });

  window.addEventListener("pagehide", function () {
    parcaGonder(sonKonum);
  });

  setInterval(function () {
    if (!video.paused && !video.seeking) {
      parcaGonder(video.currentTime);
    }
  }, BILDIRIM_ARALIGI_MS);

  function kaynakYukle(adres, altyazi) {
    var iz = document.createElement("track");
    iz.kind = "captions";
    iz.srclang = "tr";
    iz.label = "Türkçe";
    iz.src = altyazi;
    iz.default = true;
    video.appendChild(iz);

    video.addEventListener("loadedmetadata", function () {
      if (!tamamlandi && enIleri > 0 && enIleri < video.duration - 1) {
        duzeltiliyor = true;
        video.currentTime = enIleri;
      }
      parcaBas = sonKonum = video.currentTime;
    }, { once: true });

    if (window.Hls && window.Hls.isSupported()) {
      var hls = new window.Hls({ capLevelToPlayerSize: true });
      hls.on(window.Hls.Events.ERROR, function (_, veri) {
        if (veri.fatal) {
          mesaj("hata", "Video yüklenemedi. Sayfayı yenileyip tekrar deneyin.");
        }
      });
      hls.loadSource(adres);
      hls.attachMedia(video);
    } else if (video.canPlayType("application/vnd.apple.mpegurl")) {
      // iPhone ve iPad'de Safari'nin kendi HLS oynatıcısı kullanılır, aynı olaylar geçerlidir.
      video.src = adres;
    } else {
      mesaj("hata", "Tarayıcınız bu videoyu oynatamıyor.");
    }
  }

  istek("/oturum").then(function (y) {
    if (y.durum !== 200) {
      mesaj("hata", (y.veri && y.veri.mesaj) || "Video açılamadı.");
      return;
    }
    oturum = y.veri.oturumId;
    enIleri = y.veri.enIleriSaniye;
    tamamlandi = y.veri.tamamlandi;
    ilerlemeGoster(y.veri.izlenenOran);
    kap.setAttribute("data-hazir", "1");
    kaynakYukle(y.veri.oynatmaAdresi, y.veri.altyaziAdresi);
  });
})();

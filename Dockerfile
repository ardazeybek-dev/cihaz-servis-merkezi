# Uygulama imajı: Linux sunucuda canlı demo için. IIS/Plesk kurulumu için README'deki yayın paketine bakın.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS derleme
WORKDIR /kaynak
COPY Directory.Build.props ./
COPY src/CihazServis.Application/CihazServis.Application.csproj src/CihazServis.Application/
COPY src/CihazServis.Infrastructure/CihazServis.Infrastructure.csproj src/CihazServis.Infrastructure/
COPY src/CihazServis.Web/CihazServis.Web.csproj src/CihazServis.Web/
RUN dotnet restore src/CihazServis.Web/CihazServis.Web.csproj
COPY src/ src/
RUN dotnet publish src/CihazServis.Web/CihazServis.Web.csproj -c Release --no-restore -o /cikti

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Ekler ve veri koruma anahtarları adlandırılmış birimlerde tutulur. Klasörler önceden açılır ki
# birim ilk oluşturulduğunda sahibi uygulama kullanıcısı olsun.
RUN mkdir -p /veri/ekler /veri/medya /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /veri /home/app/.aspnet
COPY --from=derleme /cikti .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Depolama__Klasor=/veri/ekler \
    Medya__Klasor=/veri/medya
EXPOSE 8080
ENTRYPOINT ["dotnet", "CihazServis.Web.dll"]

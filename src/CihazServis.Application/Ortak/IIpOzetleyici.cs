namespace CihazServis.Application.Ortak;

/// <summary>
/// IP adresini geri çevrilemeyen kısa bir özete dönüştürür. Günlüklerde ve sorgu
/// kayıtlarında açık IP adresi tutulmaz.
/// </summary>
public interface IIpOzetleyici
{
    string Ozetle(string? ip);
}

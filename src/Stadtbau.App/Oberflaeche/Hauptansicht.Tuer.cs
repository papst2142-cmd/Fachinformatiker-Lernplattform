using Avalonia.Controls;
using Lernplattform.Design;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.24.0 (Weisung P12-100, Nico 1b, Sammelliste 245): Knopf „Tür dazu“ bzw. „Tür entfernen“ unter dem
/// ausgewaehlten Gebaeude ohne Innenraum. Jeder Druck ist ein Schritt im Verlauf (Rueckgaengig geht). Ein gelber Hinweis
/// (Weg erreicht keine Strasse) steht in der Meldezeile und in der Pruefung.
/// </summary>
public sealed partial class Hauptansicht
{
    private Button TuerKnopf(AussenStelle s)
    {
        var hat = _projekt is not null && GebaeudeTuer.HatTuer(_projekt.AussenStand, s);
        var k = Schrift.Knopf(hat ? Texte.TuerEntfernen : Texte.TuerDazu).Mit(hat ? Texte.TuerEntfernenTipp : Texte.TuerDazuTipp);
        k.Click += (_, _) => TuerUmschalten(s);
        return k;
    }

    /// <summary>Tuer dazu oder weg am Gebaeude <paramref name="s"/> (ein Schritt). Rueckgabe: Meldung (auch fuer den Start-Test).</summary>
    public string? TuerUmschalten(AussenStelle s)
    {
        if (_projekt is null || _projekt.NurLesen || s.Art != AussenArt.Gebaeude)
        {
            return null;
        }
        string? meldung;
        if (GebaeudeTuer.HatTuer(_projekt.AussenStand, s))
        {
            _projekt.AussenAendern(Texte.TuerEntfernen, aa => GebaeudeTuer.Entfernen(aa, s));
            meldung = Texte.TuerEntfernen;
        }
        else
        {
            var raster = Karte.Raster;
            string? hinweis = null;
            _projekt.AussenAendern(Texte.TuerDazu, aa => hinweis = GebaeudeTuer.Dazu(aa, s, raster));
            var name = ModellNamen.Anzeige(_projekt.AussenStand, ModellVon(s));
            meldung = hinweis ?? Texte.TuerDazuGemacht(name);
        }
        Karte.Waehlen(AussenBearbeitung.Gueltig(_projekt.AussenStand, s) ? s : null);
        Melden(meldung);
        return meldung;
    }

    private string ModellVon(AussenStelle s) =>
        Aussen.Landschaft(_projekt!.AussenStand).ElementAtOrDefault(s.Nummer) is Lernplattform.Core.Daten.PyObjekt g ? Lernplattform.Core.Spiel.Py.Str(g["modell"]) : "";
}

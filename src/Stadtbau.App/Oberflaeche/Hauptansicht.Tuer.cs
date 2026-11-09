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

    /// <summary>Knopf „Tür setzen“ (S 0.28.0): schaltet das Tuer-Setzen fuer das Haus ein und aus.</summary>
    private Button TuerSetzenKnopf(AussenStelle s)
    {
        var an = Karte.TuerFuer == s;
        var k = Schrift.Knopf(an ? Texte.TuerSetzenBeenden : Texte.TuerSetzenKnopf).Mit(Texte.TuerSetzenTipp);
        k.Click += (_, _) => TuerSetzenUmschalten(s);
        if (an)
        {
            // gewaehlt wie ein Setzen-Knopf links (S 0.9.1)
            k.Farbe(Button.BackgroundProperty, "VerlaufW.primary").Farbe(Button.ForegroundProperty, "Pinsel.on_accent");
            (k.Content as TextBlock)?.Farbe(TextBlock.ForegroundProperty, "Pinsel.on_accent");
        }
        return k;
    }

    /// <summary>Tuer-Setzen fuer das Haus <paramref name="s"/> ein oder aus (kein Schritt).</summary>
    public void TuerSetzenUmschalten(AussenStelle s)
    {
        if (Karte.TuerFuer == s)
        {
            Karte.TuerSetzenBeenden();
            Melden(Texte.TuerSetzenBeenden);
        }
        else
        {
            Karte.TuerSetzenStarten(s);
        }
    }

    private string ModellVon(AussenStelle s) =>
        Aussen.Landschaft(_projekt!.AussenStand).ElementAtOrDefault(s.Nummer) is Lernplattform.Core.Daten.PyObjekt g ? Lernplattform.Core.Spiel.Py.Str(g["modell"]) : "";
}

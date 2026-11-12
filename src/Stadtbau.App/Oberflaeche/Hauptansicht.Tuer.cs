using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
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
        k.Click += async (_, _) => await TuerUmschaltenAsync(s);
        return k;
    }

    /// <summary>Tuer dazu oder weg am Gebaeude <paramref name="s"/> (ein Schritt). Rueckgabe: Meldung (auch fuer den Start-Test).</summary>
    public async Task<string?> TuerUmschaltenAsync(AussenStelle s)
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
            // Sammelliste 293: Platz auf der gezeichneten Fassade im Hintergrund rechnen (Szene bauen), dann ein Schritt
            var projekt = _projekt;
            var stand = (PyObjekt)Py.Kopie(projekt.AussenStand)!;
            var platz = await Task.Run(() => GebaeudeTuer.DazuPlatz(projekt, stand, s, raster));
            if (!ReferenceEquals(projekt, _projekt))
            {
                return null;
            }
            _projekt.AussenAendern(Texte.TuerDazu, aa => hinweis = GebaeudeTuer.DazuAn(aa, s, raster, platz));
            var name = ModellNamen.Anzeige(_projekt.AussenStand, ModellVon(s));
            meldung = hinweis ?? Texte.TuerDazuGemacht(name);
        }
        Karte.Waehlen(AussenBearbeitung.Gueltig(_projekt.AussenStand, s) ? s : null);
        Melden(meldung);
        return meldung;
    }

    private Button EingangKnopf(AussenStelle s)
    {
        var k = Schrift.Knopf(Texte.EingangAutomatisch).Mit(Texte.EingangAutomatischTipp);
        k.Click += async (_, _) => await EingangAsync(s);
        return k;
    }

    /// <summary>
    /// Patch S 0.46.1 (Weisung P12-225 b): Eingang eines Orts automatisch (Platz im Hintergrund rechnen, dann ein Schritt).
    /// Rueckgabe: Meldung (auch fuer den Start-Test).
    /// </summary>
    public async Task<string?> EingangAsync(AussenStelle s)
    {
        if (_projekt is null || _projekt.NurLesen || s.Art != AussenArt.Haus)
        {
            return null;
        }
        var projekt = _projekt;
        var stand = (PyObjekt)Py.Kopie(projekt.AussenStand)!;
        var platz = await Task.Run(() => GebaeudeTuer.EingangPlatz(projekt, stand, s));
        if (!ReferenceEquals(projekt, _projekt))
        {
            return null;
        }
        string? hinweis = null;
        _projekt.AussenAendern(Texte.EingangAutomatisch, aa => hinweis = GebaeudeTuer.EingangAn(aa, s, platz));
        var meldung = hinweis ?? Texte.EingangGemacht(_projekt.OrtName(s.Ort));
        Karte.Waehlen(AussenBearbeitung.Gueltig(_projekt.AussenStand, s) ? s : null);
        Melden(meldung);
        return meldung;
    }

    private Button HaltepunktKnopf(AussenStelle s)
    {
        var k = Schrift.Knopf(Texte.HaltepunktNeu).Mit(Texte.HaltepunktNeuTipp);
        k.Click += async (_, _) => await HaltepunktAsync(s);
        return k;
    }

    /// <summary>
    /// Patch S 0.46.1 (Weisung P12-225 a): alter Haltepunkt bekommt das neue Aussehen (Platz der Tuer im Hintergrund, dann ein
    /// Schritt). Rueckgabe: Meldung (auch fuer den Start-Test).
    /// </summary>
    public async Task<string?> HaltepunktAsync(AussenStelle s)
    {
        if (_projekt is null || _projekt.NurLesen || s.Art != AussenArt.Haus)
        {
            return null;
        }
        var projekt = _projekt;
        var stand = (PyObjekt)Py.Kopie(projekt.AussenStand)!;
        var platz = await Task.Run(() => HaltepunktTausch.Platz(projekt, stand, s.Ort));
        if (!ReferenceEquals(projekt, _projekt))
        {
            return null;
        }
        string? hinweis = null;
        _projekt.AussenAendern(Texte.HaltepunktNeu, aa => hinweis = HaltepunktTausch.Tauschen(aa, s.Ort, platz));
        var meldung = hinweis ?? Texte.HaltepunktGemacht(_projekt.OrtName(s.Ort));
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

    /// <summary>Knopf „Als Gebäude übernehmen“ an einem Katalog-Stueck (S 0.31.0, Weisung P12-130 Frage 2 = a).</summary>
    private Button AlsGebaeudeKnopf(AussenStelle s)
    {
        var k = Schrift.Knopf(Texte.AlsGebaeude).Mit(Texte.AlsGebaeudeTipp);
        k.Click += (_, _) => AlsGebaeudeUebernehmen(s);
        return k;
    }

    /// <summary>
    /// Das Katalog-Stueck <paramref name="s"/> als Gebaeude uebernehmen (ein Schritt) und das neue Gebaeude waehlen.
    /// Rueckgabe: Meldung (auch fuer den Start-Test).
    /// </summary>
    public string? AlsGebaeudeUebernehmen(AussenStelle s)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return null;
        }
        if (AussenBearbeitung.AlsGebaeudeNicht(_projekt.AussenStand, s) is { } nicht)
        {
            Melden(nicht);
            return nicht;
        }
        string? hinweis = null;
        var neu = _projekt.AussenAendern(Texte.AlsGebaeude, aa => AussenBearbeitung.AlsGebaeude(aa, s, out _, out hinweis));
        if (neu is null)
        {
            return null;
        }
        var meldung = hinweis ?? Texte.AlsGebaeudeGemacht(ModellNamen.Anzeige(_projekt.AussenStand, ModellVon(neu)));
        Karte.Waehlen(neu);
        Melden(meldung);
        return meldung;
    }

    private string ModellVon(AussenStelle s) =>
        Aussen.Landschaft(_projekt!.AussenStand).ElementAtOrDefault(s.Nummer) is Lernplattform.Core.Daten.PyObjekt g ? Lernplattform.Core.Spiel.Py.Str(g["modell"]) : "";
}

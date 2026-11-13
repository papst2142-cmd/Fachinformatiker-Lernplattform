using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241). Teil 1 Straßenzug: in einer leeren Stadt 32 × 24 „Straße ziehen“ mit der Vorlage
/// „Wohnstraße“ von (2; 12) bis (30; 12): die Vorschau zeigt die Baeume, die Nachpruefung im Hintergrund wird fertig
/// („geprüft“), der Schritt setzt genau die gruenen Stuecke der Vorschau (Vorschau = Ergebnis), EIN Schritt, ein
/// Rueckgaengig nimmt alles zurueck, kein roter Fund. Messpunkt „Strasse mit Ausstattung ziehen (bis zum Bild)“ (wiederholbar)
/// mit der Hauptstraße quer (Zebrastreifen, Haltestelle). Eigene Vorlage zusammenstellen, speichern, waehlen; ohne Gehweg
/// sind Zebrastreifen und Haltestelle grau; eine kaputte Datei steht mit Grund in der Liste; Loeschen nach Rueckfrage.
/// Teil 2 Gelaende: Umschalt+Ziehen (Rahmen) waehlt Huegel und gemalte Hoehen (Gegenprobe Stand S 0.50.0: keine gemalten),
/// Umschalt+Klick auf gemalten Boden nimmt den Bereich, Verschieben und Kopieren je ein Schritt; bei 16 Huegeln ist der Knopf
/// „Hügel“ grau mit Grund (Gegenprobe: einer weg, wieder frei). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task StrassenzugAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Strassenzug", null, (32, 24));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        k.Einpassen();
        ZiehWaehlen(a, 0);
        await BildAsync(a);
        if (k.ZiehArtAktiv != ZiehArt.Strasse || a.ZugWahlKnoepfe.Count < 4)
        {
            Fund(string.Create(Inv, $"Strassenzug: rechts keine Wahl „{Texte.ZugAusstattung}“ ({a.ZugWahlKnoepfe.Count} Knoepfe)"));
            return;
        }
        if (a.ZugWahlKnoepfe[0].IsChecked != true || k.ZugVorlage is not null)
        {
            Fund("Strassenzug: Vorgabe ist nicht „" + Texte.ZugOhne + "“");
        }
        a.ZugWahlKnoepfe[1].IsChecked = true;
        await BildAsync(a);
        if (k.ZugVorlage != Strassenzug.Wohnstrasse)
        {
            Fund("Strassenzug: Wahl „Wohnstraße“ wirkt nicht");
            return;
        }
        k.ZiehAnfangSetzen(2, 12);
        k.ZiehZielSetzen(30, 12);
        if (!await ZugGeprueftAsync(k))
        {
            Fund("Strassenzug: die Nachpruefung der Vorschau wird nicht fertig");
            return;
        }
        var vorschau = k.ZugVorschau!;
        if (vorschau.Stuecke.Count(s => s.Teil == ZugTeil.Baum) < 10 || !a.ZugInfoText.Contains("geprüft", StringComparison.Ordinal))
        {
            Fund(string.Create(Inv, $"Strassenzug: Vorschau mit {vorschau.Stuecke.Count} Stuecken, Info „{a.ZugInfoText}“"));
        }
        await AlleGroessenAsync(a, f, "Strassenzug Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var schritte = p.SchritteZurueck;
        var vorher = PythonJson.Standard(p.AussenStand);
        var baeumeVorher = AussenBearbeitung.Alle(p.AussenStand).Count(s => s.Stelle.Art == AussenArt.Baum);
        k.ZiehenBis(30, 12);
        await a.AussenGeprueft;
        var gesetzt = AussenBearbeitung.Alle(p.AussenStand).Count(s => s.Stelle.Art == AussenArt.Baum) - baeumeVorher;
        Messwerte.Notiere(string.Create(Inv, $"Strassenzug Wohnstrasse: Vorschau {vorschau.Frei} frei / {vorschau.Ausgelassen} rot, gesetzt {gesetzt} Baeume, Schritte +{p.SchritteZurueck - schritte}"));
        if (p.SchritteZurueck != schritte + 1 || gesetzt != vorschau.Frei)
        {
            Fund(string.Create(Inv, $"Strassenzug: Vorschau {vorschau.Frei} gruen, gesetzt {gesetzt}, Schritte +{p.SchritteZurueck - schritte} (soll gleich und 1)"));
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot)
        {
            Fund("Strassenzug: roter Fund nach der Wohnstraße: " + rot.Text);
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        if (PythonJson.Standard(p.AussenStand) != vorher)
        {
            Fund("Strassenzug: ein Rueckgaengig stellt den Stand vor der Straße nicht her");
        }
        // Grundlage fuer die Hauptstraße quer: die Wohnstraße bleibt
        k.ZiehAnfangSetzen(2, 12);
        k.ZiehZielSetzen(30, 12);
        k.ZiehenBis(30, 12);
        await a.AussenGeprueft;

        // Hauptstraße quer von der Wohnstraße nach oben: Zebrastreifen und Haltestelle
        a.ZugWahlKnoepfe[2].IsChecked = true;
        await MessenAsync("Strasse mit Ausstattung ziehen (bis zum Bild)", k, async () =>
        {
            k.ZiehAnfangSetzen(16, 11.5);
            k.ZiehenBis(16, 2);
            await BildAsync(k);
            var halte = AussenBearbeitung.Alle(p.AussenStand).Count(s => s.Stelle.Art == AussenArt.Haltestelle);
            var zebras = AussenBearbeitung.Alle(p.AussenStand).Count(s => s.Stelle.Art == AussenArt.Zebrastreifen);
            if (halte != 1 || zebras != 1)
            {
                Fund(string.Create(Inv, $"Strassenzug Hauptstrasse: {halte} Haltestellen, {zebras} Zebrastreifen (soll je 1)"));
            }
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        await a.AussenGeprueft;
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rotH)
        {
            Fund("Strassenzug: roter Fund nach der Hauptstraße: " + rotH.Text);
        }
        k.ZiehAnfangSetzen(8, 12.5);
        k.ZiehZielSetzen(8, 22);
        await ZugGeprueftAsync(k);
        await AlleGroessenAsync(a, f, "Strassenzug Hauptstrasse Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        k.ZiehAbbrechen();

        // eigene Vorlage: ohne Gehweg sind Zebrastreifen und Haltestelle grau; speichern, waehlen, kaputte Datei, loeschen
        a.ZugEigeneKnopf!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (a.ZugNameFeld is null || a.ZugGehwegSchalter is null || a.ZugSpeichernKnopf is null)
        {
            Fund("Strassenzug: „" + Texte.ZugEigene + "“ zeigt die Felder nicht");
            return;
        }
        a.ZugGehwegSchalter.IsChecked = false;
        await BildAsync(a);
        if (a.ZugZebraSchalter?.IsEnabled != false || a.ZugHaltSchalter?.IsEnabled != false)
        {
            Fund("Strassenzug: ohne Gehweg sind Zebrastreifen und Haltestelle nicht grau");
        }
        a.ZugGehwegSchalter!.IsChecked = true;
        await BildAsync(a);
        a.ZugNameFeld!.Text = "Wohnstraße";
        if (a.ZugSpeichern() is null)
        {
            Fund("Strassenzug: der Name einer festen Vorlage laesst sich speichern (Gegenprobe)");
        }
        a.ZugNameFeld!.Text = "Starttest Allee";
        a.ZugZebraSchalter!.IsChecked = true;
        await AlleGroessenAsync(a, f, "Strassenzug eigene Vorlage");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.ZugNameFeld!.Text = "Starttest Allee";
        if (a.ZugSpeichern() is { } grund)
        {
            Fund("Strassenzug: eigene Vorlage nicht gespeichert: " + grund);
            return;
        }
        await BildAsync(a);
        var ordner = a.Ablage.Strassenzuege;
        var kaputt = Path.Combine(ordner, "starttest_kaputt" + Strassenzug.Endung);
        await File.WriteAllTextAsync(kaputt, "kein Inhalt");
        a.ZugWahlKnoepfe[0].IsChecked = true;
        await BildAsync(a);
        var eigene = a.ZugWahlKnoepfe.FirstOrDefault(r => Avalonia.Automation.AutomationProperties.GetName(r)?.StartsWith("Starttest Allee", StringComparison.Ordinal) == true);
        if (eigene is null || a.ZugKaputtZeilen.Count != 1 || a.ZugKaputtZeilen[0].Text?.Contains(Texte.ZugKaputt, StringComparison.Ordinal) != true)
        {
            Fund(string.Create(Inv, $"Strassenzug: Liste ohne die eigene Vorlage ({eigene is not null}) oder die kaputte Datei mit Grund ({a.ZugKaputtZeilen.Count})"));
        }
        else
        {
            eigene.IsChecked = true;
            await BildAsync(a);
            if (k.ZugVorlage is not { Name: "Starttest Allee", Zebrastreifen: true } || a.ZugLoeschenKnopf is null)
            {
                Fund("Strassenzug: eigene Vorlage nicht waehlbar oder ohne „" + Texte.ZugLoeschen + "“");
            }
            await AlleGroessenAsync(a, f, "Strassenzug Liste mit eigener");
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
            a.ZugLoeschen(k.ZugVorlage!.Datei!, "Starttest Allee");
            if (k.ZugVorlage is not null || Strassenzug.Alle(a.Ablage).Any(e => e.Name == "Starttest Allee"))
            {
                Fund("Strassenzug: Loeschen der eigenen Vorlage wirkt nicht");
            }
        }
        File.Delete(kaputt);
        a.SetzenBeenden();
        await BildAsync(a);
        await GelaendeAuswahlAsync(a, f);
    }

    /// <summary>Warten, bis die Nachpruefung der Vorschau fertig ist (hoechstens 20 s).</summary>
    private static async Task<bool> ZugGeprueftAsync(KartenFlaeche k)
    {
        var uhr = Stopwatch.StartNew();
        while (k.ZugVorschau is not { Nachgeprueft: true } && uhr.Elapsed < TimeSpan.FromSeconds(20))
        {
            await Task.Delay(50);
        }
        Messwerte.Notiere(string.Create(Inv, $"Strassenzug: Nachpruefung der Vorschau {uhr.Elapsed.TotalMilliseconds:0} ms"));
        return k.ZugVorschau is { Nachgeprueft: true };
    }

    private static async Task GelaendeAuswahlAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Gelaendeauswahl", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        p.AussenAendern("Hügel", aa => AussenBearbeitung.Setzen(aa, AussenArt.Huegel, 8, 8));
        p.AussenAendern("Pinsel", aa => AussenBearbeitung.Pinselzug(aa, PinselArt.Heben, [new(20, 12), new(21, 12)], 1));
        await a.AussenGeprueft;
        k.Einpassen();
        await BildAsync(a);
        Point B(double x, double y) => k.BildPunktFuerTests(x, y);
        var gemalt = AussenBearbeitung.GemaltListe(p.AussenStand).Count;

        // Umschalt+Ziehen: Rahmen um alles
        MausMit(k, B(1, 1), B(31, 19), KeyModifiers.Shift);
        await BildAsync(k);
        var hoehen = k.Gewaehlt.Count(s => s.Art == AussenArt.Hoehe);
        if (!k.Gewaehlt.Contains(new AussenStelle(AussenArt.Huegel, 0)) || hoehen != gemalt)
        {
            Fund(string.Create(Inv, $"Gelaende-Auswahl: Rahmen waehlt Huegel {k.Gewaehlt.Contains(new AussenStelle(AussenArt.Huegel, 0))}, gemalte {hoehen} von {gemalt}"));
        }
        await AlleGroessenAsync(a, f, "Gelaende Rahmen");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var schritte = p.SchritteZurueck;
        var linksVorher = Enumerable.Range(0, gemalt).Min(n => AussenBearbeitung.HoehePunkt(p.AussenStand, n)!.Value.Lage.X);
        k.GruppeVerschieben(2, 0);
        await a.AussenGeprueft;
        var nachher = Enumerable.Range(0, AussenBearbeitung.GemaltListe(p.AussenStand).Count).Select(n => AussenBearbeitung.HoehePunkt(p.AussenStand, n)!.Value.Lage.X).ToList();
        if (p.SchritteZurueck != schritte + 1 || nachher.Count != gemalt || Math.Abs(nachher.Min() - (linksVorher + 2)) > 1e-3)
        {
            Fund(string.Create(Inv, $"Gelaende-Auswahl: Verschieben ist nicht ein Schritt mit dem Gemalten (Schritte +{p.SchritteZurueck - schritte}, kleinstes x {nachher.DefaultIfEmpty().Min()} statt {linksVorher + 2})"));
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        // Gegenprobe Stand S 0.50.0: der Rahmen nimmt keine gemalten Hoehen
        AussenBearbeitung.GelaendeAuswahlAltFuerTests = true;
        try
        {
            k.Waehlen(null);
            MausMit(k, B(1, 1), B(31, 19), KeyModifiers.Shift);
            if (k.Gewaehlt.Any(s => s.Art == AussenArt.Hoehe))
            {
                Fund("Gelaende-Auswahl Gegenprobe: auch ohne die neue Regel gemalte Hoehen gewaehlt");
            }
        }
        finally
        {
            AussenBearbeitung.GelaendeAuswahlAltFuerTests = false;
        }

        // Umschalt+Klick auf gemalten Boden: der ganze Bereich
        k.Waehlen(null);
        MausMit(k, B(20.5, 12), B(20.5, 12), KeyModifiers.Shift);
        await BildAsync(k);
        if (k.Gewaehlt.Count(s => s.Art == AussenArt.Hoehe) != gemalt)
        {
            Fund(string.Create(Inv, $"Gelaende-Auswahl: Umschalt+Klick auf Gemaltes waehlt {k.Gewaehlt.Count} statt {gemalt}"));
        }
        schritte = p.SchritteZurueck;
        a.GruppeKopierenKnopf?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await a.AussenGeprueft;
        if (p.SchritteZurueck != schritte + 1 || AussenBearbeitung.GemaltListe(p.AussenStand).Count != 2 * gemalt)
        {
            Fund(string.Create(Inv, $"Gelaende-Auswahl: Kopieren gab {AussenBearbeitung.GemaltListe(p.AussenStand).Count} Punkte statt {2 * gemalt}, Schritte +{p.SchritteZurueck - schritte}"));
        }
        await AlleGroessenAsync(a, f, "Gelaende kopiert");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.Rueckgaengig();
        k.Waehlen(null);

        // 16 Huegel: Knopf grau mit Grund, Gegenprobe einer weg
        for (var i = 1; i < Gelaende.HuegelHoechstens; i++)
        {
            var (x, y) = (3 + (i % 8 * 3.5), i < 8 ? 3.0 : 17.0);
            p.AussenAendern("Hügel", aa => AussenBearbeitung.Setzen(aa, AussenArt.Huegel, x, y));
        }
        await a.AussenGeprueft;
        if (a.HuegelKnopf is not { } hk)
        {
            Fund("Gelaende-Auswahl: Knopf „Hügel“ fehlt");
            return;
        }
        a.GruppeOeffnenFuer(hk);
        await BildAsync(a);
        if (hk.IsEnabled || a.HuegelGrundZeile is not { IsEffectivelyVisible: true } zeile || zeile.Text != Texte.HuegelGenug(Gelaende.HuegelHoechstens))
        {
            Fund(string.Create(Inv, $"Gelaende-Auswahl: bei {AussenBearbeitung.HuegelListe(p.AussenStand).Count} Huegeln ist der Knopf nicht grau mit Grund"));
        }
        await AlleGroessenAsync(a, f, "Huegel Hoechstzahl");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        p.AussenAendern("Hügel", aa => AussenBearbeitung.Loeschen(aa, new AussenStelle(AussenArt.Huegel, 0)));
        await a.AussenGeprueft;
        if (!hk.IsEnabled || a.HuegelGrundZeile?.IsVisible == true)
        {
            Fund("Gelaende-Auswahl Gegenprobe: bei 15 Huegeln bleibt der Knopf grau");
        }
    }
}

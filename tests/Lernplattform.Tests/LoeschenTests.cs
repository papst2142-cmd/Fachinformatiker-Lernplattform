using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 5 BE4 „Alle Lerndaten löschen“ und „Historie löschen“ wie Pythons reset_all, clear_history
/// und _marker_now (G40, tests/Vergleich/loeschen.json): Marke mit gemessener Uhrabweichung
/// (Pflicht-Kontrollfehler Weisung P5-3 Punkt 5), Stand danach, clock_fix_text, Texte.
/// </summary>
public class LoeschenTests
{
    private static readonly JsonElement G40 = VergleichsHilfe.Lade("loeschen.json");
    private static readonly JsonElement G20 = VergleichsHilfe.Lade("uebernahme.json");

    private static Uhr Uhr => SpeichernTests.FesteUhr(G40.GetProperty("jetzt_utc").GetString()!);

    private static Uhrabweichung Abweichung => new(() => G40.GetProperty("lauf").GetDouble(), G40.GetProperty("start").GetString(),
        G40.GetProperty("prozess").GetString()!);

    /// <summary>Pflicht: Marke mit Uhrabweichung (Uhr geht vor/nach, gestellt, Sprung nicht feststellbar, zu alt, Zeitumstellung).</summary>
    [Fact]
    public void MarkeWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G40.GetProperty("marken").EnumerateArray())
        {
            using var ordner = new TempOrdner();
            var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
            if (fall[0].ValueKind == JsonValueKind.String)
            {
                Assert.True(lernstand.Schreiben("INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($k, $w)",
                    ("$k", Uhrabweichung.Schluessel), ("$w", fall[0].GetString())));
            }
            var schreiber = new LernstandSchreiber(lernstand, Uhr, null, Abweichung);
            Assert.True(schreiber.AllesLoeschen());
            var marke = lernstand.Abfragen("SELECT value FROM sync_meta WHERE key = 'reset_at'");
            Assert.Equal(fall[1].GetString(), (string)marke.Single()[0]!);
            Assert.Equal(fall[2].GetDouble(), schreiber.LetzteUhrkorrektur);
            Assert.Equal(fall[3].GetString(), Uhrtexte.Korrektur(schreiber.LetzteUhrkorrektur));
            anzahl++;
        }
        Assert.True(anzahl >= 22);
        Assert.Contains(G40.GetProperty("marken").EnumerateArray(), f => f[1].GetString()!.EndsWith("+01:00", StringComparison.Ordinal));
    }

    /// <summary>Kontrollfehler „Historie löscht auch Karteikarten“: Stand danach wie Python (alle Tabellen, Marken).</summary>
    [Theory]
    [InlineData("historie")]
    [InlineData("alles")]
    public void LoeschenWiePython(string art)
    {
        using var ordner = new TempOrdner();
        var pfad = UebernahmeTests.BaueDatenbank(G20.GetProperty("quelle_db"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), Uhr, null, Abweichung);
        Assert.True(art == "historie" ? schreiber.HistorieLoeschen() : schreiber.AllesLoeschen());
        var erwartet = G40.GetProperty("loeschen").GetProperty(art);
        var soll = VergleichsHilfe.Stand(erwartet);
        var ist = VergleichsHilfe.Stand(pfad, erwartet);
        Assert.True(soll.SequenceEqual(ist), VergleichsHilfe.Unterschied(soll, ist));
        Assert.Equal(0, schreiber.LetzteUhrkorrektur);
        // Karteikarten bleiben bei "Historie löschen"
        using var db = VergleichsHilfe.Oeffnen(pfad);
        var karten = (long)VergleichsHilfe.Abfrage(db, "SELECT COUNT(*) FROM card_events")[0][0]!;
        Assert.Equal(art == "historie", karten > 0);
    }

    [Fact]
    public void TabellenWiePython()
    {
        Assert.Equal(G40.GetProperty("ereignistabellen").EnumerateArray().Select(t => t.GetString()), Wiederherstellung.Ereignistabellen);
        Assert.Equal(G40.GetProperty("historientabellen").EnumerateArray().Select(t => t.GetString()), Wiederherstellung.Historientabellen);
    }

    [Fact]
    public void UhrtextWiePython()
    {
        foreach (var fall in G40.GetProperty("uhrtexte").EnumerateArray())
        {
            Assert.Equal(fall[1].GetString(), Uhrtexte.Korrektur(fall[0].GetDouble()));
        }
    }

    /// <summary>Wird nichts gespeichert: nichts geloescht, keine Marke.</summary>
    [Fact]
    public void NurLesenLoeschtNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var vorher = UebernahmeTests.Sha(pfad);
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), Uhr, null, Abweichung);
        Assert.False(schreiber.AllesLoeschen());
        Assert.False(schreiber.HistorieLoeschen());
        Assert.Equal(vorher, UebernahmeTests.Sha(pfad));
    }

    [Fact]
    public void TexteWiePython()
    {
        var t = G40.GetProperty("texte");
        string T(string k) => t.GetProperty(k).GetString()!;
        Assert.Equal(LoeschenTexte.Lerndaten, T("lerndaten"));
        Assert.Equal(LoeschenTexte.LerndatenText, T("lerndaten_text"));
        Assert.Equal(LoeschenTexte.LerndatenKnopf, T("lerndaten_knopf"));
        Assert.Equal(LoeschenTexte.LerndatenFrageTitel, T("lerndaten_frage_titel"));
        Assert.Equal(LoeschenTexte.LerndatenFrage, T("lerndaten_frage"));
        Assert.Equal(LoeschenTexte.LerndatenErfolgTitel, T("lerndaten_erfolg_titel"));
        Assert.Equal(LoeschenTexte.LerndatenErfolg, T("lerndaten_erfolg"));
        Assert.Equal(LoeschenTexte.Historie, T("historie"));
        Assert.Equal(LoeschenTexte.HistorieText, T("historie_text"));
        Assert.Equal(LoeschenTexte.HistorieKnopf, T("historie_knopf"));
        Assert.Equal(LoeschenTexte.HistorieFrageTitel, T("historie_frage_titel"));
        Assert.Equal(LoeschenTexte.HistorieFrage, T("historie_frage"));
        Assert.Equal(LoeschenTexte.HistorieErfolgTitel, T("historie_erfolg_titel"));
        Assert.Equal(LoeschenTexte.HistorieErfolg, T("historie_erfolg"));
        Assert.Equal(LoeschenTexte.NichtsGeloescht, T("nichts_geloescht"));
        Assert.Equal(LoeschenTexte.Spielstand, T("spielstand"));
        Assert.Equal(LoeschenTexte.SpielstandKnopf, T("spielstand_knopf"));
        Assert.Equal(LoeschenTexte.Bestenliste, T("bestenliste"));
        Assert.Equal(LoeschenTexte.BestenlisteKnopf, T("bestenliste_knopf"));
        Assert.Equal(LoeschenTexte.DatenbankTitel, T("datenbank_titel"));
        var bereich = Optionen.Bereich(Optionen.Loeschen);
        Assert.Equal(bereich.Titel, T("bereich_titel"));
        Assert.Equal(bereich.UntertitelPc, T("bereich_untertitel"));
        // (W9) Datenbank: LERNPLATTFORM_DATEN (Ordner) statt FISI_DB_PATH (Datei)
        Assert.Contains("FISI_DB_PATH", T("datenbank_hinweis"));
        Assert.Contains("LERNPLATTFORM_DATEN", LoeschenTexte.DatenbankHinweis);
        Assert.DoesNotContain("FISI_DB_PATH", LoeschenTexte.DatenbankHinweis);
    }
}

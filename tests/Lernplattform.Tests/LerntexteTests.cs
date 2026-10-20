using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, Welle 2 (L4, L5, L6): Texte und Textregeln der Karteikarten, des
/// Pruefungstrainers und des Dialogs "Lernstand übernehmen" zeichengleich wie Python
/// v0.63.2 (Vergleichswerte G22, werkzeuge/vergleich/erzeuge_lerntexte.py).
/// </summary>
public class LerntexteTests
{
    private static readonly JsonElement G22 = VergleichsHilfe.Lade("lerntexte.json");
    private static readonly Type[] Klassen = [typeof(KartenTexte), typeof(TrainerTexte), typeof(UebernahmeTexte)];

    // Konstanten ohne sichtbaren Python-Text (Schluessel, Farben) oder mit eigenem Test
    private static readonly HashSet<string> OhneVergleich =
    [
        "KartenTexte.Freitext", "KartenTexte.Mehrfachwahl", "KartenTexte.Aufdecken", "KartenTexte.FarbeOffen",
        "TrainerTexte.Uebung", "TrainerTexte.Pruefung", "TrainerTexte.Ja", "TrainerTexte.Nein",
    ];

    private static string Konstante(string schluessel)
    {
        var teile = schluessel.Split('.');
        var klasse = Klassen.Single(k => k.Name == teile[0]);
        return (string)klasse.GetField(teile[1], BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    }

    [Fact]
    public void TexteWiePython()
    {
        var anzahl = 0;
        foreach (var bereich in new[] { "texte", "konstanten" })
        {
            foreach (var eintrag in G22.GetProperty(bereich).EnumerateObject())
            {
                if (eintrag.Name.StartsWith("Python.", StringComparison.Ordinal))
                {
                    continue;
                }
                Assert.True(eintrag.Value.GetString() == Konstante(eintrag.Name), eintrag.Name);
                anzahl++;
            }
        }
        Assert.Equal(66, anzahl);
    }

    [Fact]
    public void JederKartenUndTrainertextHatEinenVergleichswert()
    {
        var bekannt = G22.GetProperty("texte").EnumerateObject().Concat(G22.GetProperty("konstanten").EnumerateObject())
            .Select(e => e.Name).ToHashSet();
        foreach (var klasse in new[] { typeof(KartenTexte), typeof(TrainerTexte) })
        {
            foreach (var feld in klasse.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
            {
                var name = klasse.Name + "." + feld.Name;
                Assert.True(bekannt.Contains(name) || OhneVergleich.Contains(name), name + " ohne Vergleichswert");
            }
        }
    }

    [Fact]
    public void ListenWiePython()
    {
        var listen = G22.GetProperty("listen");
        static List<(string, string)> Paare(JsonElement liste) =>
            liste.EnumerateArray().Select(p => (p[0].GetString()!, p[1].GetString()!)).ToList();
        Assert.Equal(Paare(listen.GetProperty("Q_STATUS_TABS")), KartenTexte.StatusReiter);
        Assert.Equal(Paare(listen.GetProperty("CardsView.MODES")), KartenTexte.Modi);
        Assert.Equal(Paare(listen.GetProperty("QUIZ_MODES")), TrainerTexte.Arten);
        Assert.Equal(KartenTexte.Alle, listen.GetProperty("FILTER_ALL").GetString());
    }

    [Fact]
    public void StatuszeileWiePython()
    {
        foreach (var fall in G22.GetProperty("status_zeilen").EnumerateArray())
        {
            var buch = new Antwortbuch();
            var i = 0;
            foreach (var wert in fall.GetProperty("folge").EnumerateArray())
            {
                buch.Hinzufuegen(Quellen.Karte, "x", $"2026-10-{++i:00} 10:00:00", wert.ValueKind == JsonValueKind.Null ? null : wert.GetBoolean());
            }
            var (text, farbe) = KartenTexte.StatusZeile(new StatusBuch(buch), Quellen.Karte, "x");
            Assert.Equal(fall.GetProperty("text").GetString(), text);
            Assert.Equal(fall.GetProperty("farbe").GetString(), farbe);
        }
    }

    private static Dictionary<string, int> Zahlen(JsonElement z) => new()
    {
        [FragenStatus.Offen] = z[0].GetInt32(), [FragenStatus.Ueben] = z[1].GetInt32(), [FragenStatus.Fertig] = z[2].GetInt32(),
    };

    [Fact]
    public void ZaehltextUndPoolWiePython()
    {
        foreach (var fall in G22.GetProperty("zaehltexte").EnumerateArray())
        {
            Assert.Equal(fall.GetProperty("text").GetString(), KartenTexte.ZaehlText(Zahlen(fall.GetProperty("zahlen"))));
        }
        foreach (var fall in G22.GetProperty("pool").EnumerateArray())
        {
            Assert.Equal(fall.GetProperty("text").GetString(),
                TrainerTexte.Pool(fall.GetProperty("verfuegbar").GetInt32(), Zahlen(fall.GetProperty("zahlen"))));
        }
    }

    [Fact]
    public void ErgebnisblockWiePython()
    {
        foreach (var fall in G22.GetProperty("ergebnisse").EnumerateArray())
        {
            var richtig = fall.GetProperty("richtig").GetInt32();
            var gesamt = fall.GetProperty("gesamt").GetInt32();
            var prozent = Trainer.Prozent(richtig, gesamt);
            Assert.Equal(fall.GetProperty("text").GetString(), TrainerTexte.Ergebnis(richtig, gesamt, prozent, Trainer.Note(prozent),
                fall.GetProperty("sekunden").GetInt32(), fall.GetProperty("gespeichert").GetBoolean()));
        }
    }

    private static UebernahmeBericht Bericht(int neu, int nichtUebernommen = 0, int entfernt = 0) =>
        new(UebernahmeQuelle.Sicherung, false, neu, neu, 0, entfernt, 0, nichtUebernommen, 0, 0, 0, 0, 0,
            new Dictionary<string, string>(), [], null);

    [Fact]
    public void ErgebnissaetzeWiePython()
    {
        var konstanten = G22.GetProperty("texte");
        foreach (var fall in G22.GetProperty("eintraege").EnumerateArray())
        {
            var anzahl = fall.GetProperty("anzahl").GetInt32();
            Assert.Equal(fall.GetProperty("plural").GetString(), UebernahmeTexte.Eintraege(anzahl));
            Assert.Equal(fall.GetProperty("entfernt").GetString(), Beschriftungen.Einsetzen(UebernahmeTexte.Entfernt, UebernahmeTexte.Eintraege(anzahl)));
            if (anzahl > 0)
            {
                Assert.Equal(fall.GetProperty("uebernommen").GetString(), UebernahmeTexte.Ergebnis(Bericht(anzahl), null));
            }
        }
        Assert.Equal(konstanten.GetProperty("Python.merge_alles_da").GetString(), UebernahmeTexte.Ergebnis(Bericht(0), null));
        Assert.StartsWith(konstanten.GetProperty("Python.merge_nichts").GetString() + " ", UebernahmeTexte.Ergebnis(Bericht(0, 2), null), StringComparison.Ordinal);
        var py = G22.GetProperty("konstanten");
        Assert.StartsWith(UebernahmeTexte.HinweisDoppelt + " ", py.GetProperty("Python.PREVIEW_HINT").GetString(), StringComparison.Ordinal);
        Assert.Equal(UebernahmeTexte.FilterSicherung,
            py.GetProperty("Python.FILE_TYPE").GetString() + " (*" + py.GetProperty("Python.BACKUP_EXT").GetString() + ")");
        Assert.Equal(UebernahmeTexte.FilterSicherungMuster, "*" + py.GetProperty("Python.BACKUP_EXT").GetString());
    }

    /// <summary>
    /// Weisung P2-4: Freitext-Pruefung mit Python-lower(). Gleich bis auf das griechische
    /// Schluss-Sigma: Python schreibt am Wortende "ς", .NET "σ" (gemeldet, nicht angepasst).
    /// </summary>
    [Fact]
    public void KleinschreibungWiePythonAusserSchlussSigma()
    {
        var abweichend = new List<string>();
        foreach (var fall in G22.GetProperty("klein").EnumerateArray())
        {
            var text = fall.GetProperty("text").GetString()!;
            var erwartet = fall.GetProperty("klein").GetString()!;
            var ist = RahmenplanGewicht.Klein(text);
            if (ist != erwartet)
            {
                // Die einzige bekannte Abweichung: genau die Schluss-Sigmas
                Assert.Equal(erwartet.Replace('ς', 'σ'), ist);
                abweichend.Add(text);
            }
        }
        Assert.Equal(["ΟΔΟΣ", "ΟΔΟΣ ΚΑΙ", "ΑΣ.", "ΪΣ"], abweichend);
    }

    [Fact]
    public void MehrfachwahlReihenfolgeNachE1()
    {
        var inhalte = Lerninhalte.Laden(Pfade.Inhalte);
        var faelle = G22.GetProperty("mehrfachwahl").EnumerateArray().ToList();
        Assert.Equal(inhalte.Karteikarten.Count, faelle.Count);
        foreach (var (fall, karte) in faelle.Zip(inhalte.Karteikarten))
        {
            Assert.Equal(fall.GetProperty("frage").GetString(), karte.Frage);
            Assert.Equal(fall.GetProperty("optionen").EnumerateArray().Select(o => o.GetString()!).ToList(),
                Lernregeln.MehrfachwahlReihenfolge(karte.Frage!, karte.Optionen));
        }
    }

    [Fact]
    public void SicherungsVorschauWiePython()
    {
        var dateien = VergleichsHilfe.Lade("uebernahme.json").GetProperty("dateien");
        foreach (var fall in G22.GetProperty("sicherungen").EnumerateArray())
        {
            var json = Encoding.UTF8.GetBytes(dateien.GetProperty(fall.GetProperty("name").GetString()!).GetString()!);
            using var puffer = new MemoryStream();
            using (var gz = new GZipStream(puffer, CompressionLevel.Fastest, leaveOpen: true))
            {
                gz.Write(json);
            }
            Assert.Equal(fall.GetProperty("zeilen").EnumerateArray().Select(z => z.GetString()!).ToList(),
                UebernahmeAblauf.SicherungsZeilen(puffer.ToArray()));
        }
    }
}

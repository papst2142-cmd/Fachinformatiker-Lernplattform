using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W6: Vergleichswerte G50 (<c>spiel_ansichten.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_ansichten.py</c>): was die Spielansichten anzeigen, genau wie
/// die Python-Funktionen der Handy-App (result_text, reaction_text, rack_bars, rack_checks, form_check,
/// spare_catalog, spare_kinds, spare_delivery_days, knowledge, topic_knowledge, learned_projects,
/// avatar_shapes). Die Antworten kommen aus G46 (<c>spiel_bewertung.json</c>, gleiche Reihenfolge).
/// </summary>
public class SpielAnsichtenTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_ansichten.json");

    private static readonly JsonElement Bewertungen = VergleichsHilfe.Lade("spiel_bewertung.json");

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    private static void Gleich(JsonElement soll, Func<object?> rechnen, string wo)
    {
        if (soll.ValueKind == JsonValueKind.Object && soll.TryGetProperty("ausnahme", out var art))
        {
            Assert.True(Record.Exception(() => rechnen()) is not null, wo + ": Python wirft " + art.GetString() + ", C# nicht");
            return;
        }
        Assert.Equal(wo + ": " + T(W(soll)), wo + ": " + T(rechnen()));
    }

    [Fact]
    public void ErgebnisReaktionUndAnzeigewerteWiePython()
    {
        var soll = Soll.GetProperty("ergebnis").EnumerateArray().ToList();
        var auftraege = Bewertungen.GetProperty("aufgaben").EnumerateArray().ToList();
        Assert.Equal(auftraege.Count, soll.Count);
        var antworten = 0;
        for (var i = 0; i < soll.Count; i++)
        {
            var eintrag = auftraege[i];
            var id = eintrag.GetProperty("id").GetString()!;
            Assert.Equal(id, soll[i].GetProperty("id").GetString());
            var auftrag = Py.Flach(SpielDaten.Auftrag(id, Inhalt)!);
            if (eintrag.GetProperty("extra").ValueKind == JsonValueKind.Object)
            {
                foreach (var (k, v) in (PyObjekt)W(eintrag.GetProperty("extra"))!)
                {
                    Py.Setzen(auftrag, k, v);
                }
            }
            var wo = id + (eintrag.GetProperty("extra").ValueKind == JsonValueKind.Object ? " (extra)" : "");
            if (soll[i].TryGetProperty("ersatz", out var ersatz))
            {
                Gleich(ersatz, () => SpielAnsicht.ErsatzKatalog(auftrag, Inhalt).Select(t => t["id"]).ToList(), wo + " Ersatz");
            }
            var zeilen = eintrag.GetProperty("antworten").EnumerateArray().ToList();
            var sollZeilen = soll[i].GetProperty("antworten").EnumerateArray().ToList();
            Assert.Equal(zeilen.Count, sollZeilen.Count);
            for (var n = 0; n < zeilen.Count; n++)
            {
                var z = sollZeilen[n];
                var name = wo + " " + z.GetProperty("name").GetString();
                Assert.Equal(zeilen[n].GetProperty("name").GetString(), z.GetProperty("name").GetString());
                var antwort = W(zeilen[n].GetProperty("antwort"));
                if (z.TryGetProperty("ausnahme", out _))
                {
                    Assert.NotNull(Record.Exception(() => Bewertung.Auswerten(auftrag, Py.Kopie(antwort), false, [], 3, null, null, Inhalt, null)));
                }
                else
                {
                    var daten = Bewertung.Auswerten(auftrag, Py.Kopie(antwort), false, [], 3, null, null, Inhalt, null);
                    Gleich(z.GetProperty("text"), () => SpielAnsicht.Ergebnis(auftrag, daten, null, Inhalt), name + " Text");
                    Gleich(z.GetProperty("reaktion"), () => SpielAnsicht.Reaktion(auftrag, daten, Inhalt), name + " Reaktion");
                }
                if (z.TryGetProperty("balken", out var balken))
                {
                    Gleich(balken, () => SpielAnsicht.RackBalken(auftrag, Py.Kopie(antwort) as PyObjekt, Inhalt), name + " Balken");
                    Gleich(z.GetProperty("regeln"), () => SpielAnsicht.RackPruefungen(auftrag, Py.Kopie(antwort) as PyObjekt, Inhalt), name + " Regeln");
                }
                if (z.TryGetProperty("felder", out var felder))
                {
                    Gleich(felder, () => SpielAnsicht.FormPruefung(auftrag, Py.Kopie(antwort) as PyObjekt), name + " Felder");
                }
                antworten++;
            }
        }
        Assert.True(antworten > 4000);
    }

    [Fact]
    public void ErsatzteileWiePython()
    {
        var ersatz = Soll.GetProperty("ersatz");
        Assert.Equal(T(W(ersatz.GetProperty("arten"))), T(SpielAnsicht.ErsatzTypen(Inhalt)));
        Assert.Equal(ersatz.GetProperty("liefertage").GetInt64(), SpielAnsicht.ErsatzLieferTage(Inhalt));
    }

    [Fact]
    public void WissenAusDemLernstandWiePython()
    {
        using var ordner = new TempOrdner();
        var lernen = Lerninhalte.Laden(Pfade.Inhalte);
        var balancing = SpielDaten.Objekt(Inhalt["balancing"]);
        var testdaten = Path.Combine(ordner.Pfad, "testdaten.db");
        File.Copy(Pfade.Vergleich("testdaten.db"), testdaten);
        foreach (var (name, pfad) in new[] { ("testdaten", testdaten), ("leer", Path.Combine(ordner.Pfad, "fehlt.db")) })
        {
            var soll = Soll.GetProperty("wissen").GetProperty(name);
            var wissen = new SpielWissen(pfad, lernen, balancing);
            Assert.Equal(name + ": " + T(W(soll.GetProperty("fachbereiche"))), name + ": " + T(wissen.Fachbereiche()));
            Assert.Equal(name + ": " + T(W(soll.GetProperty("themen"))), name + ": " + T(wissen.Themen()));
            Assert.Equal(name + ": " + T(W(soll.GetProperty("projekte"))),
                name + ": " + T(wissen.Projekte().OrderBy(p => Py.Int(p)).Cast<object?>().ToList()));
        }
    }

    [Fact]
    public void FigurWiePython()
    {
        var faelle = 0;
        foreach (var fall in Soll.GetProperty("figur").EnumerateArray())
        {
            var name = fall[0].GetString();
            var ist = SpielBilder.Avatar((PyObjekt)W(fall[1])!)
                .Select(f => (object?)new List<object?> { f.Art, new List<object?> { f.Ecken.X1, f.Ecken.Y1, f.Ecken.X2, f.Ecken.Y2 }, f.Farbe })
                .ToList();
            Assert.Equal(name + ": " + T(W(fall[2])), name + ": " + T(ist));
            faelle++;
        }
        Assert.True(faelle > 20);
    }
}

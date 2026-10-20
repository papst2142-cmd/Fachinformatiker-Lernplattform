using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 1, K1: Datenmodell Lerninhalte. Geladene Listen, erzeugte Fragen,
/// Kennzahlen und Pruefmeldungen exakt wie Python (Vergleichswerte G12, ohne
/// Spielteil). Die absichtlich kaputten Faelle sind die Kontrollfehler: jeder
/// muss genau die Meldungen von Python liefern.
/// </summary>
public class InhaltePruefungTests
{
    private static readonly JsonElement G12 = Lade();
    private static readonly Lerninhalte Echt = Lerninhalte.Laden(Pfade.Inhalte);

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("inhalte_pruefung.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    private static List<string> Texte(JsonElement liste) => liste.EnumerateArray().Select(e => e.GetString()!).ToList();

    [Fact]
    public void EchteInhalteOhneMeldung()
    {
        Assert.Equal(Texte(G12.GetProperty("echt").GetProperty("meldungen")), InhaltePruefung.Pruefen(Echt));
        Assert.Empty(InhaltePruefung.Pruefen(Echt));
    }

    [Fact]
    public void ListenWiePython()
    {
        var soll = G12.GetProperty("echt").GetProperty("listen");
        var ist = new Dictionary<string, IReadOnlyList<Lerninhalt>>
        {
            ["KARTEIKARTEN"] = Echt.Karteikarten,
            ["QUIZ_QUESTIONS"] = Echt.Quizfragen,
            ["SZENARIEN"] = Echt.Ap2Szenarien,
            ["AP1_SZENARIEN"] = Echt.Ap1Szenarien,
            ["PROJEKTARBEITEN"] = Echt.Testprojekte,
        };
        Assert.Equal(soll.EnumerateObject().Select(p => p.Name).Order(), ist.Keys.Order());
        foreach (var liste in soll.EnumerateObject())
        {
            Assert.Equal(liste.Value.GetProperty("anzahl").GetInt32(), ist[liste.Name].Count);
            Assert.True(liste.Value.GetProperty("pruefsumme").GetString() == Pruefsumme(ist[liste.Name]), liste.Name);
        }
        Assert.Equal(G12.GetProperty("echt").GetProperty("anzahl_quizfragen_datei").GetInt32(), Echt.AnzahlQuizfragenDatei);
    }

    [Fact]
    public void ErzeugteFragenWiePython()
    {
        var soll = JsonNode.Parse(G12.GetProperty("echt").GetProperty("erzeugte_fragen").GetRawText())!.AsArray();
        var ist = Echt.Quizfragen.Skip(Echt.AnzahlQuizfragenDatei).ToList();
        Assert.Equal(soll.Count, ist.Count);
        for (var i = 0; i < soll.Count; i++)
        {
            Assert.True(JsonNode.DeepEquals(soll[i], ist[i].Daten), soll[i]!.ToJsonString());
            Assert.Equal(InhaltArt.Quizfrage, ist[i].Art);
            Assert.Equal(Echt.AnzahlQuizfragenDatei + i + 1, ist[i].Nummer);
        }
    }

    [Fact]
    public void ZaehlwerteWiePython()
    {
        var soll = G12.GetProperty("echt").GetProperty("zaehlwerte");
        Vergleiche(soll.GetProperty("content_totals"), Echt.InhalteJeFachbereich());
        Vergleiche(soll.GetProperty("topic_totals"), Echt.InhalteJeThema());
        Vergleiche(soll.GetProperty("theme_totals"), Echt.Ap2SzenarienJeBlock());
        Vergleiche(soll.GetProperty("ap1_theme_totals"), Echt.Ap1SzenarienJeBlock());
    }

    private static void Vergleiche(JsonElement soll, OrderedDictionary<string, int> ist) =>
        Assert.Equal(soll.EnumerateObject().Select(p => (p.Name, p.Value.GetInt32())), ist.Select(p => (p.Key, p.Value)));

    /// <summary>Kontrollfehler: absichtlich kaputte Inhalte liefern genau die Meldungen von Python.</summary>
    [Fact]
    public void KaputteInhalteWiePython()
    {
        var faelle = G12.GetProperty("faelle").EnumerateArray().ToList();
        Assert.True(faelle.Count >= 28);
        var mitMeldung = 0;
        foreach (var fall in faelle)
        {
            using var ordner = new TempOrdner();
            KopiereInhalte(ordner.Pfad);
            foreach (var aenderung in fall.GetProperty("aenderungen").EnumerateArray())
            {
                Aendere(ordner.Pfad, aenderung);
            }
            var soll = Texte(fall.GetProperty("meldungen"));
            var ist = InhaltePruefung.Pruefen(Lerninhalte.Laden(ordner.Pfad));
            Assert.True(soll.SequenceEqual(ist),
                fall.GetProperty("name").GetString() + ": soll [" + string.Join(" | ", soll) + "] ist [" + string.Join(" | ", ist) + "]");
            mitMeldung += soll.Count > 0 ? 1 : 0;
        }
        // Nur die Positivprobe "ap2_thema_block" (Thema ueber THEME_BLOCK zugeordnet) ist ohne Meldung
        Assert.Equal(faelle.Count - 1, mitMeldung);
    }

    /// <summary>Kontrollfehler fuer den Listenvergleich: ein geaendertes Zeichen faellt auf.</summary>
    [Fact]
    public void PruefsummeErkenntAenderung()
    {
        using var ordner = new TempOrdner();
        KopiereInhalte(ordner.Pfad);
        Aendere(ordner.Pfad, JsonDocument.Parse("""["szenarien_ap1", [7, "title"], "setze", "Geaendert"]""").RootElement);
        var geaendert = Lerninhalte.Laden(ordner.Pfad);
        var soll = G12.GetProperty("echt").GetProperty("listen");
        Assert.NotEqual(soll.GetProperty("AP1_SZENARIEN").GetProperty("pruefsumme").GetString(), Pruefsumme(geaendert.Ap1Szenarien));
        Assert.Equal(soll.GetProperty("KARTEIKARTEN").GetProperty("pruefsumme").GetString(), Pruefsumme(geaendert.Karteikarten));
    }

    [Fact]
    public void UnbekannterFachbereichBrichtAb()
    {
        using var ordner = new TempOrdner();
        KopiereInhalte(ordner.Pfad);
        Aendere(ordner.Pfad, JsonDocument.Parse("""["karteikarten", [0, "cat"], "setze", "physik"]""").RootElement);
        var fehler = Assert.Throws<InvalidDataException>(() => Lerninhalte.Laden(ordner.Pfad));
        Assert.Contains("physik", fehler.Message);
    }

    [Fact]
    public void UmwandlungWiePython()
    {
        var roh = JsonNode.Parse(File.ReadAllText(Path.Combine(Pfade.Inhalte, "szenarien_ap2.json")))!.AsArray();
        var erstes = Echt.Ap2Szenarien[0];
        Assert.Equal(Fachbereiche.Schluessel[(string)roh[0]!["cat"]!], erstes.Fachbereich);
        foreach (var feld in Lerninhalte.TextFelder)
        {
            if (roh[0]![feld] is JsonArray zeilen)
            {
                Assert.Equal(string.Join("\n", zeilen.Select(z => (string)z!)), erstes.Text(feld));
            }
        }
        // Ohne Feld "fachrichtung" gilt Systemintegration
        Assert.All(Echt.Karteikarten.Concat(Echt.Quizfragen).Concat(Echt.Ap2Szenarien).Concat(Echt.Ap1Szenarien).Concat(Echt.Testprojekte),
            e => Assert.Equal(Fachrichtungen.Systemintegration, e.Fachrichtung));
        Assert.Equal(4, Echt.Karteikarten[0].Optionen.Count);
        Assert.Equal(Fachbereiche.Alle.Count, Echt.Themen.Count);
    }

    // -- Hilfen ----------------------------------------------------------------

    /// <summary>Pruefsumme wie werkzeuge/vergleich/erzeuge_inhalte_pruefung.py.</summary>
    private static string Pruefsumme(IEnumerable<Lerninhalt> liste)
    {
        static string WertText(JsonNode? wert) => wert switch
        {
            JsonArray a => "[" + string.Join("\u001f", a.Select(WertText)) + "]",
            JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
            _ => throw new InvalidDataException("unerwarteter Typ " + wert?.GetValueKind()),
        };
        var teile = liste.Select(e => string.Join("\u001d",
            e.Daten.Select(p => p.Key).Order(StringComparer.Ordinal).Select(k => k + "=" + WertText(e.Daten[k]))));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001e", teile))));
    }

    private static void KopiereInhalte(string ziel)
    {
        foreach (var datei in Directory.EnumerateFiles(Pfade.Inhalte, "*.json"))
        {
            File.Copy(datei, Path.Combine(ziel, Path.GetFileName(datei)));
        }
    }

    /// <summary>Eine Aenderung [datei, pfad, art, wert] wie aendere() im Erzeuger.</summary>
    private static void Aendere(string ordner, JsonElement aenderung)
    {
        var datei = Path.Combine(ordner, aenderung[0].GetString() + ".json");
        var daten = JsonNode.Parse(File.ReadAllText(datei))!;
        var pfad = aenderung[1].EnumerateArray().ToList();
        var eltern = Gehe(daten, pfad.Take(pfad.Count - 1));
        var letzter = pfad[^1];
        var wert = aenderung[3];
        switch (aenderung[2].GetString())
        {
            case "setze":
                Setze(eltern, letzter, JsonNode.Parse(wert.GetRawText()));
                break;
            case "entferne":
                if (eltern is JsonArray liste)
                {
                    liste.RemoveAt(Index(liste, letzter));
                }
                else
                {
                    Assert.True(eltern.AsObject().Remove(letzter.GetString()!));
                }
                break;
            case "anhaengen":
                Hole(eltern, letzter)!.AsArray().Add(JsonNode.Parse(wert.GetRawText()));
                break;
            case "kopiere":
                Setze(eltern, letzter, Gehe(daten, wert.EnumerateArray()).DeepClone());
                break;
            default:
                throw new ArgumentException(aenderung[2].GetString());
        }
        File.WriteAllText(datei, daten.ToJsonString());
    }

    private static JsonNode Gehe(JsonNode daten, IEnumerable<JsonElement> pfad)
    {
        foreach (var teil in pfad)
        {
            daten = Hole(daten, teil)!;
        }
        return daten;
    }

    private static JsonNode? Hole(JsonNode eltern, JsonElement teil) =>
        eltern is JsonArray liste ? liste[Index(liste, teil)] : eltern[teil.GetString()!];

    private static void Setze(JsonNode eltern, JsonElement teil, JsonNode? wert)
    {
        if (eltern is JsonArray liste)
        {
            liste[Index(liste, teil)] = wert;
        }
        else
        {
            eltern[teil.GetString()!] = wert;
        }
    }

    /// <summary>Listenindex wie Python (negativ = vom Ende).</summary>
    private static int Index(JsonArray liste, JsonElement teil) => teil.GetInt32() < 0 ? liste.Count + teil.GetInt32() : teil.GetInt32();
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Bau;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pakete;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.97.0 (Weisung P12-312 Punkte 2 und 3, Nico 2c): „Inhalte laden“ schneller mit gleichem Ergebnis.
/// W1: Ausschluss ueber eine Nachschlagetabelle, Gegenprobe alte Suche gegen neue auf allen Eintraegen.
/// W2: Lern-Dateien je Fachrichtung beim Bauen aufgeteilt (werkzeuge/bau/LernenTeile.cs, derselbe Teiler wie im Bau der App);
/// Quelle bleibt allein inhalte_csharp/lernen. Geprueft: jeder Eintrag mit seiner Stelle gleich der Quelle, Index (Laenge,
/// Pruefsumme, Platzhalter) gleich der Rechnung der App, und das Laden mit Teilen gibt Eintrag fuer Eintrag dasselbe wie ohne
/// (Nummern, Fachrichtung, Daten, Ausschluss, Fachgespraech) fuer jede Fachrichtung und beim Nachladen. Gegenprobe: mit Teilen
/// liest das Laden von FISI die ganzen Listen nicht.
/// </summary>
public sealed class InhalteLadenSchnellTests
{
    private static readonly string Lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");

    private static byte[]? Quelle(string name) =>
        Path.Combine(Lernen, name + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;

    private static readonly Lazy<Dictionary<string, byte[]>> Teile = new(() => LernenTeiler.Teilen(n => Quelle(n)!));

    private static Func<string, byte[]?> TeileLesen(Dictionary<string, byte[]> teile) => n => teile.GetValueOrDefault(n);

    // ---- W2: Teiler gegen die Quelle ----------------------------------------------------------------------------------------

    /// <summary>Alle Teile einer Liste gegen die Quelle: jede Stelle genau einmal, Eintrag gleich, im Teil seiner Fachrichtung.</summary>
    private static void TeileGleichQuelle(Dictionary<string, byte[]> teile, string name, byte[] quelle)
    {
        using var dokument = JsonDocument.Parse(quelle);
        var eintraege = dokument.RootElement.EnumerateArray().ToList();
        var gesehen = new bool[eintraege.Count];
        foreach (var (datei, inhalt) in teile.Where(t => t.Key.StartsWith(name + ".", StringComparison.Ordinal)))
        {
            var teil = datei[(name.Length + 1)..^".json".Length];
            using var teilDokument = JsonDocument.Parse(inhalt);
            var vorher = -1;
            foreach (var paar in teilDokument.RootElement.EnumerateArray())
            {
                Assert.Equal(2, paar.GetArrayLength());
                var stelle = paar[0].GetInt32();
                Assert.True(stelle > vorher, $"{datei}: Stelle {stelle} nicht steigend");
                vorher = stelle;
                Assert.False(gesehen[stelle], $"{datei}: Stelle {stelle} doppelt");
                gesehen[stelle] = true;
                Assert.True(JsonElement.DeepEquals(eintraege[stelle], paar[1]), $"{datei}: Eintrag an Stelle {stelle} anders");
                Assert.Equal(LernenTeiler.Teil(FachrichtungsPaket.FachrichtungVon(eintraege[stelle])!), teil);
            }
        }
        Assert.True(gesehen.All(g => g), name + ": Stelle fehlt in den Teilen: " + Array.IndexOf(gesehen, false));
    }

    /// <summary>Index gegen die Rechnung der App (FachrichtungsPaket.IstPlatzhalter/FachrichtungVon auf der Quelle).</summary>
    private static void IndexGleich(Dictionary<string, byte[]> teile, string name, byte[] quelle, JsonElement liste)
    {
        using var index = JsonDocument.Parse(teile[LernenTeiler.IndexName]);
        var eintrag = index.RootElement.GetProperty("listen").GetProperty(name);
        var elemente = liste.EnumerateArray().ToList();
        Assert.Equal(elemente.Count, eintrag.GetProperty("laenge").GetInt32());
        Assert.Equal(LernenTeiler.Pruefsumme(quelle), eintrag.GetProperty("sha256").GetString());
        var platzhalter = elemente.Where(FachrichtungsPaket.IstPlatzhalter).Select(FachrichtungsPaket.FachrichtungVon).OfType<string>()
            .Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(platzhalter, eintrag.GetProperty("platzhalter").EnumerateArray().Select(p => p.GetString()!).ToList());
    }

    [Fact]
    public void Teiler_JederEintragMitStelle_GleichDerQuelle()
    {
        var teile = Teile.Value;
        foreach (var name in LernenTeiler.Listen)
        {
            var quelle = Quelle(name)!;
            TeileGleichQuelle(teile, name, quelle);
            using var dokument = JsonDocument.Parse(quelle);
            IndexGleich(teile, name, quelle, dokument.RootElement);
        }
        foreach (var paar in LernenTeiler.NurIndex)
        {
            var quelle = Quelle(paar[0])!;
            using var dokument = JsonDocument.Parse(quelle);
            IndexGleich(teile, paar[0], quelle, dokument.RootElement.GetProperty(paar[1]));
        }
        // die Listen der App sind dieselben wie die des Teilers (und die des Pakets)
        Assert.Equal(Lerninhalte.LernenListen.Select(l => l.Name), LernenTeiler.Listen);
        Assert.Equal(FachrichtungsPaket.Listen, [.. LernenTeiler.Listen, LernenTeiler.NurIndex[0][0]]);
        // FISI ist der kleinste Teil (darum der Gewinn beim Start)
        Assert.True(teile["quizfragen.si.json"].Length < Quelle("quizfragen")!.Length / 3);
    }

    [Fact]
    public void Teiler_Sonderfaelle_Platzhalter_Escapes_OhneFachrichtung()
    {
        // Platzhalter, Eintrag ohne Fachrichtung, kein Objekt, Anfuehrungszeichen und Klammern in Texten, verschachtelt
        var liste = """
            [ {"q": "a \"[{\" b", "fachrichtung": "ae", "x": {"fachrichtung": "dv"}},
              {"fachrichtung":"dpa","im_paket":true},
              {"q":"ohne"} , 17, "text", [1,{"fachrichtung":"si"}],
              {"q":"\\\\","fachrichtung":"si","im_paket":false,"z":[{"im_paket":true}]},
              {"q":"ü € 😀","fachrichtung":"AE"} ]
            """;
        var quelle = Encoding.UTF8.GetBytes(liste);
        var fach = Encoding.UTF8.GetBytes("""{"_hinweis":["x"],"fragen":[{"fachrichtung":"dv","im_paket":true}]}""");
        var teile = LernenTeiler.Teilen(n => n switch { "quizfragen" => quelle, "fachgespraech" => fach, _ => null! });
        TeileGleichQuelle(teile, "quizfragen", quelle);
        Assert.Contains("quizfragen._ohne.json", teile.Keys);
        Assert.DoesNotContain("karteikarten.si.json", teile.Keys);
        using (var d = JsonDocument.Parse(quelle))
        {
            IndexGleich(teile, "quizfragen", quelle, d.RootElement);
        }
        using (var d = JsonDocument.Parse(fach))
        {
            IndexGleich(teile, "fachgespraech", fach, d.RootElement.GetProperty("fragen"));
        }
        // die App liest den Index genauso
        var ordner = new LernenOrdner(n => n == "quizfragen" ? quelle : null, TeileLesen(teile));
        Assert.True(ordner.MitTeilen);
        Assert.True(ordner.HatPlatzhalter("dpa"));
        Assert.True(ordner.HatPlatzhalter("dv"));
        Assert.False(ordner.HatPlatzhalter("ae"));
        Assert.Equal([0], ordner.TeilEintraege("quizfragen", ["ae"]).Select(e => e.Index));
        Assert.Equal([1, 6], ordner.TeilEintraege("quizfragen", ["si", "dpa"]).Select(e => e.Index));
        // kaputte Quelle bricht den Bau ab (kein stilles Weglassen)
        Assert.Throws<InvalidDataException>(() => LernenTeiler.Teilen(n => n == "quizfragen" ? Encoding.UTF8.GetBytes("[{\"q\":1},") : null!));
        Assert.Throws<InvalidDataException>(() => LernenTeiler.Teilen(n => n == "quizfragen" ? Encoding.UTF8.GetBytes("{}") : null!));
    }

    // ---- W2: Laden mit Teilen gleich Laden ohne -----------------------------------------------------------------------------

    private static string Bild(Lerninhalte i)
    {
        var b = new StringBuilder();
        foreach (var (art, liste) in new[] { ("k", i.Karteikarten), ("q", i.Quizfragen), ("a", i.Ap2Szenarien), ("p", i.Testprojekte) })
        {
            foreach (var e in liste)
            {
                b.Append(art).Append(e.Nummer).Append('|').Append(e.Zugehoerigkeit).Append('|').Append(e.Daten.ToJsonString()).Append('\n');
            }
        }
        foreach (var (art, liste) in i.Neue.OrderBy(p => p.Key))
        {
            b.Append(art).Append(':').Append(string.Join(",", liste.Select(e => e.Nummer))).Append('\n');
        }
        b.Append(string.Join(";", i.Ausschluss)).Append('\n');
        b.Append(string.Join(";", i.NeueFachfragen.Select(f => f.Nummer + f.Zugehoerigkeit + f.Daten.ToJsonString()))).Append('\n');
        b.Append(string.Join(",", i.Geladen?.Order() ?? Enumerable.Empty<string>())).Append('|').Append(i.Themen.Sum(t => t.Value.Count));
        return b.ToString();
    }

    [Fact]
    public void Laden_MitTeilen_GleichOhne_JeFachrichtung_UndNachladen()
    {
        var teile = Teile.Value;
        var bisher = Lerninhalte.Laden(Pfade.Inhalte);
        foreach (string[] geladen in new[] { new[] { "si" }, ["si", "ae"], ["si", "dpa"], ["si", "dv"] })
        {
            var ohne = Lerninhalte.MitLernen(bisher, new LernenOrdner(Quelle), geladen);
            var mit = Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), new LernenOrdner(Quelle, TeileLesen(teile)), geladen);
            Assert.Equal(Bild(ohne), Bild(mit));
            foreach (var fr in Fachrichtungen.Werte)
            {
                Assert.Equal(Bild(ohne.Fuer(fr)), Bild(mit.Fuer(fr)));
            }
            // Nachladen einer weiteren Fachrichtung (Wechsel in den Optionen)
            var weitere = geladen.Length == 1 ? "dv" : "ae" == geladen[1] ? "dpa" : "ae";
            Assert.Equal(Bild(ohne.MitFachrichtung(weitere)), Bild(mit.MitFachrichtung(weitere)));
        }
        // alle Fachrichtungen (null) laden weiter die ganzen Listen: gleich
        Assert.Equal(Bild(Lerninhalte.MitLernen(bisher, new LernenOrdner(Quelle), null)),
            Bild(Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), new LernenOrdner(Quelle, TeileLesen(teile)), null)));
    }

    [Fact]
    public void Gegenprobe_MitTeilen_LiestSiNichtDieGanzenListen()
    {
        var teile = Teile.Value;
        var gelesen = new System.Collections.Concurrent.ConcurrentBag<string>();
        byte[]? Zaehlend(string name)
        {
            gelesen.Add(name);
            return Quelle(name);
        }
        var ordner = new LernenOrdner(Zaehlend, TeileLesen(teile));
        ordner.Vorab();
        var inhalte = Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), ordner, ["si"]);
        Assert.True(inhalte.Quizfragen.Count > 0);
        Assert.DoesNotContain(gelesen, n => LernenTeiler.Listen.Contains(n));
        // ohne Teile liest dasselbe Laden die ganzen Listen (so war es bis A 0.96.0)
        gelesen.Clear();
        Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), new LernenOrdner(Zaehlend), ["si"]);
        Assert.Contains(gelesen, n => n == "quizfragen");
    }

    // ---- W1: Nachschlagetabelle gegen die alte Suche ------------------------------------------------------------------------

    [Fact]
    public void Ausschluss_Nachschlagetabelle_GleichDerAltenSucheAufAllenEintraegen()
    {
        var alle = Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), Quelle);
        var ausschluss = alle.Ausschluss;
        Assert.True(ausschluss.Count > 50, "Ausschluesse: " + ausschluss.Count);
        var tabelle = Lerninhalte.AusschlussTabelle.Aus(ausschluss);
        var treffer = 0;
        var geprueft = 0;
        foreach (var name in LernenTeiler.Listen)
        {
            var datei = JsonNode.Parse(Quelle(name)!)!.AsArray();
            for (var i = 0; i < datei.Count; i++)
            {
                if (datei[i] is not JsonObject daten)
                {
                    continue;
                }
                var alt = Lerninhalte.Ausgeschlossen(ausschluss, name, i, daten);
                Assert.Equal(alt, tabelle.Enthaelt(name, i, daten));
                treffer += alt ? 1 : 0;
                geprueft++;
                // falsche Stelle, falsche Datei, anderer Text: beide nie
                Assert.Equal(Lerninhalte.Ausgeschlossen(ausschluss, name, i + 1, daten), tabelle.Enthaelt(name, i + 1, daten));
                Assert.Equal(Lerninhalte.Ausgeschlossen(ausschluss, "x" + name, i, daten), tabelle.Enthaelt("x" + name, i, daten));
            }
        }
        Assert.Equal(ausschluss.Count, treffer);
        Assert.True(geprueft > 10000, "geprueft: " + geprueft);
        // Feld q fehlt oder ist kein Text: nie ausgeschlossen (wie bisher)
        var a = ausschluss[0];
        foreach (var daten in new[] { new JsonObject(), new JsonObject { ["q"] = 5 }, new JsonObject { ["q"] = new JsonArray(a.Frage) } })
        {
            Assert.False(Lerninhalte.Ausgeschlossen(ausschluss, a.Datei, a.Stelle, daten));
            Assert.False(tabelle.Enthaelt(a.Datei, a.Stelle, daten));
        }
        Assert.True(tabelle.Enthaelt(a.Datei, a.Stelle, new JsonObject { ["q"] = a.Frage }));
    }
}

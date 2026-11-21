using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.1.0 (Sammelliste 409 „Gaenge zu eng“, Weisung P12-328): Blickfeld der Ich-Sicht (Regler 60 bis 110 Grad, normale
/// Kamera unveraendert). Welle A 1.2.0 (Weisung P12-332, Nico: „Der Flur wurde laenger, aber nicht breiter“): Massstab wieder
/// 0,6 m, Vorgabe des Blickfelds 75 Grad, Flure wirklich breiter (<see cref="Flurbreite"/>), Laenge wie bis A 1.0.0.
/// </summary>
public class SpielBlickfeldTests
{
    /// <summary>Alle Grundrisse mit Flur, die breiter werden (Ort, Name, Grundriss in den alten Zahlen).</summary>
    private static List<(string Ort, string Wo, PyObjekt Gebaeude)> Grundrisse()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        var liste = new List<(string, string, PyObjekt)> { (SpielDaten.OrtBuero, "bitweiche", SpielDaten.Objekt(inhalt["gebaeude"])) };
        foreach (var w in SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]).Select(SpielDaten.Objekt))
        {
            liste.Add((SpielDaten.OrtZuhause, "wohnung " + Py.Str(w["id"]), SpielDaten.Objekt(w["gebaeude"])));
        }
        var firma = SpielDaten.Objekt(inhalt["firma"]);
        foreach (var (teil, ort) in new[] { ("gebaeude", SpielDaten.OrtBuero), ("filiale", SpielDaten.OrtFiliale) })
        {
            var stufen = SpielDaten.Liste(SpielDaten.Objekt(firma[teil])["stufen"]);
            for (var i = 0; i < stufen.Count; i++)
            {
                liste.Add((ort, $"{teil} Stufe {i}", SpielDaten.Objekt(SpielDaten.Objekt(stufen[i])["gebaeude"])));
            }
        }
        foreach (var k in SpielDaten.Kundenorte(inhalt))
        {
            liste.Add((Py.Str(k["id"]), "kunde " + Py.Str(k["id"]), SpielDaten.Objekt(k["gebaeude"])));
        }
        liste.Add((Innenraum.Serverfarm, "serverfarm", SpielDaten.Objekt(SpielDaten.Objekt(SpielInnenraumTests.Innenraeume.Value["serverfarm"])["gebaeude"])));
        return liste;
    }

    /// <summary>Breite und Laenge (m) des Flurs im Innenraum: neu (Umrechnung, Massstab) oder wie A 1.1.0 (ohne, 0,75 m).</summary>
    private static (double Breite, double Laenge) Masse(string ort, PyObjekt g, float meter, bool breiter)
    {
        var neu = breiter ? Flurbreite.Anwenden(g, Flurbreite.Fuer(ort, g)) : g;
        return (Py.F(SpielDaten.Objekt(neu["flur"])["h"]) / meter, Py.F(neu["breite"]) / meter);
    }

    /// <summary>
    /// Welle A 1.2.0 (Weisung P12-332 Punkt 1, Nico: „mehr Platz nach links und rechts zur Wand … nicht laenger, nur breiter“):
    /// jede Diele mindestens 1,30 m, jeder andere Flur mindestens 1,78 m breit, die Laenge jedes Flurs wie bis A 1.0.0
    /// (0,6 m je Einheit, +-0,01 m). Gegenprobe Stand A 1.1.0 (0,75 m, Flur nicht breiter): zu schmal und zu lang.
    /// </summary>
    [Fact]
    public void Flure_Breiter_NichtLaenger_WieA100_GegenprobeA110Rot()
    {
        Assert.Equal(1f / 0.6f, Innenraum.Meter, 5);
        var plaene = Grundrisse();
        Assert.True(plaene.Count >= 15, "zu wenige Grundrisse: " + plaene.Count);
        Assert.Equal(4, plaene.Count(p => p.Ort == SpielDaten.OrtZuhause));
        var funde = new List<string>();
        var gegen = new List<string>();
        foreach (var (ort, wo, g) in plaene)
        {
            var mindestens = ort == SpielDaten.OrtZuhause ? 1.30 : 1.78;
            var (breite, laenge) = Masse(ort, g, Innenraum.Meter, true);
            var laengeA100 = Py.F(g["breite"]) * 0.6;
            if (breite < mindestens || Math.Abs(laenge - laengeA100) > 0.01)
            {
                funde.Add($"{wo}: {breite:0.00} m breit, {laenge:0.00} m lang (A 1.0.0 {laengeA100:0.00} m)");
            }
            var (b110, l110) = Masse(ort, g, Innenraum.MeterA110, false);
            if (b110 < mindestens || Math.Abs(l110 - laengeA100) > 0.01)
            {
                gegen.Add(wo);
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // Gegenprobe: mit A 1.1.0 ist jeder Grundriss zu schmal oder zu lang (alle laenger, die meisten auch zu schmal)
        Assert.Equal(plaene.Count, gegen.Count);
        Assert.Equal(plaene.Count, plaene.Count(p => Masse(p.Ort, p.Gebaeude, Innenraum.MeterA110, false).Laenge > (Py.F(p.Gebaeude["breite"]) * 0.6) + 0.01));
    }

    /// <summary>Tueroeffnungen (Raum, offen, Seite, von, bis, Lage der Wand) eines Grundrisses.</summary>
    private static List<(string Raum, bool Offen, string Seite, double Von, double Bis, double Wand)> Tueroeffnungen(PyObjekt g) =>
        [.. SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt).Where(r => SpielDaten.Get(r, "tuer") is PyObjekt t && SpielDaten.Get(t, "von") is not null).Select(r =>
        {
            var t = SpielDaten.Objekt(r["tuer"]);
            var seite = Wege.TuerSeite(r, g);
            var wand = seite switch
            {
                "s" => Py.F(r["y"]) + Py.F(r["h"]),
                "n" => Py.F(r["y"]),
                "w" => Py.F(r["x"]),
                _ => Py.F(r["x"]) + Py.F(r["w"]),
            };
            return (Py.Str(r["id"]), Py.Wahr(SpielDaten.Get(t, "offen")), seite, Py.F(t["von"]), Py.F(t["bis"]), wand);
        })];

    /// <summary>Stuecke (nicht begehbar), die in der Oeffnung einer Tuer (0,3 Einheiten zu beiden Seiten der Wand) stehen.</summary>
    private static List<string> InDerTuer(PyObjekt g)
    {
        var deko = SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt).SelectMany(r => SpielDaten.ListeOderLeer(r, "deko"))
            .Concat(SpielDaten.ListeOderLeer(SpielDaten.Objekt(g["flur"]), "deko")).Select(SpielDaten.Objekt)
            .Where(d => !Wege.Begehbar.Contains(Py.Str(d["typ"])) && SpielDaten.Get(d, "w") is not null).ToList();
        var funde = new List<string>();
        foreach (var t in Tueroeffnungen(g).Where(t => !t.Offen))
        {
            var quer = t.Seite is "w" or "o";
            var (x1, y1, x2, y2) = quer ? (t.Wand - 0.3, t.Von, t.Wand + 0.3, t.Bis) : (t.Von, t.Wand - 0.3, t.Bis, t.Wand + 0.3);
            funde.AddRange(deko.Where(d => Py.F(d["x"]) < x2 && x1 < Py.F(d["x"]) + Py.F(d["w"]) && Py.F(d["y"]) < y2 && y1 < Py.F(d["y"]) + Py.F(d["h"]))
                .Select(d => $"{t.Raum}: {Py.Str(d["typ"])} {Py.F(d["x"])}/{Py.F(d["y"])}"));
        }
        return funde;
    }

    /// <summary>
    /// Welle A 1.2.0 (Weisung P12-332, Chat 3a): jede Innentuer im Innenraum ist mindestens 0,90 m breit (offene Durchgaenge
    /// sind ohnehin breiter), liegt ganz in der Wand ihres Raums, ihre Mitte bleibt (+-0,01), und kein Stueck steht neu in ihrer
    /// Oeffnung. Gegenprobe heutiger Stand (A 1.1.0, ohne Verbreiterung): Tueren schmaler als 0,90 m.
    /// </summary>
    [Fact]
    public void Innentueren_MindestensNeunzigZentimeter_MitteBleibt_Gegenprobe()
    {
        var funde = new List<string>();
        var schmalVorher = 0;
        foreach (var (ort, wo, g) in Grundrisse())
        {
            var breit = Flurbreite.Anwenden(g, Flurbreite.Fuer(ort, g));
            var neu = Flurbreite.Tueren(breit);
            var vorher = Tueroeffnungen(breit);
            var nachher = Tueroeffnungen(neu);
            Assert.Equal(vorher.Count, nachher.Count);
            var raeume = SpielDaten.Liste(neu["raeume"]).Select(SpielDaten.Objekt).ToDictionary(r => Py.Str(r["id"]));
            for (var i = 0; i < vorher.Count; i++)
            {
                var (a, b) = (vorher[i], nachher[i]);
                schmalVorher += !a.Offen && (a.Bis - a.Von) / Innenraum.Meter < 0.9 - 1e-6 ? 1 : 0;
                var meter = (b.Bis - b.Von) / Innenraum.Meter;
                if (meter < 0.9 - 1e-6)
                {
                    funde.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{wo} {b.Raum}: Tuer {meter:F2} m"));
                }
                var r = raeume[b.Raum];
                var (anfang, laenge) = b.Seite is "w" or "o" ? (Py.F(r["y"]), Py.F(r["h"])) : (Py.F(r["x"]), Py.F(r["w"]));
                if (b.Von < anfang - 1e-6 || b.Bis > anfang + laenge + 1e-6)
                {
                    funde.Add($"{wo} {b.Raum}: Tuer ragt aus der Wand");
                }
                if (Math.Abs(((a.Von + a.Bis) / 2) - ((b.Von + b.Bis) / 2)) > 0.01 && !a.Offen)
                {
                    funde.Add($"{wo} {b.Raum}: Mitte der Tuer verschoben");
                }
            }
            var alt = InDerTuer(breit);
            funde.AddRange(InDerTuer(neu).Where(f => !alt.Contains(f)).Select(f => wo + " " + f + " steht in der Tuer"));
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(schmalVorher >= 50, "Gegenprobe: zu wenige schmale Tueren vorher: " + schmalVorher);
        // im gebauten Innenraum: Bitweiche mit breiten Tueren, die Mitte jeder Tuer bleibt fuer die Wege des Spiel-Kerns
        var buero = Innenraum.Bauen("buero", new Spielablauf(SpielInhaltTests.Inhalt).Stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
        Assert.All(Tueroeffnungen(buero.Gebaeude), t => Assert.True(t.Bis - t.Von >= Flurbreite.Tuer - 1e-6, t.Raum));
    }

    /// <summary>
    /// Welle A 1.2.0: die Raeume behalten ihre Groesse; Raeume ueber dem Flur bleiben, Raeume darunter (mit Einrichtung und Tueren)
    /// ruecken genau um den Zuwachs; ein Raum, der den Flur ganz quert, wird um den Zuwachs tiefer, keiner ragt nur hinein; Hin und Zurueck ergeben wieder dieselbe Stelle.
    /// </summary>
    [Fact]
    public void Raeume_GleichGross_UnterDemFlurVerschoben_UmrechnungHinUndZurueck()
    {
        foreach (var (ort, wo, g) in Grundrisse())
        {
            var u = Flurbreite.Fuer(ort, g);
            Assert.True(u.Zuwachs > 0, wo + ": Flur wird nicht breiter");
            var neu = Flurbreite.Anwenden(g, u);
            Assert.Equal(Py.F(g["hoehe"]) + u.Zuwachs, Py.F(neu["hoehe"]), 6);
            Assert.Equal(Py.F(g["breite"]), Py.F(neu["breite"]));
            var alt = SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt).ToList();
            var jetzt = SpielDaten.Liste(neu["raeume"]).Select(SpielDaten.Objekt).ToList();
            Assert.Equal(alt.Count, jetzt.Count);
            for (var i = 0; i < alt.Count; i++)
            {
                var unten = Py.F(alt[i]["y"]) >= u.Unten - 1e-6;
                var oben = Py.F(alt[i]["y"]) + Py.F(alt[i]["h"]) <= u.Oben + 1e-6;
                // ein Raum, der den Flur ganz quert (Lager der Bitweiche), wird um den Zuwachs tiefer; sonst bleibt jeder gleich gross
                var quert = !unten && !oben && Py.F(alt[i]["y"]) <= u.Oben + 1e-6 && Py.F(alt[i]["y"]) + Py.F(alt[i]["h"]) >= u.Unten - 1e-6;
                Assert.True(unten || oben || quert, wo + ": Raum ragt in den Flur");
                Assert.Equal((Py.F(alt[i]["x"]), Py.F(alt[i]["w"]), Py.F(alt[i]["h"]) + (quert ? u.Zuwachs : 0)), (Py.F(jetzt[i]["x"]), Py.F(jetzt[i]["w"]), Py.F(jetzt[i]["h"])));
                Assert.Equal(Py.F(alt[i]["y"]) + (unten ? u.Zuwachs : 0), Py.F(jetzt[i]["y"]), 6);
            }
            // der alte Grundriss bleibt unveraendert (Kopie)
            Assert.Equal(u.Alt, Py.F(SpielDaten.Objekt(g["flur"])["h"]), 6);
            for (var y = 0.0; y <= Py.F(g["hoehe"]); y += 0.25)
            {
                Assert.Equal(y, u.Zurueck(u.Hin(y)), 6);
            }
        }
        // Gegenprobe: ohne Umrechnung (Stand A 1.1.0) bleibt der Flur schmal
        Flurbreite.AusFuerTests = true;
        try
        {
            Assert.False(Flurbreite.Gilt("buero", SpielDaten.OrtBuero));
        }
        finally
        {
            Flurbreite.AusFuerTests = false;
        }
    }

    /// <summary>Welle A 1.2.0: im gebauten Innenraum ist der Flur breit (Buero, Wohnung, Serverfarm), Laeden und Schalter bleiben.</summary>
    [Fact]
    public void Innenraum_Gebaut_FlurBreit_LaedenBleiben()
    {
        var stand = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        var bib = SpielGrafikTests.Bibliothek.Value;
        foreach (var (ansicht, ziel) in new[] { ("buero", 3.0), ("zuhause", 2.2), ("serverfarm", 3.0) })
        {
            var innen = Innenraum.Bauen(ansicht, stand, SpielInnenraumTests.Innenraeume.Value, bib)!;
            Assert.Equal(ziel, Py.F(SpielDaten.Objekt(innen.Gebaeude["flur"])["h"]), 6);
            Assert.True(innen.Umrechnung.Zuwachs > 0, ansicht);
            Assert.NotSame(innen.Gebaeude, innen.GrundrissAlt);
            // die Personen stehen im selben Raum wie im Grundriss des Spiel-Kerns
            foreach (var p in innen.Personen)
            {
                var raumNeu = Wege.RaumBei(p.X, p.Z, innen.Gebaeude)?["id"];
                var raumAlt = Wege.RaumBei(p.X, innen.Umrechnung.Zurueck(p.Z), innen.GrundrissAlt)?["id"];
                Assert.Equal(Py.Str(raumAlt ?? "flur"), Py.Str(raumNeu ?? "flur"));
            }
        }
        foreach (var ansicht in new[] { "geschaeft:moebelhaus", "schalter:bank" })
        {
            var innen = Innenraum.Bauen(ansicht, stand, SpielInnenraumTests.Innenraeume.Value, bib)!;
            Assert.Equal(0, innen.Umrechnung.Zuwachs);
            Assert.Same(innen.Gebaeude, innen.GrundrissAlt);
        }
    }

    [Theory]
    [InlineData(60f)]
    [InlineData(90f)]
    [InlineData(110f)]
    public void IchSicht_WaagrechtesBlickfeld_WieEingestellt_NormaleKameraBleibt(float grad)
    {
        var k = new Kamera(0, 0, 8) { IchBlickfeld = grad };
        // normale Kamera: 40 Grad senkrecht, unabhaengig vom Regler
        var normal = k.Projektion(1600, 1000);
        Assert.Equal(1f / MathF.Tan(20f * MathF.PI / 180f), normal.M22, 3);
        // ganz in der Ich-Sicht: waagrecht genau das eingestellte Blickfeld (quer, PC 16:10 und Handy 20:9)
        k.IchAnteil = 1f;
        foreach (var (b, h) in new[] { (1600f, 1000f), (2400f, 1080f) })
        {
            var p = k.Projektion(b, h);
            Assert.Equal(1f / MathF.Tan(grad * MathF.PI / 360f), p.M11, 3);
        }
        // Hochformat: das Blickfeld gilt fuer die lange (senkrechte) Seite, nie weiter
        var hoch = k.Projektion(1080, 2400);
        Assert.Equal(1f / MathF.Tan(grad * MathF.PI / 360f), hoch.M22, 3);
        // auf halbem Weg liegt es dazwischen
        k.IchAnteil = 0.5f;
        var halb = k.SichtfeldJetzt(1.6f);
        Assert.Equal(40f + ((Kamera.IchSenkrecht(grad, 1.6f) - 40f) * 0.5f), halb, 3);
    }

    [Fact]
    public void IchSicht_Blickfeld_Bereich_UndUnterschiedlich()
    {
        var k = new Kamera(0, 0, 8) { IchAnteil = 1f };
        Assert.Equal(75f, k.IchBlickfeld);
        k.IchBlickfeld = 20f;
        Assert.Equal(60f, k.IchBlickfeld);
        k.IchBlickfeld = 170f;
        Assert.Equal(110f, k.IchBlickfeld);
        // 60, 90 und 110 ergeben je ein anderes Bild
        var m = new[] { 60f, 90f, 110f }.Select(g => { k.IchBlickfeld = g; return k.Projektion(1600, 1000).M11; }).ToList();
        Assert.Equal(3, m.Distinct().Count());
        Assert.True(m[0] > m[1] && m[1] > m[2]);
    }

    [Fact]
    public void Regler_Speichern_Lesen_Vorgabe_UndKaputteWerte()
    {
        Assert.Equal("ich_blickfeld", Blickfeldbedienung.Schluessel);
        Assert.Equal((60, 75, 110, 5), (Blickfeldbedienung.Min, Blickfeldbedienung.Vorgabe, Blickfeldbedienung.Max, Blickfeldbedienung.Schritt));
        var werte = new JsonObject();
        Assert.Equal(75, Blickfeldbedienung.Lesen(werte));
        Blickfeldbedienung.Wert(werte, 105);
        Assert.Equal(105, (int)werte["ich_blickfeld"]!);
        Assert.Equal(105, Blickfeldbedienung.Lesen(JsonNode.Parse(werte.ToJsonString())!.AsObject()));
        Blickfeldbedienung.Wert(werte, 75);           // Vorgabe = kein Eintrag
        Assert.False(werte.ContainsKey("ich_blickfeld"));
        Blickfeldbedienung.Wert(werte, 200);          // ausserhalb: auf den Rand
        Assert.Equal(110, (int)werte["ich_blickfeld"]!);
        foreach (var kaputt in new[] { """{"ich_blickfeld":"weit"}""", """{"ich_blickfeld":30}""", """{"ich_blickfeld":500}""", """{"ich_blickfeld":true}""", """{"ich_blickfeld":null}""" })
        {
            Assert.Equal(75, Blickfeldbedienung.Lesen(JsonNode.Parse(kaputt)!.AsObject()));
        }
        Assert.Equal(90, Blickfeldbedienung.Lesen(JsonNode.Parse("""{"ich_blickfeld":88}""")!.AsObject()));
        using (Blickfeldbedienung.FuerTests(65))
        {
            Assert.Equal(65, Blickfeldbedienung.Grad);
        }
    }
}

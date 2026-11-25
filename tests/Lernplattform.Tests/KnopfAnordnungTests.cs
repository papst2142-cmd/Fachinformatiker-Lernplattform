using System.Text.Json.Nodes;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;
using Knopf = Lernplattform.Core.Spiel.KnopfAnordnung.Knopf;
using Platz = Lernplattform.Core.Spiel.KnopfAnordnung.Platz;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.9.0 (Sammelliste 450, Weisungen P12-375 bis P12-377, Nico 1a 2b 3a 4a 5a+b, Frage 1a „40 Punkte“): die sechs runden
/// Knoepfe am Handy und Tablet lassen sich verschieben und je Knopf 50 bis 150 % gross stellen (100 % = heute). Abnahmesaetze:
/// A1 ohne eigene Anordnung genau wie A 1.8.0; A2 gemerkt nach „Fertig“ und Neustart; A3 nie uebereinander, nie unter
/// Kopfzeile/Kerbe/Leiste, die Sticks finden immer Platz und beruehren keinen Knopf; A5 hoch und quer getrennt, andere Groesse
/// verschiebt nichts ins Unbedienbare; A8 Groesse je Knopf, Trefferflaeche immer mindestens 40 Punkte; A9 Raster schaltbar und
/// gemerkt, ohne Raster gelten alle Schutzregeln. Jede Regel mit Gegenprobe.
/// </summary>
public sealed class KnopfAnordnungTests
{
    /// <summary>Echte Bereiche der Spielflaeche (Fotos A 1.8.0) und weitere Groessen mit Leiste.</summary>
    public static readonly (string Name, Sticks.Bereich B)[] Bereiche =
    [
        ("Handy quer", new(832, 384, 64, 80, 367, 465)),
        ("Handy hoch", new(384, 832, 64, 80, 143, 241)),
        ("Tablet quer", new(1280, 800, 64, 80, 591, 689)),
        ("Tablet hoch", new(800, 1280, 64, 80, 351, 449)),
        ("Handy quer breite Leiste", new(832, 384, 64, 86, 326, 506)),
        ("Handy hoch ohne Leiste", new(384, 832, 64, 0)),
        ("Emulator", new(640, 320, 64, 80, 271, 369)),
        ("S24 quer", new(915, 412, 64, 60, 367, 547)),
        // Start-Test A 1.9.0: Handy hoch mit breiter Leiste (die Ecke des linken Sticks liegt nicht auf dem 2-Punkte-Raster)
        ("Handy hoch breite Leiste", new(384, 832, 64, 128, 12, 372)),
    ];

    private static Dictionary<Knopf, Platz> Bewegt(Dictionary<Knopf, Platz> a, Knopf k, double dx, double dy, int? prozent = null) =>
        new(a) { [k] = a[k] with { X = a[k].X + dx, Y = a[k].Y + dy, Prozent = prozent ?? a[k].Prozent } };

    [Fact]
    public void A1_Standard_wie_A180_und_ohne_eigene_Anordnung_null()
    {
        foreach (var (name, b) in Bereiche)
        {
            var s = KnopfAnordnung.Standard(b);
            var h = HaltungKnopf.Lage(b)!.Value;
            var l = HandlungKnopf.Lage(b)!.Value;
            var t = HandlungKnopf.Telefon(b)!.Value;
            Assert.Equal((h.Springen.X, h.Springen.Y), (s[Knopf.Springen].X, s[Knopf.Springen].Y));
            Assert.Equal((h.Knien.X, h.Knien.Y), (s[Knopf.Knien].X, s[Knopf.Knien].Y));
            Assert.Equal((h.Liegen!.Value.X, h.Liegen.Value.Y), (s[Knopf.Liegen].X, s[Knopf.Liegen].Y));
            Assert.Equal((l.Handlung.X, l.Handlung.Y), (s[Knopf.Handlung].X, s[Knopf.Handlung].Y));
            Assert.Equal((l.Ich.X, l.Ich.Y), (s[Knopf.Ich].X, s[Knopf.Ich].Y));
            Assert.Equal((t.X, t.Y), (s[Knopf.Telefon].X, s[Knopf.Telefon].Y));
            Assert.All(s.Values, p => Assert.Equal(100, p.Prozent));
            // 100 % = heutige Groesse
            var (g, k) = HaltungKnopf.Groessen(b.Breite, b.Hoehe);
            var (hg, hk) = HandlungKnopf.Groesse(b.Breite, b.Hoehe);
            Assert.Equal(g, KnopfAnordnung.Durchmesser(Knopf.Springen, b, 100));
            Assert.Equal(k, KnopfAnordnung.Durchmesser(Knopf.Knien, b, 100));
            Assert.Equal(k, KnopfAnordnung.Durchmesser(Knopf.Liegen, b, 100));
            Assert.Equal(hg, KnopfAnordnung.Durchmesser(Knopf.Handlung, b, 100));
            Assert.Equal(hk, KnopfAnordnung.Durchmesser(Knopf.Ich, b, 100));
            Assert.Equal(hk, KnopfAnordnung.Durchmesser(Knopf.Telefon, b, 100));
            Assert.True(KnopfAnordnung.IstStandard(s, b), name);
            // Gegenprobe: ein Knopf 8 Punkte daneben oder 105 % ist nicht mehr der Standard
            Assert.False(KnopfAnordnung.IstStandard(Bewegt(s, Knopf.Ich, 8, 0), b), name);
            Assert.False(KnopfAnordnung.IstStandard(Bewegt(s, Knopf.Ich, 0, 0, 105), b), name);
        }
        // ohne gespeicherte Anordnung: null = die Seite nimmt den Weg von A 1.8.0 (Sperren wie bisher, keine Flaechen)
        using (KnopfAnordnung.FuerTests([]))
        {
            Assert.Null(KnopfAnordnung.Wirksam(Bereiche[0].B, default, out var passt));
            Assert.False(passt);
        }
        // eine leere Sperrflaechen-Liste aendert die Sticks nicht (gleicher Bereich, gleiche Lage)
        var b0 = Bereiche[0].B;
        Assert.Equal(b0, b0 with { Flaechen = default });
        Assert.Equal(Sticks.Lage(700, 360, Sticks.Seite.Rechts, b0), Sticks.Lage(700, 360, Sticks.Seite.Rechts, b0 with { Flaechen = new([]) }));
    }

    [Fact]
    public void A8_Trefferflaeche_immer_mindestens_40_Punkte_je_Knopf_und_Groesse()
    {
        Assert.Equal(40, KnopfAnordnung.TrefferMin);
        var kleiner = 0;
        foreach (var (name, b) in Bereiche)
        {
            foreach (var k in KnopfAnordnung.Alle)
            {
                var d100 = KnopfAnordnung.Durchmesser(k, b, 100);
                for (var p = KnopfAnordnung.GroesseMin; p <= KnopfAnordnung.GroesseMax; p += KnopfAnordnung.GroesseSchritt)
                {
                    var d = KnopfAnordnung.Durchmesser(k, b, p);
                    Assert.Equal(d100 * p / 100.0, d, 9);
                    var t = KnopfAnordnung.Treffer(d);
                    Assert.True(t >= 40, $"{name} {k} {p} %: Treffer {t}");
                    Assert.True(t >= d, $"{name} {k} {p} %: Treffer kleiner als das Bild");
                    if (d < 40)
                    {
                        kleiner++;
                        Assert.Equal(40, t);
                    }
                }
            }
        }
        // Gegenprobe: es gibt Bilder unter 40 Punkten (z. B. Ich-Sicht bei 50 % = 20), die Trefferflaeche bleibt dort 40
        Assert.True(kleiner > 0);
        Assert.Equal(20, KnopfAnordnung.Durchmesser(Knopf.Ich, Bereiche[0].B, 50));
        // Regler: 50 bis 150 %, Schritt 5, Mitte 100
        Assert.Equal(50, KnopfAnordnung.GroesseBegrenzen(10));
        Assert.Equal(150, KnopfAnordnung.GroesseBegrenzen(400));
        Assert.Equal(105, KnopfAnordnung.GroesseBegrenzen(103));
        Assert.Equal(100, (KnopfAnordnung.GroesseMin + KnopfAnordnung.GroesseMax) / 2);
    }

    [Fact]
    public void A3_Schutzregeln_mit_Gegenproben()
    {
        foreach (var (name, b) in Bereiche)
        {
            var s = KnopfAnordnung.Standard(b);
            // ein Knopf in freier Mitte des Bilds ist erlaubt
            var mitte = KnopfAnordnung.NaechsteErlaubte(s, Knopf.Ich, new Platz(b.Breite / 2, b.Oben + ((b.Hoehe - b.Oben - b.Unten) / 3)), b)!.Value;
            Assert.Equal(KnopfAnordnung.Fehler.Keiner, KnopfAnordnung.PruefenEiner(s, Knopf.Ich, mitte, b));
            var leer = new Dictionary<Knopf, Platz>();
            // auf einem anderen Knopf
            Assert.Equal(KnopfAnordnung.Fehler.Ueberlappt, KnopfAnordnung.PruefenEiner(s, Knopf.Ich, s[Knopf.Springen], b));
            // ueber der Kopfzeile und ausserhalb
            Assert.Equal(KnopfAnordnung.Fehler.Aussen, KnopfAnordnung.PruefenEiner(leer, Knopf.Ich, mitte with { Y = b.Oben }, b));
            Assert.Equal(KnopfAnordnung.Fehler.Aussen, KnopfAnordnung.PruefenEiner(leer, Knopf.Ich, mitte with { X = 5 }, b));
            Assert.Equal(KnopfAnordnung.Fehler.Aussen, KnopfAnordnung.PruefenEiner(leer, Knopf.Ich, mitte with { Y = b.Hoehe - 5 }, b));
            // im sicheren Bereich des Geraets (Kerbe links 40): dort nicht, ohne Kerbe schon
            var amRand = mitte with { X = 30 };
            Assert.Equal(KnopfAnordnung.Fehler.Keiner, KnopfAnordnung.PruefenEiner(leer, Knopf.Ich, amRand, b));
            Assert.Equal(KnopfAnordnung.Fehler.Aussen, KnopfAnordnung.PruefenEiner(leer, Knopf.Ich, amRand, b, (40, 0, 0, 0)));
            // auf der Leiste
            if (b.Unten > 0)
            {
                var leiste = new Platz(b.LeisteBis > b.LeisteVon ? (b.LeisteVon + b.LeisteBis) / 2 : b.Breite / 2, b.Hoehe - (b.Unten / 2));
                Assert.Equal(KnopfAnordnung.Fehler.Leiste, KnopfAnordnung.PruefenEiner(leer, Knopf.Ich, leiste, b));
            }
            // die Luft rechnet mit der Trefferflaeche (Frage 1a): zwei Bilder bei 50 % beruehren sich nicht, die Trefferflaechen schon
            var ich = mitte with { Prozent = 50 };
            var handy = new Platz(mitte.X + 44, mitte.Y, 50);   // Bilder 20/23 Punkte, Abstand 44: Bild frei, Treffer 40 + 8 Luft nicht
            var zwei = new Dictionary<Knopf, Platz> { [Knopf.Ich] = ich };
            Assert.True(44 > (KnopfAnordnung.Durchmesser(Knopf.Ich, b, 50) / 2) + (KnopfAnordnung.Durchmesser(Knopf.Telefon, b, 50) / 2) + KnopfAnordnung.Luft);
            Assert.Equal(KnopfAnordnung.Fehler.Ueberlappt, KnopfAnordnung.PruefenEiner(zwei, Knopf.Telefon, handy, b));
            Assert.Equal(KnopfAnordnung.Fehler.Keiner, KnopfAnordnung.PruefenEiner(zwei, Knopf.Telefon, handy with { X = mitte.X + 48 }, b));
            _ = name;
        }
    }

    [Fact]
    public void A3_Sticks_finden_immer_Platz_und_weichen_jedem_Knopf_aus()
    {
        foreach (var (name, b) in Bereiche)
        {
            var s = KnopfAnordnung.Standard(b);
            Assert.True(KnopfAnordnung.StickPlatz(s, b, Sticks.Seite.Links), name);
            Assert.True(KnopfAnordnung.StickPlatz(s, b, Sticks.Seite.Rechts), name);
            // Gegenprobe: alle drei rechten Knoepfe quer ueber die rechte Haelfte unten verteilt lassen keinen ganzen Stick mehr zu
            var halb = b.Breite / 2;
            var unten = b.Hoehe - b.Unten - 40;
            var voll = new Dictionary<Knopf, Platz>(s);
            var reihe = new[] { Knopf.Springen, Knopf.Knien, Knopf.Liegen };
            for (var i = 0; i < reihe.Length; i++)
            {
                voll[reihe[i]] = new Platz(halb + ((i + 0.5) * halb / 3), unten, 150);
            }
            if (KnopfAnordnung.Pruefen(voll, b).Fehler is KnopfAnordnung.Fehler.Keiner)
            {
                Assert.Fail($"{name}: Gegenprobe ohne Fehler");
            }
            // die Sticks halten jede Knopf-Flaeche frei: Daumen auf einem Knopf setzt keinen Stick, ein Stick daneben beruehrt ihn nicht
            var eigen = Bewegt(s, Knopf.Springen, -40, -24);
            var mit = b with { Flaechen = KnopfAnordnung.Sperren(eigen, b) };
            foreach (var (k, p) in eigen)
            {
                Assert.True(mit.Gesperrt(p.X, p.Y), $"{name}: {k} nicht gesperrt");
                var seite = p.X < halb ? Sticks.Seite.Links : Sticks.Seite.Rechts;
                var r0 = KnopfAnordnung.Treffer(KnopfAnordnung.Durchmesser(k, b, p.Prozent)) / 2;
                var (sx, sy, sr) = Sticks.Lage(p.X + r0 + 4, Math.Min(p.Y + r0 + 4, b.Hoehe - b.Unten - 1), seite, mit);
                foreach (var q in mit.Flaechen.Alle)
                {
                    Assert.False(Sticks.KreisInRechteck(sx, sy, sr, q), $"{name}: Stick bei {k} liegt auf einem Knopf");
                }
            }
            // Gegenprobe: ohne die Flaechen liegt derselbe Stick auf dem verschobenen Springen-Knopf
            var sp = eigen[Knopf.Springen];
            var rs = KnopfAnordnung.Treffer(KnopfAnordnung.Durchmesser(Knopf.Springen, b, 100)) / 2;
            var ohne = b with { Sperre = default };
            var (ox, oy, or) = Sticks.Lage(sp.X + rs + 4, Math.Min(sp.Y + rs + 4, b.Hoehe - b.Unten - 1), Sticks.Seite.Rechts, ohne);
            Assert.True(KnopfAnordnung.Sperren(eigen, b).Alle.Any(q => Sticks.KreisInRechteck(ox, oy, or, q)), $"{name}: Gegenprobe ohne Flaechen");
        }
    }

    [Fact]
    public void A9_Raster_rastet_auf_8_ein_Hilfslinien_und_Schalter_gemerkt()
    {
        var p = KnopfAnordnung.Einrasten(new Platz(101.9, 59.5, 80));
        Assert.Equal(new Platz(104, 56, 80), p);
        Assert.Equal(0, p.X % KnopfAnordnung.Raster);
        var a = new Dictionary<Knopf, Platz> { [Knopf.Springen] = new(400, 200), [Knopf.Knien] = new(400, 300), [Knopf.Liegen] = new(500, 200) };
        Assert.Equal((400.0, 200.0), KnopfAnordnung.Hilfslinien(a, Knopf.Springen));
        Assert.Equal((null, null), KnopfAnordnung.Hilfslinien(new Dictionary<Knopf, Platz>(a) { [Knopf.Springen] = new(410, 210) }, Knopf.Springen));
        // Schalter: Vorgabe ein, nur „aus“ wird gespeichert, „ein“ entfernt den Eintrag wieder
        var werte = new JsonObject();
        Assert.True(KnopfAnordnung.RasterLesen(werte));
        KnopfAnordnung.RasterWert(werte, false);
        Assert.False(KnopfAnordnung.RasterLesen(werte));
        Assert.Equal("{\"knoepfe_raster\":false}", werte.ToJsonString());
        KnopfAnordnung.RasterWert(werte, true);
        Assert.True(KnopfAnordnung.RasterLesen(werte));
        Assert.Equal("{}", werte.ToJsonString());
        // ohne Raster gelten alle Schutzregeln weiter (die Pruefung kennt das Raster nicht)
        var b = Bereiche[0].B;
        var s = KnopfAnordnung.Standard(b);
        using (KnopfAnordnung.FuerTests(null, raster: false))
        {
            Assert.False(KnopfAnordnung.RasterAn);
            Assert.Equal(KnopfAnordnung.Fehler.Ueberlappt, KnopfAnordnung.PruefenEiner(s, Knopf.Ich, s[Knopf.Handlung] with { X = s[Knopf.Handlung].X + 3.3 }, b));
        }
        Assert.True(KnopfAnordnung.RasterAn);
    }

    [Fact]
    public void A2_A5_Speichern_Lesen_je_Ausrichtung_getrennt_und_Einpassen()
    {
        var quer = Bereiche[0].B;
        var hoch = Bereiche[1].B;
        Assert.Equal("quer", KnopfAnordnung.Ausrichtung(quer));
        Assert.Equal("hoch", KnopfAnordnung.Ausrichtung(hoch));
        var s = KnopfAnordnung.Standard(quer);
        var eigen = Bewegt(s, Knopf.Springen, 0, 0, 90);
        eigen[Knopf.Ich] = KnopfAnordnung.NaechsteErlaubte(eigen, Knopf.Ich, new Platz(quer.Breite / 2, quer.Oben + 60, 50), quer)!.Value;
        Assert.Equal((KnopfAnordnung.Fehler.Keiner, (Knopf?)null), KnopfAnordnung.Pruefen(eigen, quer));
        var gemerkt = KnopfAnordnung.Alle.ToDictionary(k => k, k => KnopfAnordnung.Merken(eigen[k], quer));
        var werte = new JsonObject { ["anderes"] = 1 };
        KnopfAnordnung.Wert(werte, "quer", gemerkt);
        var gelesen = KnopfAnordnung.Lesen(JsonNode.Parse(werte.ToJsonString())!.AsObject());
        Assert.True(gelesen.ContainsKey("quer"));
        Assert.False(gelesen.ContainsKey("hoch"));
        foreach (var k in KnopfAnordnung.Alle)
        {
            var o = KnopfAnordnung.Ort(gelesen["quer"][k], quer);
            Assert.True(Math.Abs(o.X - eigen[k].X) < 0.06 && Math.Abs(o.Y - eigen[k].Y) < 0.06, $"{k}");
            Assert.Equal(eigen[k].Prozent, o.Prozent);
        }
        // nach dem Neustart wirksam (gleiche Groesse), hoch bleibt Standard (3a)
        using (KnopfAnordnung.FuerTests(gelesen))
        {
            var w = KnopfAnordnung.Wirksam(quer, default, out var passt)!;
            Assert.False(passt);
            Assert.Equal(90, w[Knopf.Springen].Prozent);
            Assert.Equal(50, w[Knopf.Ich].Prozent);
            Assert.Null(KnopfAnordnung.Wirksam(hoch, default, out _));
            // anderer Bildschirm gleicher Ausrichtung: der Abstand zur naechsten Ecke bleibt (Springen unten rechts)
            var gross = Bereiche[2].B;
            var wg = KnopfAnordnung.Wirksam(gross, default, out var passtG)!;
            Assert.False(passtG);
            Assert.Equal(quer.Breite - eigen[Knopf.Springen].X, gross.Breite - wg[Knopf.Springen].X, 1);
            Assert.Equal(quer.Hoehe - eigen[Knopf.Springen].Y, gross.Hoehe - wg[Knopf.Springen].Y, 1);
            Assert.Equal(KnopfAnordnung.Fehler.Keiner, KnopfAnordnung.Pruefen(wg, gross).Fehler);
            // aufgeklappte, viel hoehere Leiste: was nicht mehr passt, rueckt nur in der Anzeige an die naechste erlaubte Stelle
            var hoheLeiste = quer with { Unten = 150 };
            if (KnopfAnordnung.Wirksam(hoheLeiste, default, out var passtL) is { } wl)
            {
                Assert.False(passtL);
                Assert.Equal(KnopfAnordnung.Fehler.Keiner, KnopfAnordnung.Pruefen(wl, hoheLeiste).Fehler);
            }
            else
            {
                Assert.True(passtL);
            }
            // viel zu kleines Bild: Standard mit Hinweis
            Assert.Null(KnopfAnordnung.Wirksam(new Sticks.Bereich(300, 160, 64, 80), default, out var passtK));
            Assert.True(passtK);
        }
        // kaputter Eintrag: diese Ausrichtung gilt als Standard (nie ein Absturz)
        var kaputt = JsonNode.Parse("{\"knoepfe_anordnung\":{\"quer\":{\"springen\":{\"ecke\":\"xx\"}},\"hoch\":5}}")!.AsObject();
        Assert.Empty(KnopfAnordnung.Lesen(kaputt));
        // Standard setzen entfernt den Eintrag, ist keine Ausrichtung mehr da, verschwindet der Schluessel
        KnopfAnordnung.Wert(werte, "quer", null);
        Assert.Equal("{\"anderes\":1}", werte.ToJsonString());
    }

    [Fact]
    public void A5_Standard_ist_erlaubt_und_naechste_erlaubte_Stelle()
    {
        foreach (var (name, b) in Bereiche)
        {
            var s = KnopfAnordnung.Standard(b);
            // der Standard selbst erfuellt alle Schutzregeln (sonst fiele eine eigene Anordnung mit einem verschobenen Knopf beim
            // Einpassen auf den Standard zurueck), und eine Anordnung mit einem verschobenen Knopf bleibt nach Speichern wirksam
            Assert.Equal((KnopfAnordnung.Fehler.Keiner, (Knopf?)null), KnopfAnordnung.Pruefen(s, b));
            var eins = new Dictionary<Knopf, Platz>(s);
            eins[Knopf.Telefon] = KnopfAnordnung.NaechsteErlaubte(s, Knopf.Telefon, new Platz(b.Breite / 2, b.Oben + 60), b)!.Value;
            var gemerkt = KnopfAnordnung.Alle.ToDictionary(k => k, k => KnopfAnordnung.Merken(eins[k], b));
            Assert.NotNull(KnopfAnordnung.Einpassen(gemerkt, b, default, out var passt));
            Assert.False(passt, name);
            // ein Knopf auf einen anderen gezogen: die naechste erlaubte Stelle liegt nahe und ist frei
            var p = KnopfAnordnung.NaechsteErlaubte(s, Knopf.Ich, s[Knopf.Handlung], b);
            Assert.NotNull(p);
            Assert.Equal(KnopfAnordnung.Fehler.Keiner, KnopfAnordnung.PruefenEiner(s, Knopf.Ich, p!.Value, b, ohneStick: true));
            _ = name;
        }
    }
}

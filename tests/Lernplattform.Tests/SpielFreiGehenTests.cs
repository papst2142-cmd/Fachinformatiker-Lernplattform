using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.95.0 (Sammelliste 385 „frei gehen“, Weisung P12-306, Kurzplan P12-187 Abschnitt 6): Gehen mit gehaltenen Tasten.
/// Nicos Ziele: (a) draussen nie auf ein gesperrtes Feld, Fahrbahn und Gleis nur an Uebergaengen (Antwort 5a, Regel 342), an
/// jeder Kachel der aktiven Stadt und der grossen Testkarte; (b) drinnen nie in Wand, Moebel oder Person und nie ueber die
/// Wandmitte; (c) Spielzeit wie beim Antippen: gleiche Schritte, genau ein Ereignis je Halt, keins drinnen (Antwort 3a);
/// (d) Fingerabdruecke unveraendert (eigene Tests, die hier mitlaufen); (e) Tastenbelegung: eigene W-Belegung behaelt Vorrang;
/// (f) Ich-Sicht (Bewegungsbilder in <c>IchSichtFigurTests</c>, hier Blick und Gehrichtung). Gegenproben je Regel ueber
/// <see cref="StadtSpiel.FreiGehenAltFuerTests"/> und <see cref="StadtSpiel.FreiHaltAltFuerTests"/> (AsyncLocal).
/// </summary>
[Collection("Haenger")]
public sealed class SpielFreiGehenTests
{
    private const double Dt = 1.0 / 30;

    // ---- Engine-Baustein Steuerung ----

    [Fact]
    public void Steuerung_RichtungAusKamera_DiagonaleNieSchneller()
    {
        // Kamera 0 Grad schaut nach -Z: W geht nach -Z, D nach +X
        var (x, z) = Steuerung.Richtung(1, 0, 0);
        Assert.Equal((0.0, -1.0), (Math.Round(x, 9), Math.Round(z, 9)));
        (x, z) = Steuerung.Richtung(0, 1, 0);
        Assert.Equal((1.0, 0.0), (Math.Round(x, 9), Math.Round(z, 9)));
        // gedreht um 90 Grad: W geht nach -X
        (x, z) = Steuerung.Richtung(1, 0, 90);
        Assert.Equal((-1.0, 0.0), (Math.Round(x, 9), Math.Round(z, 9)));
        // W+D schraeg, Laenge 1 (nie schneller); W+S heben sich auf
        var (vor, seit) = Steuerung.AusTasten(true, false, false, true);
        (x, z) = Steuerung.Richtung(vor, seit, 37);
        Assert.Equal(1.0, Math.Sqrt((x * x) + (z * z)), 9);
        (vor, seit) = Steuerung.AusTasten(true, true, false, false);
        Assert.Equal((0.0, 0.0), Steuerung.Richtung(vor, seit, 0));
        // Stick: Totzone, ab drei Vierteln voll, dazwischen gleichmaessig
        Assert.Equal((0.0, 0.0), Steuerung.AusStick(10, 0, 100));
        var voll = Steuerung.AusStick(0, -80, 100);
        Assert.Equal((1.0, 0.0), (Math.Round(voll.Vor, 9), Math.Round(voll.Seit, 9)));
        var halb = Steuerung.AusStick(45, 0, 100);
        Assert.Equal(0.5, halb.Seit, 9);
    }

    [Fact]
    public void Steuerung_GleitetAnDerWand_UeberspringtKeineDuenneWand_RastetNurErlaubtEin()
    {
        // Wand bei x >= 5: schraeg dagegen gleitet laengs (z waechst), nie hinueber
        bool Frei(double x, double z) => x < 5;
        var e = Steuerung.Schritt(4.99, 0, 0.7071, 0.7071, 1.0, Frei);
        Assert.True(e.Angestossen);
        Assert.True(e.X < 5 && e.Z > 0.5, $"{e.X} {e.Z}");
        // frontal: stehen bleiben
        var f = Steuerung.Schritt(4.99, 0, 1, 0, 1.0, Frei);
        Assert.True(f.Angestossen && f.Weg < 0.02 && f.X < 5);
        // eine Wand von 0,05 Breite wird bei jedem Tempo nie uebersprungen
        bool Duenn(double x, double z) => x is < 2 or > 2.05;
        Assert.True(Steuerung.Schritt(1.9, 0, 1, 0, 10, Duenn).X < 2);
        // Gegenprobe: ein einziger grosser Schritt ohne Teilschritte waere hinueber
        Assert.True(Duenn(1.9 + 10, 0));
        // Einrasten: nur, wo erlaubt, hoechstens der Rest (0,1)
        bool Spalt(double x, double z) => z < 0 || (x > 0.95 && x < 1.05);
        var zustand = new Steuerung.Zustand();
        zustand.Neu((0, 1), 0.1);
        var ohne = Steuerung.Schritt(0.9, -0.01, 0, 1, 0.5, Spalt, zustand, (_, _) => false);
        Assert.True(ohne.Z < 0, "ohne Erlaubnis kein Einrasten");
        var mit = Steuerung.Schritt(0.9, -0.01, 0, 1, 0.5, Spalt, zustand, (_, _) => true);
        Assert.True(mit.Z > 0.2 && Math.Abs(mit.X - 0.9) <= 0.1 + 1e-9, $"{mit.X} {mit.Z}");
        var zu = new Steuerung.Zustand();
        zu.Neu((0, 1), 0.1);
        Assert.True(Steuerung.Schritt(0.8, -0.01, 0, 1, 0.5, Spalt, zu, (_, _) => true).Z < 0, "weiter als 0,1 daneben rastet nicht ein");
    }

    // ---- (a) draussen ----

    private static StadtSpiel Fassung(long fassung) => SpielMitfahrtTests.StadtNachFassung(fassung);

    /// <summary>
    /// Zufallslauf an jeder (bei der Testkarte jeder dritten) Kachel: Richtung wechselt alle 0,5 s, die Lage wird je Bild alle
    /// 0,02 gegen die eigene Rechnung von <see cref="SpielQuerenTests.Verboten"/> und das Laufraster geprueft. Rueckgabe: Funde, Bilder.
    /// </summary>
    private static (List<string> Funde, int Bilder) Zufallslauf(StadtSpiel spiel, int bilderJeKachel, int jedeNte)
    {
        var sz = spiel.Szene;
        var funde = new List<string>();
        var bilder = 0;
        var zufall = new Random(385);
        var kacheln = Stadtteile.Alle(sz.Breite, sz.Tiefe).Where((_, i) => i % jedeNte == 0).ToList();
        foreach (var k in kacheln)
        {
            var mitte = ((k.Spalte + 0.5) * Stadtteile.Groesse, (k.Reihe + 0.5) * Stadtteile.Groesse);
            if (sz.Laufraster.NaechsteFreie(mitte.Item1, mitte.Item2) is not { } zelle)
            {
                continue;
            }
            var start = sz.Laufraster.Mitte(zelle);
            if (sz.Laufraster.FeinGesperrt(start.X, start.Y) || SpielQuerenTests.Verboten(sz, new Vector2((float)start.X, (float)start.Y)) is not null)
            {
                continue;
            }
            spiel.StelleFuerTests(start.X, start.Y);
            var (rx, rz) = (0.0, 0.0);
            for (var i = 0; i < bilderJeKachel; i++)
            {
                if (i % 15 == 0)
                {
                    var w = zufall.NextDouble() * Math.PI * 2;
                    (rx, rz) = (Math.Sin(w), Math.Cos(w));
                }
                var vorher = new Vector2(spiel.FigurX, spiel.FigurZ);
                spiel.FreiGehen(rx, rz, Dt);
                bilder++;
                var jetzt = new Vector2(spiel.FigurX, spiel.FigurZ);
                var n = Math.Max(1, (int)(Vector2.Distance(vorher, jetzt) / 0.02f));
                for (var j = 1; j <= n && funde.Count < 10; j++)
                {
                    var p = Vector2.Lerp(vorher, jetzt, j / (float)n);
                    if (SpielQuerenTests.Verboten(sz, p) is { } v)
                    {
                        funde.Add(string.Create(CultureInfo.InvariantCulture, $"Kachel {k.Spalte}/{k.Reihe}: {v} bei {p.X:F2}/{p.Y:F2}"));
                    }
                }
                if (!sz.Laufraster.KannStehen(jetzt.X, jetzt.Y) && funde.Count < 10)
                {
                    funde.Add(string.Create(CultureInfo.InvariantCulture, $"Kachel {k.Spalte}/{k.Reihe}: gesperrtes Feld bei {jetzt.X:F2}/{jetzt.Y:F2}"));
                }
            }
            spiel.FreiGehen(0, 0, 1);
        }
        return (funde, bilder);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Stadt1_Zufallslauf_NieAufGesperrtemFeld_FahrbahnNurAnUebergaengen_Gegenprobe(long fassung)
    {
        var (funde, bilder) = Zufallslauf(Fassung(fassung), 600, 1);
        Assert.True(bilder >= 5000, "zu wenige Bilder: " + bilder);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        StadtSpiel.FreiGehenAltFuerTests = true;
        try
        {
            var (alt, _) = Zufallslauf(Fassung(fassung), 600, 1);
            Assert.True(alt.Count > 0, "Gegenprobe: ohne Sperre laeuft die Figur ueber Fahrbahn, Gleis oder in Haeuser");
        }
        finally
        {
            StadtSpiel.FreiGehenAltFuerTests = false;
        }
    }

    [Fact]
    public void Testkarte256_Zufallslauf_NieAufGesperrtemFeld()
    {
        var spiel = new StadtSpiel(SpielTestkarteTests.Testkarte.Value.Stadt, SpielTestkarteTests.Testkarte.Value.Ablauf, null, SpielInnenraumTests.Innenraeume.Value);
        var (funde, bilder) = Zufallslauf(spiel, 300, 3);
        Assert.True(bilder >= 20000, "zu wenige Bilder: " + bilder);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    [Fact]
    public void NichtGeladeneKachel_Sperrt_Gegenprobe()
    {
        var spiel = Fassung(2);
        var sz = spiel.Szene;
        // eine freie Stelle mit freiem Weg nach +X von mindestens 2
        foreach (var zelle in Enumerable.Range(0, (int)(sz.Breite / 0.5)).SelectMany(s => Enumerable.Range(0, (int)(sz.Tiefe / 0.5)).Select(z => (s, z))))
        {
            var (x, z) = ((zelle.s * 0.5) + 0.25, (zelle.z * 0.5) + 0.25);
            if (!Enumerable.Range(0, 100).All(i => sz.Laufraster.KannStehen(x + (i * 0.02), z) && !sz.Laufraster.FeinGesperrt(x + (i * 0.02), z)))
            {
                continue;
            }
            var grenze = x + 1;
            spiel.GeladenAn = (px, _) => px < grenze;
            spiel.StelleFuerTests(x, z);
            for (var i = 0; i < 60; i++)
            {
                spiel.FreiGehen(1, 0, Dt);
            }
            Assert.True(spiel.FigurX < grenze, "die Figur geht auf eine nicht geladene Kachel");
            spiel.GeladenAn = null;
            for (var i = 0; i < 60; i++)
            {
                spiel.FreiGehen(1, 0, Dt);
            }
            Assert.True(spiel.FigurX > grenze, "Gegenprobe: geladen geht sie weiter");
            return;
        }
        Assert.Fail("keine freie Strecke gefunden");
    }

    // ---- (b) drinnen ----

    /// <summary>Funde eines Zufallslaufs in einem Innenraum: Mitte der Figur naeher als 0,12 an Wand oder Moebel, naeher als
    /// <see cref="StadtSpiel.AnsprechAbstand"/> an einer Person, ausserhalb des Hauses.</summary>
    private static List<string> InnenLauf(Innenraum innen, string name, int bilder)
    {
        var funde = new List<string>();
        var rechtecke = Wege.Hindernisse(innen.Lauf.Gebaeude);
        var (breite, hoehe) = (Py.F(innen.Lauf.Gebaeude["breite"]), Py.F(innen.Lauf.Gebaeude["hoehe"]));
        var zufall = new Random(347);
        double x = innen.StartX, z = innen.StartZ;
        var (rx, rz) = (0.0, 0.0);
        for (var i = 0; i < bilder && funde.Count < 5; i++)
        {
            if (i % 12 == 0)
            {
                var w = zufall.NextDouble() * Math.PI * 2;
                (rx, rz) = (Math.Sin(w), Math.Cos(w));
            }
            var (vx, vz) = (x, z);
            var e = Steuerung.Schritt(x, z, rx, rz, Dt * StadtSpiel.Tempo,
                (px, pz) => StadtSpiel.FreiGehenAltFuerTests || StadtSpiel.FreiDrinnen(innen, px, pz, vx, vz));
            (x, z) = (e.X, e.Z);
            if (Math.Abs(x - vx) + Math.Abs(z - vz) < 1e-12)
            {
                continue;
            }
            foreach (var (x1, y1, x2, y2) in rechtecke)
            {
                var dx = Math.Max(Math.Max(x1 - x, 0), x - x2);
                var dy = Math.Max(Math.Max(y1 - z, 0), z - y2);
                if (Math.Sqrt((dx * dx) + (dy * dy)) < 0.12)
                {
                    funde.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: in Wand oder Moebel bei {x:F2}/{z:F2}"));
                    break;
                }
            }
            if (x < 0 || z < 0 || x > breite || z > hoehe)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: ausserhalb bei {x:F2}/{z:F2}"));
            }
            foreach (var p in innen.Personen)
            {
                var a = Math.Sqrt(((p.X - x) * (p.X - x)) + ((p.Z - z) * (p.Z - z)));
                var vorher = Math.Sqrt(((p.X - vx) * (p.X - vx)) + ((p.Z - vz) * (p.Z - vz)));
                if (a < StadtSpiel.AnsprechAbstand - 1e-9 && a < vorher - 1e-9)
                {
                    funde.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: naeher an {p.Name} ({a:F2}) bei {x:F2}/{z:F2}"));
                    break;
                }
            }
        }
        return funde;
    }

    [Fact]
    public void JederInnenraum_Zufallslauf_NieInWandMoebelOderPerson_Gegenprobe()
    {
        var funde = new List<string>();
        var raeume = 0;
        foreach (var (name, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume).DistinctBy(r => r.Ansicht + "|" + r.Fest + "|" + r.Personen.Count + "|" + Wege.Hindernisse(r.Lauf.Gebaeude).Count))
            {
                raeume++;
                funde.AddRange(InnenLauf(innen, name + " " + innen.Ansicht, 400));
            }
        }
        Assert.True(raeume > 20, "zu wenige Raeume: " + raeume);
        Assert.True(funde.Count == 0, string.Join("\n", funde.Take(20)));
        StadtSpiel.FreiGehenAltFuerTests = true;
        try
        {
            var alt = SpielWandTests.AlleRaeume(SpielWandTests.Fassungen().First().Innen).Take(5).SelectMany(r => InnenLauf(r, r.Ansicht, 400)).ToList();
            Assert.True(alt.Count > 0, "Gegenprobe: ohne Sperre geht die Figur durch Waende");
        }
        finally
        {
            StadtSpiel.FreiGehenAltFuerTests = false;
        }
    }

    // ---- (c) Spielzeit ----

    /// <summary>Eine Richtung, in die von der Stelle der Figur mindestens <paramref name="weite"/> gerade frei ist.</summary>
    private static (double X, double Z) FreieRichtung(StadtSpiel s, double weite)
    {
        var sz = s.Szene;
        for (var g = 0; g < 360; g += 15)
        {
            var (rx, rz) = (Math.Sin(g * Math.PI / 180), Math.Cos(g * Math.PI / 180));
            if (Enumerable.Range(1, (int)(weite / 0.02)).All(i =>
                sz.Laufraster.KannStehen(s.FigurX + (rx * i * 0.02), s.FigurZ + (rz * i * 0.02))
                && !sz.Laufraster.FeinGesperrt(s.FigurX + (rx * i * 0.02), s.FigurZ + (rz * i * 0.02))
                && sz.Orte.All(o => Math.Abs(o.StehX - (s.FigurX + (rx * i * 0.02))) + Math.Abs(o.StehZ - (s.FigurZ + (rz * i * 0.02))) > 1.5)
                && sz.Questgeber.All(q => Math.Abs(q.X - (s.FigurX + (rx * i * 0.02))) + Math.Abs(q.Z - (s.FigurZ + (rz * i * 0.02))) > 1.5)))
            {
                return (rx, rz);
            }
        }
        throw new InvalidOperationException("keine freie Richtung");
    }

    /// <summary>Spiel an einem freien Platz draussen (weg vom Ort, ohne Weg), Zahl der Ereignisse.</summary>
    private static StadtSpiel Draussen(long fassung = 2)
    {
        var s = Fassung(fassung);
        var sz = s.Szene;
        var ort = sz.Orte.First(o => s.GeheZu(o.Id) is StadtGrund.Ok or StadtGrund.SchonDa);
        s.SofortAnkommen();
        // einen freien Punkt 4 weiter antippen und hingehen (ein Ereignis), dann frei
        foreach (var d in new[] { (4.0, 0.0), (-4.0, 0.0), (0.0, 4.0), (0.0, -4.0), (3.0, 3.0), (-3.0, -3.0) })
        {
            if (s.GeheZu(ort.StehX + d.Item1, ort.StehZ + d.Item2))
            {
                s.SofortAnkommen();
                if (sz.Orte.All(o => Math.Abs(o.StehX - s.FigurX) + Math.Abs(o.StehZ - s.FigurZ) > 1.5))
                {
                    return s;
                }
            }
        }
        throw new InvalidOperationException("kein freier Platz");
    }

    [Fact]
    public void Spielzeit_GleicheSchritteWieAngetippt_GenauEinEreignisJeHalt_Gegenprobe()
    {
        var s = Draussen();
        var (rx, rz) = FreieRichtung(s, 4);
        var zeilen = s.Ablauf.Stand.Verlauf.Count;
        var uhr = s.Jetzt.Uhr;
        var (x0, z0) = (s.FigurX, s.FigurZ);
        // 1 s W halten (3,5 weit), kurz loslassen (kein Halt), weiter, dann still
        for (var i = 0; i < 15; i++)
        {
            s.FreiGehen(rx, rz, Dt);
        }
        Assert.True(s.FreiUnterwegs);
        s.FreiGehen(0, 0, 0.1);
        Assert.Equal(zeilen, s.Ablauf.Stand.Verlauf.Count);
        // 28 Bilder statt 30: die Strecke liegt nicht genau auf einer Zellgrenze (sonst entscheidet die Rundung)
        for (var i = 0; i < 13; i++)
        {
            s.FreiGehen(rx, rz, Dt);
        }
        Assert.Equal(zeilen, s.Ablauf.Stand.Verlauf.Count);
        // die gleichen Schritte, die ein Tipp auf das gerade Ziel kosten wuerde (Strecke in Zellen, aufgerundet)
        var strecke = Math.Sqrt(Math.Pow(s.FigurX - x0, 2) + Math.Pow(s.FigurZ - z0, 2));
        var erwartet = (long)Math.Ceiling((strecke / s.Stadt.Zelle) - 1e-9);
        Assert.True(s.FreiGehen(0, 0, 0.2), "nach 0,2 s noch kein Halt");
        Assert.False(s.FreiGehen(0, 0, 0.2));
        Assert.Equal(zeilen + 1, s.Ablauf.Stand.Verlauf.Count);
        var e = s.Ablauf.Stand.Verlauf[^1];
        Assert.Equal(Stadt.Gelaufen, e.Art);
        Assert.Equal(erwartet, Py.Int(e.Daten["schritte"]));
        Assert.Equal(uhr + (erwartet * s.Stadt.MinutenJeSchritt), s.Jetzt.Uhr);
        Assert.True(s.ImFreien);
        // nichts mehr nach dem Halt
        s.FreiGehen(0, 0, 1);
        Assert.Equal(zeilen + 1, s.Ablauf.Stand.Verlauf.Count);
        // Gegenprobe: ein Ereignis je Bild
        var g = Draussen();
        (rx, rz) = FreieRichtung(g, 4);
        var gz = g.Ablauf.Stand.Verlauf.Count;
        StadtSpiel.FreiHaltAltFuerTests = true;
        try
        {
            for (var i = 0; i < 15; i++)
            {
                g.FreiGehen(rx, rz, Dt);
            }
            g.FreiGehen(0, 0, 1);
            Assert.True(g.Ablauf.Stand.Verlauf.Count > gz + 1, "Gegenprobe: mehr als ein Ereignis");
        }
        finally
        {
            StadtSpiel.FreiHaltAltFuerTests = false;
        }
    }

    [Fact]
    public void Halt_VorJederAnderenAktion_UndAmProgrammende()
    {
        var s = Draussen();
        var (rx, rz) = FreieRichtung(s, 3);
        var zeilen = s.Ablauf.Stand.Verlauf.Count;
        for (var i = 0; i < 10; i++)
        {
            s.FreiGehen(rx, rz, Dt);
        }
        // Antippen schreibt zuerst den Halt, dann seinen Weg
        Assert.True(s.GeheZu(s.FigurX - (rx * 2), s.FigurZ - (rz * 2)));
        Assert.Equal(zeilen + 2, s.Ablauf.Stand.Verlauf.Count);
        Assert.Equal(Stadt.Gelaufen, s.Ablauf.Stand.Verlauf[^2].Art);
        s.SofortAnkommen();
        // Programmende (alle Spiele)
        zeilen = s.Ablauf.Stand.Verlauf.Count;
        for (var i = 0; i < 10; i++)
        {
            s.FreiGehen(rx, rz, Dt);
        }
        StadtSpiel.AlleFreiHalten();
        Assert.Equal(zeilen + 1, s.Ablauf.Stand.Verlauf.Count);
        Assert.False(s.FreiOffen);
    }

    [Fact]
    public void Drinnen_KeinEreignis_KeineSpielzeit()
    {
        var s = Fassung(2);
        var ort = s.Szene.Orte.First(o => o.Ansicht == "zuhause");
        Assert.Contains(s.GeheZu(ort.Id), new[] { StadtGrund.Ok, StadtGrund.SchonDa });
        s.SofortAnkommen();
        Assert.True(s.Betreten());
        var zeilen = s.Ablauf.Stand.Verlauf.Count;
        var uhr = s.Jetzt.Uhr;
        var (x0, z0) = (s.FigurX, s.FigurZ);
        foreach (var (rx, rz) in new[] { (0.0, 1.0), (1.0, 0.0), (0.0, -1.0), (-1.0, 0.0) })
        {
            for (var i = 0; i < 20; i++)
            {
                s.FreiGehen(rx, rz, Dt);
            }
        }
        Assert.True(Math.Abs(s.FigurX - x0) + Math.Abs(s.FigurZ - z0) > 0.1 || s.FreiOffen, "die Figur geht drinnen nicht");
        s.FreiGehen(0, 0, 1);
        Assert.Equal(zeilen, s.Ablauf.Stand.Verlauf.Count);
        Assert.Equal(uhr, s.Jetzt.Uhr);
        Assert.True(s.ImInnenraum);
    }

    [Fact]
    public void VorDerTuer_EinWegZumOrt_HineingehenGeht()
    {
        var s = Fassung(2);
        var sz = s.Szene;
        // ein Ort, an dessen Stehplatz man 0,8 entfernt frei steht und gerade hingehen kann
        foreach (var ort in sz.Orte.Where(o => s.HatInnenraum(o.Id)))
        {
            foreach (var g in Enumerable.Range(0, 24).Select(i => i * 15.0))
            {
                var (rx, rz) = (Math.Sin(g * Math.PI / 180), Math.Cos(g * Math.PI / 180));
                var (sx, sz0) = (ort.StehX - (rx * 0.8), ort.StehZ - (rz * 0.8));
                if (!Enumerable.Range(0, 41).All(i => sz.Laufraster.KannStehen(sx + (rx * i * 0.02), sz0 + (rz * i * 0.02))
                    && !sz.Laufraster.FeinGesperrt(sx + (rx * i * 0.02), sz0 + (rz * i * 0.02))))
                {
                    continue;
                }
                if (sz.Orte.Any(o => o != ort && Math.Abs(o.StehX - ort.StehX) + Math.Abs(o.StehZ - ort.StehZ) < 1.2))
                {
                    continue;
                }
                s.StelleFuerTests(sx, sz0);
                var zeilen = s.Ablauf.Stand.Verlauf.Count;
                var ansagen = new List<string>();
                s.GehAnsage += ansagen.Add;
                for (var i = 0; i < 6; i++)
                {
                    s.FreiGehen(rx, rz, Dt);
                }
                s.FreiGehen(0, 0, 1);
                Assert.Equal(zeilen + 1, s.Ablauf.Stand.Verlauf.Count);
                var e = s.Ablauf.Stand.Verlauf[^1];
                Assert.Equal(Stadt.Gegangen, e.Art);
                Assert.Equal(ort.Id, s.Stadt.Lokal(Py.Str(e.Daten["nach"])));
                Assert.Contains(string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.VorDir, ort.Name), ansagen);
                // „Hineingehen“ (Eingabe) geht sofort
                Assert.True(s.Betreten(), "Hineingehen nach dem Halt vor der Tuer");
                Assert.True(s.ImInnenraum);
                return;
            }
        }
        Assert.Fail("kein Ort mit freiem Zugang gefunden");
    }

    [Fact]
    public void AngetippterWeg_EndetBeiGehtaste_SchritteBleiben()
    {
        var s = Draussen();
        Assert.True(s.GeheZu(s.FigurX + 6, s.FigurZ) || s.GeheZu(s.FigurX - 6, s.FigurZ) || s.GeheZu(s.FigurX, s.FigurZ + 6));
        var zeilen = s.Ablauf.Stand.Verlauf.Count;
        s.Fortschreiten(0.2);
        Assert.True(s.Geht);
        s.FreiGehen(0, -1, Dt);
        Assert.False(s.Geht);
        Assert.Equal(zeilen, s.Ablauf.Stand.Verlauf.Count);
        Assert.True(s.ImFreien);
    }

    [Fact]
    public void Anstossen_EineAnsageJeHalt()
    {
        var s = Draussen();
        var ansagen = new List<string>();
        s.GehAnsage += ansagen.Add;
        // gegen die naechste Sperre laufen (in eine Richtung, bis sie haengt), lange dagegen druecken
        var (rx, rz) = (1.0, 0.0);
        for (var i = 0; i < 600; i++)
        {
            s.FreiGehen(rx, rz, Dt);
        }
        var n = ansagen.Count(a => a == SpielProbeTexte.NichtWeiter);
        Assert.True(n <= 1, "mehr als eine Ansage je Halt: " + n);
    }

    [Fact]
    public void ImBus_KeinFreiesGehen_SitzendStehtSieAuf()
    {
        // sitzend (Bank an der Haltestelle): eine Gehtaste steht auf
        var ohne = SpielMitfahrtTests.Stadt1();
        var spiel = new StadtSpiel(ohne.Stadt, ohne.Ablauf, SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
        var h = spiel.Szene.Haltestellen[0];
        var v = Verkehr.FuerSpieltag(spiel.Szene, 1);
        SpielMitfahrtTests.Hingehen(spiel, v, h);
        Assert.True(spiel.Knopf(StadtSpiel.AktionHinsetzen));
        Assert.NotNull(spiel.Haltung);
        for (var i = 0; i < 10; i++)
        {
            spiel.FreiGehen(h.Fahrt.X, h.Fahrt.Y, Dt);
        }
        Assert.Null(spiel.Haltung);
        Assert.Null(spiel.AufDerBank);
    }

    // ---- (e) Tastenbelegung ----

    [Fact]
    public void Tasten_WasdStandard_EigeneBelegungBehaeltVorrang_NieStillVerloren()
    {
        var t = new Tastenbelegung();
        Assert.Equal(Tastenbelegung.GehVor, t.AufgabeFuer("W"));
        Assert.Equal(Tastenbelegung.GehLinks, t.AufgabeFuer("A"));
        Assert.Equal(Tastenbelegung.GehZurueck, t.AufgabeFuer("S"));
        Assert.Equal(Tastenbelegung.GehRechts, t.AufgabeFuer("D"));
        // Pfeiltasten bleiben wie bisher (drinnen Ansicht schieben)
        Assert.Equal(Tastenbelegung.Links, t.AufgabeFuer("Left"));
        // eigene W-Belegung aus einer frueheren Fassung (Start-Test belegt W fuer „Weiter weg“): sie gewinnt, Gehen vorwaerts ohne Taste
        var alt = Tastenbelegung.Lesen(new JsonObject { [Tastenbelegung.Schluessel] = new JsonObject { [Tastenbelegung.Weiter] = "W" } });
        Assert.Equal(Tastenbelegung.Weiter, alt.AufgabeFuer("W"));
        Assert.Equal([Tastenbelegung.OhneTaste], alt.Tasten(Tastenbelegung.GehVor));
        Assert.Equal(Tastenbelegung.GehLinks, alt.AufgabeFuer("A"));
        // gespeichert bleibt beides (Rundreise), nichts still verloren
        var wieder = Tastenbelegung.Lesen(new JsonObject { [Tastenbelegung.Schluessel] = alt.AlsJson() });
        Assert.Equal(alt.Eigen, wieder.Eigen);
        // ohne Gehtaste nennt der Hinweis keine Gehtasten
        Assert.DoesNotContain("gehen,", SpielProbeTexte.HinweisFuer(alt), StringComparison.Ordinal);
        Assert.Contains("W, A, S und D gehen", SpielProbeTexte.HinweisFuer(t), StringComparison.Ordinal);
        // die neue Aufgabe kann man wieder belegen (Pfeil hoch ist frei, wenn „hoch“ eine andere Taste bekommt)
        Assert.Equal(Tastenbelegung.Ergebnis.Ok, alt.Setzen(Tastenbelegung.GehVor, "Y", out _));
        Assert.Equal(Tastenbelegung.GehVor, alt.AufgabeFuer("Y"));
        // Gegenprobe: ohne die Regel (frische Belegung) waere W doppelt
        Assert.Equal(Tastenbelegung.Ergebnis.Doppelt, new Tastenbelegung().Setzen(Tastenbelegung.Weiter, "W", out var andere));
        Assert.Equal(Tastenbelegung.GehVor, andere);
    }

    // ---- Schalter „Antippen zum Gehen“ (Antwort 1c) ----

    [Fact]
    public void Schalter_AntippenZumGehen_StandardAn_AusGespeichert_KaputtGiltAlsStandard()
    {
        Assert.True(Gehbedienung.Lesen([]));
        var werte = new JsonObject();
        Gehbedienung.Wert(werte, false);
        Assert.Equal("{\"antippen_gehen\":false}", werte.ToJsonString());
        Assert.False(Gehbedienung.Lesen(werte));
        Gehbedienung.Wert(werte, true);
        Assert.Equal("{}", werte.ToJsonString());
        Assert.True(Gehbedienung.Lesen(werte));
        // kaputt oder fremd = Standard an (nie still aus)
        Assert.True(Gehbedienung.Lesen(new JsonObject { [Gehbedienung.Schluessel] = "nein" }));
        Assert.True(Gehbedienung.Lesen(new JsonObject { [Gehbedienung.Schluessel] = 0 }));
        Assert.True(Gehbedienung.Lesen(new JsonObject { [Gehbedienung.Schluessel] = true }));
    }

    // ---- (f) Ich-Sicht ----

    [Fact]
    public void IchSicht_KoerperBleibtImBlick_AuchRueckwaerts()
    {
        var s = Draussen();
        var (rx, rz) = FreieRichtung(s, 2);
        // rueckwaerts: Blick nach vorn (gegen die Gehrichtung), die Figur schaut in den Blick
        var blick = (float)(Math.Atan2(-rx, -rz) * 180 / Math.PI);
        for (var i = 0; i < 10; i++)
        {
            s.FreiGehen(rx, rz, Dt, blick);
        }
        Assert.Equal(blick, s.FigurRichtung, 3);
        Assert.True(s.SchrittBild >= 0, "Bewegungsbilder beim freien Gehen");
        // ohne Ich-Sicht dreht sie in die Gehrichtung
        for (var i = 0; i < 5; i++)
        {
            s.FreiGehen(rx, rz, Dt);
        }
        Assert.Equal((float)(Math.Atan2(rx, rz) * 180 / Math.PI), s.FigurRichtung, 3);
    }
}

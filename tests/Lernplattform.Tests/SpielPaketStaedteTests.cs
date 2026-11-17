using System.Globalization;
using System.IO.Compression;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.88.0 (Inhaltspakete W2, Weisung P12-296): Paket-Staedte im Spielkern. Abnahmesaetze W2-1 (mit dem Zug hin und
/// zurueck, in der Paket-Stadt gehen, warten, laufen), W2-3 (Vorsatz der Kennungen, kein Paket ueberdeckt Stadt 1; Gegenprobe
/// ohne Vorsatz), W2-4 (Ereignisse bleiben, Stand ohne Paket bleibt in der Paket-Stadt) und W2-5 (zweites Geraet ohne Paket:
/// kein Schritt, nichts verloren; Test mit zwei Geraeten wie FW8). Die Probe-Stadt ist das Paket <c>probe_klein</c> (Stadt 1 in
/// Fassung 0): gleiche Orte wie Stadt 1, darum zeigt die Gegenprobe ohne Vorsatz die Ueberdeckung.
/// </summary>
public sealed class SpielPaketStaedteTests
{
    private const string Kennung = "probe_klein";
    private const string Name = "Probe: kleine Stadt";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static byte[] Lesen(string pfad) => File.ReadAllBytes(Path.Combine(Pfade.Wurzel, pfad));

    /// <summary>Stadt 1 (geltende Fassung 2) wie im Spiel.</summary>
    internal static Stadt Stadt1()
    {
        var f = AktiveFassung.LadenNummer(p => File.Exists(Path.Combine(Pfade.Wurzel, p)) ? Lesen(p) : null, 2);
        return f.StadtFuer(f.MitKarte(Inhalt));
    }

    /// <summary>Die Stadt-Datei aus dem Probe-Paket (wie die Sitzung sie aus dem Datenordner liest).</summary>
    internal static byte[] PaketStadtDatei()
    {
        using var zip = new ZipArchive(new MemoryStream(Lesen("tests/Lernplattform.Tests/Testdaten/pakete_echt/probe_klein_1.fipaket")));
        using var strom = zip.GetEntry("stadt.fistadt")!.Open();
        using var kopie = new MemoryStream();
        strom.CopyTo(kopie);
        return kopie.ToArray();
    }

    internal static Stadt PaketStadt(Stadt haupt, bool mitVorsatz = true)
    {
        var f = StadtFassung.Lesen(PaketStadtDatei());
        var stadt = f.StadtFuer(f.MitKarte(Inhalt));
        return mitVorsatz ? stadt.AlsPaket(Kennung, Name, haupt) : stadt;
    }

    private static Spielablauf Neu(Stadt stadt1)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(stadt1.Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 1).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return spiel;
    }

    [Fact]
    public void W2_1_MitDemZugHinUndZurueck_InDerPaketStadtGehen_UhrUeberAlleStaedteGleich()
    {
        var stadt1 = Stadt1();
        var paket = PaketStadt(stadt1);
        var spiel = Neu(stadt1);
        // vom Bahnhof in Stadt 1 geht kein Zug, solange die Figur nicht dort ist
        Assert.Equal(StadtGrund.NichtAmBahnhof, stadt1.ZugPruefen(spiel.Stand, Stadt.BahnhofVon(Kennung), Name).Grund);
        Assert.True(stadt1.Gehen(spiel, Stadt.Bahnhof).Ok);
        var vorher = stadt1.Stand(spiel.Stand);
        var zug = stadt1.Zug(spiel, Stadt.BahnhofVon(Kennung), Name);
        Assert.True(zug.Ok, zug.Grund.ToString());
        Assert.Equal(Ereignisse.ZugGefahren, spiel.Stand.Verlauf[^1].Art);
        Assert.Equal(Stadt.Bahnhof, spiel.Stand.Verlauf[^1].Daten["von"]);
        Assert.Equal(Kennung + ":" + Stadt.Bahnhof, spiel.Stand.Verlauf[^1].Daten["nach"]);
        Assert.Equal(Name, spiel.Stand.Verlauf[^1].Daten["stadt"]);
        // beide Staedte sehen dieselbe Uhr; Stadt 1 sieht die Figur in der anderen Stadt, die Paket-Stadt an ihrem Bahnhof
        var inStadt1 = stadt1.Stand(spiel.Stand);
        var inPaket = paket.Stand(spiel.Stand);
        Assert.Equal(vorher.Uhr + Stadt.Zugminuten, inStadt1.Uhr);
        Assert.Equal(inStadt1.Uhr, inPaket.Uhr);
        Assert.True(stadt1.Fremd(inStadt1.Ort));
        Assert.Equal(Stadt.Bahnhof, inPaket.Ort);
        Assert.False(paket.Fremd(inPaket.Ort));
        Assert.Equal(Kennung + ":" + Stadt.Bahnhof, stadt1.OrtGlobal(spiel.Stand));
        // in Stadt 1 geht nun kein Schritt (die Figur ist nicht da)
        Assert.Equal(StadtGrund.AndereStadt, stadt1.GehenPruefen(spiel.Stand, "moebelhaus").Grund);
        Assert.Equal(StadtGrund.AndereStadt, stadt1.LaufenPruefen(spiel.Stand, 3).Grund);
        Assert.Equal(StadtGrund.AndereStadt, stadt1.MitfahrtPruefen(spiel.Stand, 5, 0, 1).Grund);
        // in der Paket-Stadt: zu einem Geschaeft gehen, frei laufen, mit dem Bus; die Orte tragen den Vorsatz
        var gang = paket.Gehen(spiel, "moebelhaus");
        Assert.True(gang.Ok, gang.Grund.ToString());
        Assert.Equal(Kennung + ":" + Stadt.Bahnhof, gang.Daten!["von"]);
        Assert.Equal(Kennung + ":moebelhaus", gang.Daten!["nach"]);
        Assert.Equal("moebelhaus", paket.Stand(spiel.Stand).Ort);
        Assert.Contains(paket.Betreten(spiel.Stand), new[] { StadtGrund.Ok, StadtGrund.Geschlossen });
        Assert.True(paket.Laufen(spiel, 4).Ok);
        Assert.Equal(Kennung + ":moebelhaus", spiel.Stand.Verlauf[^1].Daten["ort"]);
        // Wohnung, Buero, Kunden und Firma gibt es nur in Stadt 1 (E1)
        Assert.Equal(StadtGrund.Unbekannt, paket.GehenPruefen(spiel.Stand, "zuhause").Grund);
        Assert.Equal(StadtGrund.Unbekannt, paket.GehenPruefen(spiel.Stand, "bitweiche").Grund);
        Assert.True(paket.OhneInnenraum(Stadt.Bahnhof));
        Assert.False(stadt1.OhneInnenraum(Stadt.Bahnhof));
        // zurueck: erst zum Bahnhof, dann mit dem Zug nach Stadt 1
        Assert.Equal(StadtGrund.NichtAmBahnhof, paket.ZugPruefen(spiel.Stand, Stadt.Bahnhof, ZugTexte.Stadt1).Grund);
        Assert.True(paket.Gehen(spiel, Stadt.Bahnhof).Ok);
        Assert.Equal(StadtGrund.SchonDa, paket.ZugPruefen(spiel.Stand, Stadt.BahnhofVon(Kennung), Name).Grund);
        var uhr = paket.Stand(spiel.Stand).Uhr;
        Assert.True(paket.Zug(spiel, Stadt.Bahnhof, ZugTexte.Stadt1).Ok);
        Assert.Equal(new StadtStand(1, Stadt.Bahnhof, uhr + Stadt.Zugminuten), stadt1.Stand(spiel.Stand));
        Assert.True(paket.Fremd(paket.Stand(spiel.Stand).Ort));
        Assert.True(stadt1.Gehen(spiel, "moebelhaus").Ok);
        Assert.Equal("moebelhaus", stadt1.Stand(spiel.Stand).Ort);
        // W2-3: jede Zeile, die in der Paket-Stadt geschrieben wurde, traegt den Vorsatz; Stadt 1 schreibt nie einen
        foreach (var e in spiel.Stand.Verlauf.Where(e => e.Art is Stadt.Gegangen or Stadt.Gelaufen or Stadt.Gewartet))
        {
            var orte = new[] { "von", "nach", "ort" }.Select(k => SpielDaten.Get(e.Daten, k) as string).OfType<string>().ToList();
            Assert.True(orte.All(o => o.StartsWith(Kennung + ":", StringComparison.Ordinal)) || orte.All(o => !o.Contains(':', StringComparison.Ordinal)),
                string.Join(",", orte));
        }
    }

    [Fact]
    public void W2_3_Gegenprobe_OhneVorsatzUeberdecktDiePaketStadtStadt1()
    {
        var stadt1 = Stadt1();
        var ohne = PaketStadt(stadt1, mitVorsatz: false);
        var spiel = Neu(stadt1);
        // ohne Vorsatz schreibt die Paket-Stadt „moebelhaus“: Stadt 1 haelt die Figur fuer ihr eigenes Moebelhaus
        Assert.True(ohne.Gehen(spiel, "moebelhaus").Ok);
        Assert.Equal("moebelhaus", stadt1.Stand(spiel.Stand).Ort);
        Assert.False(stadt1.Fremd(stadt1.Stand(spiel.Stand).Ort));
        // mit Vorsatz bleibt Stadt 1 unberuehrt
        var mit = PaketStadt(stadt1);
        var zweites = Neu(stadt1);
        Assert.True(stadt1.Gehen(zweites, Stadt.Bahnhof).Ok);
        Assert.True(stadt1.Zug(zweites, Stadt.BahnhofVon(Kennung), Name).Ok);
        Assert.True(mit.Gehen(zweites, "moebelhaus").Ok);
        Assert.True(stadt1.Fremd(stadt1.Stand(zweites.Stand).Ort));
        Assert.Equal(Kennung + ":moebelhaus", stadt1.Stand(zweites.Stand).Ort);
        // zwei Pakete mit gleichen Orten ueberdecken sich nicht
        var anderes = StadtFassung.Lesen(PaketStadtDatei()).StadtFuer(StadtFassung.Lesen(PaketStadtDatei()).MitKarte(Inhalt)).AlsPaket("probe_zwei", "Zwei", stadt1);
        Assert.True(anderes.Fremd(anderes.Stand(zweites.Stand).Ort));
        Assert.Equal(StadtGrund.AndereStadt, anderes.GehenPruefen(zweites.Stand, "moebelhaus").Grund);
    }

    [Fact]
    public void W2_4_StandBleibtInDerPaketStadt_AuchOhnePaket_EreignisseBleiben_SperreOhneSchritt()
    {
        var stadt1 = Stadt1();
        var paket = PaketStadt(stadt1);
        var spiel = Neu(stadt1);
        Assert.True(stadt1.Gehen(spiel, Stadt.Bahnhof).Ok);
        Assert.True(stadt1.Zug(spiel, Stadt.BahnhofVon(Kennung), Name).Ok);
        Assert.True(paket.Gehen(spiel, "moebelhaus").Ok);
        var zeilen = spiel.Stand.Verlauf.Count;
        var global = stadt1.OrtGlobal(spiel.Stand);
        var uhr = stadt1.Stand(spiel.Stand).Uhr;
        // ohne Paket (nur Stadt 1): die Figur bleibt in der Paket-Stadt, Name aus der Zugfahrt, nichts geht verloren
        Assert.Equal(Kennung, Stadt.StadtVon(global));
        Assert.Equal(Name, Stadt.StadtName(spiel.Stand, Kennung));
        Assert.Equal(uhr, stadt1.Stand(spiel.Stand).Uhr);
        // die Sitzung sperrt jeden Spielschritt mit Grund (kein Schreiben)
        spiel.Sperre = () => ZugTexte.Format(ZugTexte.KeinSchritt, Name);
        var fehler = Assert.Throws<SpielFehler>(() => spiel.Ausfuehren([(Stadt.Gelaufen, new PyObjekt { { "tag", 1L }, { "ort", global }, { "schritte", 1L }, { "minuten", 1L } })]));
        Assert.Equal(ZugTexte.Format(ZugTexte.KeinSchritt, Name), fehler.Message);
        Assert.Equal(zeilen, spiel.Stand.Verlauf.Count);
        // Paket wieder da: es geht genau dort weiter
        spiel.Sperre = null;
        var wieder = PaketStadt(stadt1);
        Assert.Equal(new StadtStand(1, "moebelhaus", uhr), wieder.Stand(spiel.Stand));
        Assert.True(wieder.Gehen(spiel, Stadt.Bahnhof).Ok);
    }

    /// <summary>
    /// W2-5 mit zwei Geraeten wie FW8: der PC faehrt in die Paket-Stadt, das Handy hat das Paket nicht. Nach dem Abgleich sieht
    /// das Handy die Figur in der Paket-Stadt (gleiche Uhr), kann in Stadt 1 keinen Schritt tun; mit dem Paket geht es auf dem
    /// Handy am selben Ort weiter, und beide Geraete sehen danach denselben Stand.
    /// </summary>
    [Fact]
    public void W2_5_ZweitesGeraetOhnePaket_KeinSchritt_NichtsVerloren()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var n = 0;
        var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n)),
            () => "zug" + n.ToString("D6", CultureInfo.InvariantCulture));
        var stadt1 = Stadt1();
        var paket = PaketStadt(stadt1);
        const string LaufId = "lzug000000000001";
        Assert.True(schreiber.SpielPlatzSpeichern(1, LaufId, Spielstaende.Angelegt, "{}", "PC"));
        Spielablauf Geraet(string g) => new(stadt1.Inhalt, new SpielDatenbank(schreiber, g, LaufId, Vorgaenge.Sperren));
        var pc = Geraet("PC");
        pc.Profil("Nico", new PyObjekt());
        Assert.True(stadt1.Gehen(pc, Stadt.Bahnhof).Ok);
        Assert.True(stadt1.Zug(pc, Stadt.BahnhofVon(Kennung), Name).Ok);
        Assert.True(paket.Gehen(pc, "kaufhaus").Ok);
        var pcStand = paket.Stand(pc.Stand);

        var handy = Geraet("Handy");
        // ohne Paket: Ort in der Paket-Stadt, gleiche Uhr, kein Schritt in Stadt 1
        var ohne = stadt1.Stand(handy.Stand);
        Assert.Equal(Kennung + ":kaufhaus", ohne.Ort);
        Assert.Equal(pcStand.Uhr, ohne.Uhr);
        Assert.Equal(StadtGrund.AndereStadt, stadt1.GehenPruefen(handy.Stand, "moebelhaus").Grund);
        Assert.Equal(StadtGrund.AndereStadt, stadt1.WartenPruefen(handy.Stand).Grund);
        Assert.Equal(StadtGrund.AndereStadt, stadt1.ZugPruefen(handy.Stand, Stadt.BahnhofVon(Kennung), Name).Grund);
        // mit Paket auf dem Handy: am selben Ort weiter, der Schritt geht vom Stand des PCs aus (FW8-Merker)
        var gang = paket.GehenPruefen(handy.Stand, Stadt.Bahnhof);
        Assert.True(gang.Ok, gang.Grund.ToString());
        Assert.Equal(Kennung + ":kaufhaus", gang.Daten!["von"]);
        Assert.Equal(pcStand.Uhr, gang.Daten![Stadt.AbUhr]);
        handy.Log(gang.Art!, gang.Daten!);
        var handyStand = paket.Stand(handy.Stand);
        Assert.Equal(Stadt.Bahnhof, handyStand.Ort);
        // beide Geraete (neu gelesen) sehen denselben Stand
        Assert.Equal(handyStand, paket.Stand(Geraet("PC").Stand));
        Assert.Equal(handyStand, paket.Stand(Geraet("Handy").Stand));
        // die Zugfahrt ist eine Stadt-Zeile: FW8 ordnet sie einem Geraet zu
        using var verbindung = lernstand.Verbindung();
        Assert.Contains(Spielstaende.StadtGeraete(verbindung, LaufId), z => z.Art == Ereignisse.ZugGefahren && z.Geraet == "PC");
    }

    [Fact]
    public void Fahrplan_AmBahnhof_KnoepfeJeStadt_NurMitSpielfigur()
    {
        var stadt1 = Stadt1();
        var paket = PaketStadt(stadt1);
        var spiel = Neu(stadt1);
        Assert.True(stadt1.Gehen(spiel, Stadt.Bahnhof).Ok);
        var plan = new List<FahrplanStadt>
        {
            new(null, ZugTexte.Stadt1, true),
            new(Kennung, Name, true),
            new("probe_fehlt", "Lindenau", false, (StadtSpiel.AktionPaketLaden + "probe_fehlt", ZugTexte.Format(ZugTexte.PaketFehlt, "Lindenau", "0,51"))),
        };
        var s1 = new StadtSpiel(stadt1, spiel) { Fahrplan = () => plan };
        s1.FahrplanNeu();
        Assert.True(s1.AmBahnhof);
        Assert.Contains((StadtSpiel.AktionZug + Kennung, ZugTexte.Format(ZugTexte.ZugNach, Name)), s1.Knoepfe);
        Assert.Contains((StadtSpiel.AktionPaketLaden + "probe_fehlt", "Lindenau: Paket fehlt (0,51 MB) – Laden"), s1.Knoepfe);
        Assert.DoesNotContain(s1.Knoepfe, k => k.Aktion == StadtSpiel.AktionZug + StadtSpiel.ZugStadt1);
        Assert.Contains("Lindenau", s1.Zeile, StringComparison.Ordinal);
        Assert.Equal(StadtGrund.Ok, s1.ZugFahren(Kennung, Name));
        var s2 = new StadtSpiel(paket, spiel) { Fahrplan = () => plan };
        s2.FahrplanNeu();
        Assert.True(s2.AmBahnhof);
        Assert.False(s2.KannBetreten);
        Assert.Contains((StadtSpiel.AktionZug + StadtSpiel.ZugStadt1, ZugTexte.Format(ZugTexte.ZugNach, ZugTexte.Stadt1)), s2.Knoepfe);
        Assert.DoesNotContain(s2.Knoepfe, k => k.Aktion == StadtSpiel.AktionZug + Kennung);
        // die alte Stadt-1-Ansicht sieht die Figur in der anderen Stadt; fehlt das Paket, steht sie (nur gezeichnet) am Bahnhof
        var s3 = new StadtSpiel(stadt1, spiel);
        Assert.True(s3.InAndererStadt);
        s3.PaketFehlt(Kennung, Name);
        var bahnhof = s3.Szene.Ort(Stadt.Bahnhof)!;
        Assert.Equal((bahnhof.StehX, bahnhof.StehZ), (s3.FigurX, s3.FigurZ));
        Assert.Equal([(StadtSpiel.AktionPaketLaden + Kennung, ZugTexte.PaketLaden)], s3.Knoepfe);
        Assert.Equal(StadtGrund.AndereStadt, s3.GeheZu("moebelhaus"));
    }
}

/// <summary>
/// Welle A 0.88.0 (Weisung P12-296, Belastungsproben): jede Aktion der Liste <see cref="Aktionen"/> (darunter „Zug hin“ und
/// „Zug zurueck“) fuer jeden Ort der Probe-Stadt, an den Spieltagen 1 bis 3. Kein Absturz, nach jeder Aktion sehen beide Staedte
/// dieselbe Uhr und denselben Tag, ein gesperrter Schritt schreibt keine Zeile. Am Ende jedes Tages Feierabend in der Paket-Stadt:
/// der neue Tag beginnt zu Hause in Stadt 1. Gegenprobe: ohne Vorsatz meldet die Probe einen Fund.
/// </summary>
public sealed class SpielZugBelastungTests
{
    internal sealed record Lage(Stadt Stadt1, Stadt Paket, Spielablauf Spiel, string Ort);

    private const string Kennung = "probe_klein";

    /// <summary>Die Aktionen der Probe in ihrer Reihenfolge; jede gibt einen Fund zurueck oder null.</summary>
    internal static readonly (string Name, Func<Lage, string?> Tun)[] Aktionen =
    [
        ("Zum Bahnhof", l => l.Stadt1.Gehen(l.Spiel, Stadt.Bahnhof) is { Ok: true } || l.Stadt1.Stand(l.Spiel.Stand).Ort == Stadt.Bahnhof ? null : "nicht am Bahnhof"),
        ("Zug hin", l => l.Stadt1.Zug(l.Spiel, Stadt.BahnhofVon(Kennung), "Probe") is { Ok: true } ? null : "Zug faehrt nicht"),
        ("Stadt 1 gesperrt", l =>
        {
            var zeilen = l.Spiel.Stand.Verlauf.Count;
            var gruende = new[]
            {
                l.Stadt1.GehenPruefen(l.Spiel.Stand, "moebelhaus").Grund, l.Stadt1.LaufenPruefen(l.Spiel.Stand, 2).Grund,
                l.Stadt1.WartenPruefen(l.Spiel.Stand).Grund, l.Stadt1.Betreten(l.Spiel.Stand),
                l.Stadt1.ZugPruefen(l.Spiel.Stand, Stadt.BahnhofVon(Kennung), "Probe").Grund,
            };
            return gruende.All(g => g == StadtGrund.AndereStadt) && l.Spiel.Stand.Verlauf.Count == zeilen ? null : "Stadt 1: " + string.Join(",", gruende);
        }),
        ("Gehen", l => l.Ort == Stadt.Bahnhof || l.Paket.Gehen(l.Spiel, l.Ort) is { Ok: true } ? null : "geht nicht zu " + l.Ort),
        ("Betreten", l => l.Ort == Stadt.Bahnhof ? (l.Paket.OhneInnenraum(l.Ort) ? null : "Bahnhof mit Innenraum")
            : l.Paket.Betreten(l.Spiel.Stand) is StadtGrund.Ok or StadtGrund.Geschlossen or StadtGrund.OeffnetNichtMehr ? null : "Betreten " + l.Paket.Betreten(l.Spiel.Stand)),
        ("Laufen", l => l.Paket.Laufen(l.Spiel, 3) is { Ok: true } ? null : "laeuft nicht"),
        ("Mitfahrt", l => l.Paket.Mitfahrt(l.Spiel, 6, 0, 1) is { Ok: true } && l.Spiel.Stand.Verlauf[^1].Daten["ort"] as string == l.Paket.Global(l.Paket.Stand(l.Spiel.Stand).Ort) ? null : "Mitfahrt geht nicht"),
        ("Bus", l => l.Paket.Stand(l.Spiel.Stand).Ort == Stadt.Bahnhof || l.Paket.Bus(l.Spiel, Stadt.Bahnhof, 40, 12) is { Ok: true } ? null : "Bus zum Bahnhof geht nicht"),
        ("Warten", l => l.Paket.WartenPruefen(l.Spiel.Stand).Grund is var g && (g == StadtGrund.Ok ? l.Paket.Warten(l.Spiel).Ok : g != StadtGrund.AndereStadt) ? null : "Warten " + g),
        ("Zurueck zum Bahnhof", l => l.Paket.Stand(l.Spiel.Stand).Ort == Stadt.Bahnhof || l.Paket.Gehen(l.Spiel, Stadt.Bahnhof) is { Ok: true } ? null : "nicht am Bahnhof"),
        ("Zug zurueck", l => l.Paket.Zug(l.Spiel, Stadt.Bahnhof, ZugTexte.Stadt1) is { Ok: true } && l.Stadt1.Stand(l.Spiel.Stand).Ort == Stadt.Bahnhof ? null : "nicht in Stadt 1"),
        ("Paket-Stadt gesperrt", l =>
        {
            var zeilen = l.Spiel.Stand.Verlauf.Count;
            return l.Paket.GehenPruefen(l.Spiel.Stand, Stadt.Bahnhof).Grund == StadtGrund.AndereStadt && l.Spiel.Stand.Verlauf.Count == zeilen ? null : "Paket-Stadt nicht gesperrt";
        }),
    ];

    internal static List<string> Probe(long[] tage, bool mitVorsatz = true)
    {
        var funde = new List<string>();
        var stadt1 = SpielPaketStaedteTests.Stadt1();
        var paket = SpielPaketStaedteTests.PaketStadt(stadt1, mitVorsatz);
        var spiel = SpielPaketStaedteTestsNeu(stadt1);
        var orte = paket.Orte(spiel.Stand).Select(o => (string)o["id"]!).ToList();
        foreach (var tag in tage)
        {
            foreach (var ort in orte)
            {
                var lage = new Lage(stadt1, paket, spiel, ort);
                foreach (var (name, tun) in Aktionen)
                {
                    try
                    {
                        var fund = tun(lage);
                        var a = stadt1.Stand(spiel.Stand);
                        var b = paket.Stand(spiel.Stand);
                        fund ??= a.Uhr != b.Uhr || a.Tag != b.Tag || a.Tag != tag ? $"Staedte uneins: {a} / {b}" : null;
                        if (fund is not null)
                        {
                            funde.Add($"Tag {tag}, {ort}, {name}: {fund}");
                            Zurueck(stadt1, paket, spiel);
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        funde.Add($"Tag {tag}, {ort}, {name}: {e.GetType().Name} {e.Message}");
                        Zurueck(stadt1, paket, spiel);
                        break;
                    }
                }
            }
            // Feierabend in der Paket-Stadt: der naechste Tag beginnt zu Hause in Stadt 1
            if ((stadt1.Stand(spiel.Stand).Ort == Stadt.Bahnhof || stadt1.Gehen(spiel, Stadt.Bahnhof).Ok) && stadt1.Zug(spiel, Stadt.BahnhofVon(Kennung), "Probe").Ok)
            {
                spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", tag }, { "gehalt", 0L } });
                var neu = stadt1.Stand(spiel.Stand);
                if (neu != new StadtStand(tag + 1, stadt1.Heim, stadt1.Beginn))
                {
                    funde.Add($"Tag {tag}, Feierabend: neuer Tag beginnt nicht zu Hause in Stadt 1 ({neu})");
                }
            }
            else
            {
                funde.Add($"Tag {tag}, Feierabend: Zug faehrt nicht");
            }
        }
        return funde;
    }

    /// <summary>Nach einem Fund zurueck nach Stadt 1, damit die naechsten Orte eigene Funde zeigen (nicht nur Folgefunde).</summary>
    private static void Zurueck(Stadt stadt1, Stadt paket, Spielablauf spiel)
    {
        if (stadt1.Fremd(stadt1.Stand(spiel.Stand).Ort) && !paket.Fremd(paket.Stand(spiel.Stand).Ort))
        {
            paket.Gehen(spiel, Stadt.Bahnhof);
            paket.Zug(spiel, Stadt.Bahnhof, ZugTexte.Stadt1);
        }
    }

    private static Spielablauf SpielPaketStaedteTestsNeu(Stadt stadt1)
    {
        var spiel = new Spielablauf(stadt1.Inhalt, new SpielSpeicher(() => "2026-10-01 08:00:00"));
        spiel.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return spiel;
    }

    [Fact]
    public void JedeAktion_JederOrt_DreiTage_OhneFund()
    {
        Assert.Contains(Aktionen, a => a.Name == "Zug hin");
        Assert.Contains(Aktionen, a => a.Name == "Zug zurueck");
        var funde = Probe([1, 2, 3]);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    [Fact]
    public void Gegenprobe_OhneVorsatz_WirdGemeldet()
    {
        Assert.NotEmpty(Probe([1], mitVorsatz: false));
    }
}

using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Tests;

/// <summary>
/// Welle Fehlerpruefung Teil 2 (Weisung P12-345, Sammelliste 422 Gruppe 1 „Laden“): die Messpunkte „Inhalte laden“ und
/// „Karteikarten &lt;FR&gt; (erstes Oeffnen)“ lagen am Mac knapp ueber 400 ms. Ursache 1: die Inhalte-Dateien wurden
/// nacheinander gelesen. Ursache 2: die Reihenfolge der neuen Karten rechnete je Karte die Pruefungsphase (Termine lesen mit
/// regulaerem Ausdruck) neu, und das erste Oeffnen rechnete alle Werte. Jede Pruefung hat eine Gegenprobe mit dem alten Weg;
/// die Ergebnisse sind gleich (die Sollwerte G16 und der Vergleich der Inhalte bleiben gruen).
/// </summary>
public class LadenSchnellerTests
{
    private static string Abbild(Lerninhalte l) => string.Join("\n",
        new[] { l.Karteikarten, l.Quizfragen, l.Ap2Szenarien, l.Ap1Szenarien, l.Testprojekte }
            .Select(liste => string.Join("|", liste.Select(e => e.Art + ":" + e.Nummer + ":" + e.Daten.ToJsonString()))))
        + "\n" + l.AnzahlQuizfragenDatei.ToString(CultureInfo.InvariantCulture)
        + "\n" + string.Join(",", l.ThemenReihenfolge)
        + "\n" + string.Join(",", l.Themen.Select(p => p.Key + "=" + string.Join(";", p.Value.Select(t => t.Id + "/" + t.Name + "/" + t.Kurz + "/" + t.Fachbereich))))
        + "\n" + l.Rahmenplan.ToJsonString() + "\n" + l.RahmenplanZuordnung.ToJsonString();

    /// <summary>Quizfragen, Themen, Karteikarten, AP2, AP1, Testprojekte, Rahmenplan, Zuordnung.</summary>
    private const int TeileJeLaden = 8;

    private static (Lerninhalte Inhalte, int Hintergrund) Laden(bool nacheinander)
    {
        var zaehler = new System.Runtime.CompilerServices.StrongBox<int>();
        Lerninhalte.NacheinanderFuerTests.Value = nacheinander;
        Lerninhalte.HintergrundFuerTests.Value = zaehler;
        try
        {
            var inhalte = Lerninhalte.Laden(Pfade.Inhalte);
            // ein spaet begonnener Hintergrund-Teil zaehlt noch mit (er rechnet dann nichts mehr); nacheinander wird keiner uebergeben
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            while (!nacheinander && Volatile.Read(ref zaehler.Value) < TeileJeLaden && uhr.Elapsed < TimeSpan.FromSeconds(30))
            {
                Thread.Sleep(10);
            }
            Thread.Sleep(50);
            return (inhalte, Volatile.Read(ref zaehler.Value));
        }
        finally
        {
            Lerninhalte.NacheinanderFuerTests.Value = false;
            Lerninhalte.HintergrundFuerTests.Value = null;
        }
    }

    [Fact]
    public void InhalteLaden_DateienNebeneinander_GleichesErgebnisWieNacheinander()
    {
        var (neben, hintergrund) = Laden(nacheinander: false);
        Assert.Equal(TeileJeLaden, hintergrund);
        // Gegenprobe: der Weg bis A 1.3.0 (nacheinander) beginnt nichts im Hintergrund; Ergebnis gleich
        var (nach, keiner) = Laden(nacheinander: true);
        Assert.Equal(0, keiner);
        Assert.Equal(Abbild(nach), Abbild(neben));
        Assert.True(neben.Karteikarten.Count > 1000 && neben.Quizfragen.Count > 1000);
    }

    [Fact]
    public void InhalteLaden_Fehler_ErsterInDerReihenfolgeDerDateien_WieNacheinander()
    {
        // zwei kaputte Dateien: nebeneinander kommt derselbe (erste) Fehler wie nacheinander, nie einer aus dem Hintergrund verschluckt
        byte[] Lesen(string n) => n is "karteikarten" or "projektarbeiten" ? throw new IOException("Probe " + n) : File.ReadAllBytes(Path.Combine(Pfade.Inhalte, n + ".json"));
        foreach (var nacheinander in new[] { false, true })
        {
            Lerninhalte.NacheinanderFuerTests.Value = nacheinander;
            try
            {
                var fehler = Record.Exception(() => Lerninhalte.Laden(Lesen));
                Assert.Equal("Probe karteikarten", Assert.IsType<IOException>(fehler).Message);
            }
            finally
            {
                Lerninhalte.NacheinanderFuerTests.Value = false;
            }
        }
    }

    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);

    private static readonly DateOnly[] Tage = [new(2026, 6, 15), new(2027, 5, 1), new(2028, 1, 10)];

    private static IEnumerable<RahmenplanWerte> AlleWerte(RahmenplanGewicht plan) =>
    [
        RahmenplanWerte.Vorgabe,
        RahmenplanWerte.Aus(new JsonObject { ["rp_abschnitt_b"] = true, ["rp_abschnitt_d"] = true, ["rp_lernfeld"] = "LF 4" }, plan),
        RahmenplanWerte.Aus(new JsonObject { ["rp_termin_ap1"] = "01.03.2027", ["rp_termin_ap2"] = "2028-02-01" }, plan),
    ];

    [Fact]
    public void Reihenfolge_FesteTeileEinmalJeAufruf_GleicheReihenfolge()
    {
        var plan = new RahmenplanGewicht(Inhalte);
        var karten = Inhalte.Karteikarten.Select(k => k.Frage!).ToList();
        foreach (var tag in Tage)
        {
            foreach (var werte in AlleWerte(plan))
            {
                var vorher = plan.FestGerechnet;
                var reihe = plan.GewichteteReihenfolge(Quellen.Karte, karten, werte, tag);
                Assert.Equal(1, plan.FestGerechnet - vorher);
                // Gegenprobe: der alte Weg rechnet je Karte alles neu (Gewicht einzeln, je Aufruf feste Teile) und ergibt dieselbe Reihenfolge
                vorher = plan.FestGerechnet;
                var alt = karten.Select(s => (Wert: Core.Mathe.PythonPow.Pow(RahmenplanGewicht.Tageszufall(Quellen.Karte, s, tag), 1.0 / plan.Gewicht(Quellen.Karte, s, werte, tag)), Schluessel: s))
                    .OrderByDescending(x => x.Wert).Select(x => x.Schluessel).ToList();
                Assert.Equal(karten.Count, plan.FestGerechnet - vorher);
                Assert.Equal(alt, reihe);
            }
        }
    }

    [Fact]
    public void Reihenfolge_Vorab_ErstesOeffnenRechnetNichtsNeu_GleicheReihenfolge()
    {
        var plan = new RahmenplanGewicht(Inhalte);
        var ohne = new RahmenplanGewicht(Inhalte);
        var karten = Inhalte.Karteikarten.Select(k => k.Frage!).ToList();
        var fragen = Inhalte.Quizfragen.Select(k => k.Frage!).ToList();
        foreach (var tag in Tage)
        {
            foreach (var werte in AlleWerte(plan))
            {
                plan.Vorbereiten(Quellen.Karte, karten, werte, tag);
                plan.Vorbereiten(Quellen.Quiz, fragen, werte, tag);
                Assert.Equal(karten.Distinct().Count(), plan.VorabAnzahl(Quellen.Karte, werte, tag));
                var vorher = plan.WerteGerechnet;
                var reihe = plan.GewichteteReihenfolge(Quellen.Karte, karten, werte, tag);
                var quiz = plan.GewichteteReihenfolge(Quellen.Quiz, fragen, werte, tag);
                Assert.Equal(0, plan.WerteGerechnet - vorher);
                // Gegenprobe: ohne Vorab rechnet das erste Oeffnen jeden Wert, Ergebnis gleich
                vorher = ohne.WerteGerechnet;
                Assert.Equal(ohne.GewichteteReihenfolge(Quellen.Karte, karten, werte, tag), reihe);
                Assert.Equal(ohne.GewichteteReihenfolge(Quellen.Quiz, fragen, werte, tag), quiz);
                Assert.Equal(karten.Count + fragen.Count, ohne.WerteGerechnet - vorher);
            }
        }
        // ein anderer Tag oder andere Einstellungen nutzen die vorab gerechneten Werte nie
        Assert.Equal(0, plan.VorabAnzahl(Quellen.Karte, RahmenplanWerte.Vorgabe, new DateOnly(2026, 6, 16)));
        var vorher2 = plan.WerteGerechnet;
        plan.GewichteteReihenfolge(Quellen.Karte, karten, RahmenplanWerte.Vorgabe, new DateOnly(2026, 6, 16));
        Assert.Equal(karten.Count, plan.WerteGerechnet - vorher2);
    }
}

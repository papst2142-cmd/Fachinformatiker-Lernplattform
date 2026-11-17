using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.89.0 (Inhaltspakete W3, Weisung P12-297 W3-2, Plan P12-175 Abschnitt 4): Pakete liegen im Repo unter
/// <c>pakete/&lt;kennung&gt;/</c> als Quelle (<c>paket.json</c> ohne <c>dateien</c>, dazu die Dateien des Inhalts);
/// <c>werkzeuge/pakete/paket_bauen.py ordner</c> baut daraus die <c>.fipaket</c>-Datei (bytegleich, Selbsttest in der CI).
/// Hier prueft die App jedes Paket so, wie sie es nach dem Laden prueft (<see cref="Paket.Pruefen"/>), und das Spiel laedt
/// seine Stadt mit Vorsatz. Die Werkzeug-Pruefungen (beide Fundlisten, Grenzen) stehen in <c>Stadtbau.Tests/PaketeImRepoTests</c>.
/// Das Probe-Paket <c>probe_quests</c> (Stadt 1 in Fassung 2 mit zwei Auftraegen einer Baeckerin) erzeugt
/// <see cref="ProbeQuestsDatei"/>; neu schreiben mit <c>PAKETE_SCHREIBEN=1</c>.
/// </summary>
public sealed class PaketeImRepoTests
{
    internal const string ProbeQuests = "probe_quests";

    internal static string Ordner => Path.Combine(Pfade.Wurzel, "pakete");

    internal static IReadOnlyList<string> Kennungen =>
        [.. Directory.GetDirectories(Ordner).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    /// <summary>
    /// Die Stadt-Datei des Probe-Pakets: Stadt 1 in Fassung 2 als neue Stadt mit Paket-Abschnitt, dazu eine Questgeberin
    /// draussen vor dem Moebelhaus (Greta Sommer, freie Stelle laut Werkzeug) mit den zwei neuen Quests der Quest-Tests
    /// (Auswahl, danach Zuordnung). Fassung 2, weil das Werkzeug in Fassung 0 als neuer Stadt eine Laterne rot meldet.
    /// </summary>
    internal static byte[] ProbeQuestsDatei()
    {
        var f0 = StadtbauFassung2Tests.Fassung2.Value;
        var daten = (PyObjekt)Py.Lesen(PythonJson.Text(f0.Stadtdaten))!;
        var abschnitte = SpielQuestTests.Abschnitte();
        var greta = SpielDaten.Liste(abschnitte[Questebene.AbschnittQuestgeber]).Select(SpielDaten.Objekt).Single(q => Py.Gleich(q["id"], "neu-qg-1"));
        Py.Setzen(greta, "platz", new PyObjekt { { "draussen", true }, { "x", 15.4 }, { "y", 9.6 }, { "blick", 90L } });
        var quests = SpielDaten.Liste(abschnitte[Questebene.AbschnittQuests]).Select(SpielDaten.Objekt).Where(q => Py.Hat(q, "typ")).ToList();
        foreach (var q in quests)
        {
            Py.Setzen(q, "questgeber", "neu-qg-1");
        }
        Py.Setzen(daten, Questebene.AbschnittQuestgeber, new List<object?> { greta });
        Py.Setzen(daten, Questebene.AbschnittQuests, quests.Cast<object?>().ToList());
        Py.Setzen(daten, StadtPaket.Abschnitt, StadtPaket.Neu(ProbeQuests, "Probe: Stadt mit Aufträgen"));
        var f = new StadtFassung
        {
            StadtId = "neu_" + ProbeQuests, Nummer = 1, Basis = null, Werkzeug = "Fachinformatiker Stadtbau S 0.57.0", Erstellt = "2026-11-17T18:00:00+01:00",
            Nachricht = "Probe-Paket: Stadt 1 in Fassung 2 mit zwei Aufträgen von Greta Sommer (nur für Tests).",
            Karte = f0.Karte, Stadtdaten = daten, Innenraeume = f0.Innenraeume, Laternen = f0.Laternen, Beete = f0.Beete,
        };
        return f.Schreiben();
    }

    [Fact]
    public void ProbeQuests_StadtDatei_GleichDemErzeuger()
    {
        var datei = Path.Combine(Ordner, ProbeQuests, Paket.StadtDatei);
        var neu = ProbeQuestsDatei();
        if (Environment.GetEnvironmentVariable("PAKETE_SCHREIBEN") == "1")
        {
            File.WriteAllBytes(datei, neu);
        }
        Assert.Equal(neu, File.ReadAllBytes(datei));
    }

    /// <summary>Das Paket aus der Quelle im Repo, gepackt wie <c>paket_bauen.py</c> (Kopf mit Pruefsummen, Dateien ohne Ordner).</summary>
    internal static Dictionary<string, byte[]> Dateien(string kennung)
    {
        var ordner = Path.Combine(Ordner, kennung);
        var kopf = JsonNode.Parse(File.ReadAllText(Path.Combine(ordner, Paket.KopfDatei)))!.AsObject();
        var dateien = Directory.GetFiles(ordner).Where(d => Path.GetFileName(d) != Paket.KopfDatei)
            .ToDictionary(d => Path.GetFileName(d), File.ReadAllBytes, StringComparer.Ordinal);
        var angaben = new JsonObject();
        foreach (var (name, inhalt) in dateien.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            angaben[name] = new JsonObject { ["groesse"] = inhalt.LongLength, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(inhalt)) };
        }
        Assert.False(kopf.ContainsKey("dateien"), kennung + ": die Quelle nennt keine Dateien, paket_bauen.py setzt sie");
        kopf["dateien"] = angaben;
        dateien[Paket.KopfDatei] = Encoding.UTF8.GetBytes(kopf.ToJsonString());
        return dateien;
    }

    [Fact]
    public void JedesPaket_LadepruefungDerApp_KennungGleichOrdnerUndStadt_SpielLaedtMitVorsatz()
    {
        Assert.Contains(ProbeQuests, Kennungen);
        foreach (var kennung in Kennungen)
        {
            var dateien = Dateien(kennung);
            var ergebnis = Paket.Pruefen(dateien, "A 0.89.0");
            Assert.True(ergebnis.Kopf is not null, kennung + ": " + ergebnis.Grund);
            Assert.Equal(kennung, ergebnis.Kopf!.Kennung);
            Assert.Equal(Paket.ArtStadt, ergebnis.Kopf.Art);
            var f = StadtFassung.Lesen(dateien[Paket.StadtDatei]);
            Assert.Equal((kennung, ergebnis.Kopf.Name), StadtPaket.Von(f.Stadtdaten));
            Assert.Equal(4, StadtFassung.FormatFuer(f.Stadtdaten, f.Karte));
            Assert.Empty(StadtbauPruefung.Inhalt(f, SpielInhaltTests.Inhalt));
            // das Spiel laedt die Stadt wie die Sitzung (mit Vorsatz neben Stadt 1)
            var stadt = f.StadtFuer(f.MitKarte(SpielInhaltTests.Inhalt)).AlsPaket(kennung, ergebnis.Kopf.Name, SpielPaketStaedteTests.Stadt1());
            Assert.Equal(kennung + ":", stadt.Vorsatz);
            Assert.NotNull(stadt.Tuer(Stadt.Bahnhof));
        }
        // Gegenprobe: eine Stadt mit dem Abschnitt eines anderen Pakets lehnt die App mit Grund ab
        var falsch = Dateien(ProbeQuests);
        var kopf = JsonNode.Parse(falsch[Paket.KopfDatei])!.AsObject();
        kopf["kennung"] = "probe_andere";
        falsch[Paket.KopfDatei] = Encoding.UTF8.GetBytes(kopf.ToJsonString());
        Assert.Equal(PaketTexte.Format(PaketTexte.StadtAnderesPaket, ProbeQuests), Paket.Pruefen(falsch, "A 0.89.0").Grund);
    }

    /// <summary>
    /// Das mit <c>paket_bauen.py ordner</c> gebaute Probe-Paket liegt als echte Testdatei bei (der Selbsttest in der CI prueft,
    /// dass es bytegleich dem Bau ist); die App nimmt es an, und Kennung, Stand und Stadt sind die der Quelle.
    /// </summary>
    [Fact]
    public void ProbeQuests_GebautesPaket_AppNimmtEsAn_GleichDerQuelle()
    {
        var echt = Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/pakete_echt", Paket.Dateiname(ProbeQuests, 1));
        using var strom = File.OpenRead(echt);
        var ergebnis = Paket.PruefenArchiv(strom, "A 0.89.0");
        Assert.True(ergebnis.Kopf is not null, ergebnis.Grund);
        Assert.Equal((ProbeQuests, 1L), (ergebnis.Kopf!.Kennung, ergebnis.Kopf.Stand));
        strom.Position = 0;
        Assert.Equal(File.ReadAllBytes(Path.Combine(Ordner, ProbeQuests, Paket.StadtDatei)), Paket.Entpacken(strom).Dateien![Paket.StadtDatei]);
        // eine aeltere App lehnt es mit klarem Grund ab (braucht A 0.89.0)
        strom.Position = 0;
        Assert.Equal(PaketTexte.Format(PaketTexte.AppAlt, "A 0.89.0"), Paket.PruefenArchiv(strom, "A 0.88.0").Grund);
    }

    [Fact]
    public void ProbeQuests_ZweiAuftraege_NurInDerPaketStadt()
    {
        var f = StadtFassung.Lesen(File.ReadAllBytes(Path.Combine(Ordner, ProbeQuests, Paket.StadtDatei)));
        var stadt = f.StadtFuer(f.MitKarte(SpielInhaltTests.Inhalt)).AlsPaket(ProbeQuests, "Probe: Stadt mit Aufträgen", SpielPaketStaedteTests.Stadt1());
        Assert.Equal([ProbeQuests + ":neu-q-1", ProbeQuests + ":neu-q-2"], PaketQuests.Alle(stadt).Select(a => Py.Str(a["id"])));
    }
}

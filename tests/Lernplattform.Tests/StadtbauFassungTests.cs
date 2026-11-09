using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 13 Welle 1 (Weisung P12-16, Plan Abschnitt 6): "Stadt aus Daten". Fassung 0 von Stadt 1 ist bytegleich der Auszug aus
/// den drei Dateien und den bisher festen Werten (geschuetzt: aendert sich eine Quelle, ist <see cref="Fassung0_GleichDemAuszug"/>
/// rot); das Spiel liest sie ueber aktiv.json; die Szene aus der Fassung ist dieselbe wie aus den Dateien (Pruefsumme der ganzen
/// Szene in allen Jahres- und Festzeiten, Innenraeume, gespielter Stand) und dieselbe wie vor Welle 1 (Sollwerte vom Stand
/// main 42bbee7). Die Formatpruefung lehnt jede Abweichung ab (Gegenproben).
/// </summary>
public sealed class StadtbauFassungTests
{
    private static string Ordner => Path.Combine(Pfade.Wurzel, "inhalte_csharp", "spiel", "stadtbau");

    private static byte[]? Lesen(string pfad)
    {
        var datei = Path.Combine(Pfade.Wurzel, pfad);
        return File.Exists(datei) ? File.ReadAllBytes(datei) : null;
    }

    private static byte[] Quelle(params string[] teile) => File.ReadAllBytes(Path.Combine([Pfade.Wurzel, .. teile]));

    internal static readonly Lazy<StadtFassung> Fassung0 = new(() => AktiveFassung.Laden(Lesen));

    private static StadtFassung Auszug0() =>
        Auszug.Fassung0(Quelle("inhalte", "spiel", "orte.json"), Quelle("inhalte_csharp", "spiel", "stadt.json"), Quelle("inhalte_csharp", "spiel", "innenraeume.json"));

    /// <summary>
    /// Szene-Pruefsummen vor Welle 1 (main 42bbee7, Szene aus den Dateien und Nachtlicht.Skizze/Beete), Stand null; seit
    /// A 0.43.6 (Weisung P12-50) gelten sie fuer die Grafik je Dreieck (<see cref="GrafikBibliothek.JeDreieck"/>, so zeichnete
    /// das Spiel bis A 0.43.5), die vierte Spalte ist der Wert mit Farbverlauf je Ecke (Stand A 0.43.6, gleicher Rechner).
    /// Seit A 0.55.0 (Weisung P12-121, Nico 1b) gelten die dritte und vierte Spalte mit dem alten Schild
    /// (<see cref="StadtSzene.SchildAltFuerTests"/>, Gegenprobe); die fuenfte Spalte ist der Wert ohne Schild (Stand A 0.55.0).
    /// </summary>
    public static TheoryData<string?, Jahreszeit, string, string, string> StadtVorher => new()
    {
        { null, Jahreszeit.Fruehling, "472ec15a44777c5b53824c0e2e86e4c2223e9507a61641fddba7f051b460f370", "c1d39aee89bbb9f400767308f0befc4eb427a4fda4b6f895325105c850ed231b", "e465e633452227d94611e27c90bbfbf11cc7dabcd3b431241638a7e03ad4d877" },
        { null, Jahreszeit.Sommer, "05cc6125e56d9eb6724a0afbec0659b543830734063d487f33d30ae28605093c", "466943b29ce8de3dda64cae93f6a6033b789bafebca7cd3cdd6764a0b8bcd4fd", "b60d5c227dd2303380ae99aa05ec717ed3992cb86e391527e19af0af070ca9ff" },
        { null, Jahreszeit.Herbst, "7e787448537b1b55bf19ab4d011055daf9853276a4c8ccc5adefab5be77168b0", "2da33b72ad64afc66d8a44f37452fdded89769ec62946283ac950955ac6fdc27", "5f605c4becfc76c703f9e080f23c8164661725558f7a769ab20ead2988699eed" },
        { null, Jahreszeit.Winter, "33e96c79814a78d970631676c36ebb483e04726068da81ae90abd410eeb45426", "77fea78aff9f9e6e42ccabc1191239b6f59bc8670af6b48ef893c961c3faaf34", "1d350dc3e1be48a16e784d1ee9f329eed339a5e4310852a86c13248ad106c835" },
        { Festzeit.Ostern, Jahreszeit.Fruehling, "56d8c6f1d275a6fef20ddd13cbc4ae1eda494b470462401cad4e82051f513200", "98dfed7a47c8c626ce9b1d582f5fd01b353a069fa447cfda859851461a3a1ab8", "e7cf6758a0bd99f83dec49175bdaaec2277ab4c030a0264e29620f661d47aed2" },
        { Festzeit.Halloween, Jahreszeit.Herbst, "f235c68e6e254cf193d29c248f7207542b7c2be7057b011788d622178fbead6a", "1397eead9dded60c3c9eae60a602650c8de381076bd612a8ac686d8dc707f089", "beb0f69f97711ea36f73e27c79f02575da840b19860dfa2b7219c46b999e3bbd" },
        { Festzeit.Weihnachten, Jahreszeit.Winter, "25e7cc2364ccfeb00eba0405eed7232afa3b6a3e9f1305b6b94752ef66578574", "91a9a7299b2dd6fe360e5008e255b97987d9a09cd48f0bf6d124dc7368a77d73", "9463c5120c2f1920842e8fcebc4e8e453e0e275f07425fe426e9c4746744b707" },
        { Festzeit.Silvester, Jahreszeit.Winter, "13bd51e246bfcdfc4a0529cf490b4c192f1b5af188dc2e60299235612d82313b", "6acf713991c9cdd9cf59602491ef52db32da529e0c3e6290606d3db9885785df", "db847d2e9a1947cacb1ce745858cc390e1b25b5e12bac855e63b116126ea3cc1" },
    };

    /// <summary>Innenraeume vor Welle 1 (neuer Stand; "-gespielt" = Stand aus SpielFingerabdruckTests.Gespielt); Spalten wie <see cref="StadtVorher"/>.</summary>
    public static TheoryData<string, string, string> InnenVorher => new()
    {
        { "buero", "10ef0d96e19e75c6e1b45db27565ef106ef04da6d4e232ce4aa7a784bd851ff1", "10ef0d96e19e75c6e1b45db27565ef106ef04da6d4e232ce4aa7a784bd851ff1" },
        { "zuhause", "a25ed9d20481525d1c0cf9a5b0a8f848eb0f730e6610550dbf4d16ab4997057a", "a25ed9d20481525d1c0cf9a5b0a8f848eb0f730e6610550dbf4d16ab4997057a" },
        { "serverfarm", "735cb0b0f768d30352748aef5ddbb4798fae5605b2f96aaf0357c50163e3366a", "735cb0b0f768d30352748aef5ddbb4798fae5605b2f96aaf0357c50163e3366a" },
        { "geschaeft:moebelhaus", "2c2f0cefbe88f26280decbb255dc334ede359ac61870dfa7a56d3e5642fb1366", "2c2f0cefbe88f26280decbb255dc334ede359ac61870dfa7a56d3e5642fb1366" },
        { "geschaeft:elektromarkt", "261ee7c77f61ea62cc7e175dae38b84dd8e6b29295a89488e47c195c7dc86efc", "261ee7c77f61ea62cc7e175dae38b84dd8e6b29295a89488e47c195c7dc86efc" },
        { "geschaeft:kaufhaus", "0a2bdfd2cd738d6bf3d4496993c4711a0a342edd8388550f31a550614e5c1e77", "0a2bdfd2cd738d6bf3d4496993c4711a0a342edd8388550f31a550614e5c1e77" },
        { "schalter:bank", "fcaaa6e145fdf9dd33d605bd8dab86374c7090bc63cb3c11f7e4040a063cf652", "86bc7a5110d2ca43d510296a2803e5b2b3fe4a870a0328ce5aa18faaff9f76db" },
        { "schalter:amt", "3149dcb5f35a02b3d63387af7bf7680fef12d051a5c1547987fd51e18ea5bcfa", "be21fc35bfd882a98408d49fb091c45bf86d25f73d6206d11e4c2fd31c127c74" },
        { "kunde:talheim", "97a7e0f2db1e00a537a7739734deb7ae0810a18a3d846c441e33d316a9f6cfd0", "97a7e0f2db1e00a537a7739734deb7ae0810a18a3d846c441e33d316a9f6cfd0" },
        { "kunde:talheim_nord", "aa981220b9891a75389c925347a14c1128d8d91c144a6050090f4b1a4c47183a", "aa981220b9891a75389c925347a14c1128d8d91c144a6050090f4b1a4c47183a" },
        { "kunde:betriebswerkstatt", "9f905f2c97001deb2d9b8ec129e5acdb82c532b9f6802d1521a31ecb7779d409", "7ffbda2600cc5083da8c8984369e61949e75ee51d0a2284e47c968f42d784823" },
        { "kunde:lindenau", "9754bbaebb7b4a9f6ec64ff476bce8563c3ffbf1a71e38259fa0f04ed40859a9", "9754bbaebb7b4a9f6ec64ff476bce8563c3ffbf1a71e38259fa0f04ed40859a9" },
        { "kunde:birkenhain", "156a0ca0df7420b237b4f350401e3c4d6afc1b06fff32dc8818537950e232a30", "156a0ca0df7420b237b4f350401e3c4d6afc1b06fff32dc8818537950e232a30" },
        { "buero-gespielt", "15fafd96b4788c4608caad56dd035d80b6c02f060fe332bd1aef0c86cc19eecf", "15fafd96b4788c4608caad56dd035d80b6c02f060fe332bd1aef0c86cc19eecf" },
        { "filiale-gespielt", "c4aeab32876d912563acc1da8f600ba1a75a039e1f84cb189c7fdddf70c99ac8", "c4aeab32876d912563acc1da8f600ba1a75a039e1f84cb189c7fdddf70c99ac8" },
        { "zuhause-gespielt", "a25ed9d20481525d1c0cf9a5b0a8f848eb0f730e6610550dbf4d16ab4997057a", "a25ed9d20481525d1c0cf9a5b0a8f848eb0f730e6610550dbf4d16ab4997057a" },
    };

    /// <summary>Weisung P12-50: die Grafik je Dreieck (Darstellung bis A 0.43.5) fuer die Sollwerte von vorher.</summary>
    private static readonly Lazy<GrafikBibliothek> JeDreieck = new(() => SpielGrafikTests.Bibliothek.Value.JeDreieck());

    /// <summary>
    /// Die Sollwerte der Stadt sind auf dem Rechner der Aufnahme entstanden (Linux x64, Intel Xeon). Gleitkomma der
    /// 3D-Rechnung (Vektor-Befehle des Prozessors) weicht je Prozessor im letzten Bit ab: Der GitHub-Rechner (ubuntu-latest)
    /// ergibt in allen 9 Stadt-Faellen eigene Werte, die Innenraeume sind gleich. Die festen Werte gelten deshalb nur, wenn
    /// der unveraenderte Weg (Szene aus den Dateien und den festen Werten im Programm) hier denselben Wert wie bei der Aufnahme
    /// ergibt; auf jedem Rechner gilt dafuer <see cref="SzeneAusFassungGleichAusDateien"/> (beide Wege im selben Lauf).
    /// </summary>
    private static readonly Lazy<bool> GleitkommaWieAufnahme = new(() =>
        OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64
        && MitSchild(() => StadtSzeneHash.Stadt(SzeneAusDateien(null, Jahreszeit.Sommer, grafik: JeDreieck.Value))) == "05cc6125e56d9eb6724a0afbec0659b543830734063d487f33d30ae28605093c");

    /// <summary>A 0.55.0: <paramref name="f"/> mit dem alten Schild rechnen (Sollwerte bis A 0.54.0, Gegenprobe).</summary>
    internal static T MitSchild<T>(Func<T> f)
    {
        var vorher = StadtSzene.SchildAltFuerTests;
        StadtSzene.SchildAltFuerTests = true;
        try
        {
            return f();
        }
        finally
        {
            StadtSzene.SchildAltFuerTests = vorher;
        }
    }

    private static bool SollwertSystem => GleitkommaWieAufnahme.Value;

    private static StadtSzene SzeneAusDateien(string? fest, Jahreszeit j, Spielstand? stand = null, GrafikBibliothek? grafik = null) =>
        StadtSzene.Bauen(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), stand, grafik ?? SpielGrafikTests.Bibliothek.Value, fest, FestDekoTests.Daten.Value, j);

    private static StadtSzene SzeneAusFassung(string? fest, Jahreszeit j, Spielstand? stand = null, GrafikBibliothek? grafik = null)
    {
        var f = Fassung0.Value;
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(SpielInhaltTests.Inhalt)), stand, grafik ?? SpielGrafikTests.Bibliothek.Value, fest, FestDekoTests.Daten.Value, j);
    }

    [Fact]
    public void Fassung0_GleichDemAuszug()
    {
        var datei = Quelle("inhalte_csharp", "spiel", "stadtbau", StadtFassung.Dateiname(AktiveFassung.Stadt1, 0));
        var auszug = Auszug0().Schreiben();
        Assert.True(datei.AsSpan().SequenceEqual(auszug),
            "stadt1_fassung0.fistadt ist nicht mehr gleich dem Auszug aus orte.json, stadt.json, innenraeume.json und den Laternen/Beeten; "
            + "die heutige Stadt ist geschuetzt, Aenderungen nur als neue Fassung (Sollwert-Aenderung mit Nicos Freigabe)");
    }

    /// <summary>Die Abschnitte der Fassung 0 sind streng gleich den Quellen (json.dumps, 1 ungleich 1.0) und den festen Werten.</summary>
    [Fact]
    public void Fassung0_AbschnitteGleichDenQuellen()
    {
        var f = Fassung0.Value;
        Assert.Equal(AktiveFassung.Stadt1, f.StadtId);
        Assert.Equal(0, f.Nummer);
        Assert.Null(f.Basis);
        Assert.Equal(PythonJson.Standard(Py.Lesen(Quelle("inhalte", "spiel", "orte.json"))), PythonJson.Standard(f.Karte));
        Assert.Equal(PythonJson.Standard(Py.Lesen(Quelle("inhalte_csharp", "spiel", "stadt.json"))), PythonJson.Standard(f.Stadtdaten));
        Assert.Equal(PythonJson.Standard(Py.Lesen(Quelle("inhalte_csharp", "spiel", "innenraeume.json"))), PythonJson.Standard(f.Innenraeume));
        Assert.Equal(Nachtlicht.Skizze, f.Laternen);
        Assert.Equal(Nachtlicht.Beete, f.Beete);
        Assert.Equal(22, f.Laternen.Count);
        // die Karte der Fassung ist dieselbe wie die geladene Karte des Spiels (SpielInhalt aus inhalte/)
        Assert.Equal(PythonJson.Standard(SpielInhaltTests.Inhalt["orte"]), PythonJson.Standard(f.Karte));
    }

    [Fact]
    public void Fassung0_PruefungOhneFund()
    {
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(Quelle("inhalte_csharp", "spiel", "stadtbau", "stadt1_fassung0.fistadt"))));
        Assert.Empty(StadtbauPruefung.Inhalt(Fassung0.Value, SpielInhaltTests.Inhalt));
    }

    /// <summary>aktiv.json nennt genau Stadt 1, Fassung 0; im Ordner liegen nur aktiv.json und Fassungen.</summary>
    [Fact]
    public void AktivNenntFassung0()
    {
        var aktiv = (PyObjekt)Py.Lesen(Quelle("inhalte_csharp", "spiel", "stadtbau", AktiveFassung.Datei))!;
        Assert.Equal(0, AktiveFassung.Nummer(aktiv, AktiveFassung.Stadt1));
        Assert.Equal(["_hinweis", "stadt1"], aktiv.Select(p => p.Key));
        var dateien = Directory.GetFiles(Ordner).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["aktiv.json", "stadt1_fassung0.fistadt"], dateien);
    }

    /// <summary>Gelesen und wieder geschrieben ist die Datei bytegleich (kein Wert aendert beim Lesen seine Art oder Stelle).</summary>
    [Fact]
    public void LesenUndSchreibenBytegleich()
    {
        var datei = Quelle("inhalte_csharp", "spiel", "stadtbau", "stadt1_fassung0.fistadt");
        Assert.True(StadtFassung.Lesen(datei).Schreiben().AsSpan().SequenceEqual(datei));
    }

    [Theory]
    [MemberData(nameof(StadtVorher))]
    public void SzeneAusFassungGleichAusDateien(string? fest, Jahreszeit j, string vorher, string jetzt, string ohneSchild)
    {
        var ausFassung = StadtSzeneHash.Stadt(SzeneAusFassung(fest, j));
        Assert.Equal(StadtSzeneHash.Stadt(SzeneAusDateien(fest, j)), ausFassung);
        if (SollwertSystem)
        {
            Assert.Equal(vorher, MitSchild(() => StadtSzeneHash.Stadt(SzeneAusFassung(fest, j, grafik: JeDreieck.Value))));
            var mitSchild = MitSchild(() => StadtSzeneHash.Stadt(SzeneAusFassung(fest, j)));
            Assert.True(jetzt == mitSchild, "Stand A 0.43.6: " + mitSchild);
            Assert.True(ohneSchild == ausFassung, "Stand A 0.55.0: " + ausFassung);
        }
    }

    /// <summary>
    /// Phase 13 Welle 2 (Zusatz Chat zu Weisung P12-17): fester Wert der Stadt aus Fassung 0, der auf jedem Rechner gilt
    /// (auch auf den GitHub-Rechnern, deren Gleitkomma im letzten Bit abweicht): Ganzzahliges genau, Gleitkomma als Summen
    /// mit Toleranz 1e-6 je Wert (<see cref="StadtSzeneHash.StadtRechnerfest"/>). Sommer ohne Festzeit.
    /// </summary>
    [Fact]
    public void SzeneRechnerunabhaengig_FesterWert()
    {
        // Stand A 0.43.6 (Farbverlauf je Ecke): Geometrie und Ganzzahliges gleich (gleicher Hash), die Farbsummen neu;
        // gezeichnet je Dreieck (Gegenprobe) gelten die alten Summen weiter.
        // A 0.55.0 (Weisung P12-121): mit dem alten Schild gelten die bisherigen Werte (Gegenprobe), ohne Schild die neuen
        MitSchild(() =>
        {
            RechnerfestPruefen(SzeneAusFassung(null, Jahreszeit.Sommer), RechnerfestSummen, RechnerfestHash);
            RechnerfestPruefen(SzeneAusFassung(null, Jahreszeit.Sommer, grafik: JeDreieck.Value), RechnerfestSummenJeDreieck, RechnerfestHash);
            return 0;
        });
        RechnerfestPruefen(SzeneAusFassung(null, Jahreszeit.Sommer), RechnerfestSummenOhneSchild, RechnerfestHashOhneSchild);
    }

    private static void RechnerfestPruefen(StadtSzene szene, (string Netz, int Werte, double Summe, double Betraege)[] soll, string sollHash)
    {
        var (hash, summen) = StadtSzeneHash.StadtRechnerfest(szene);
        var text = string.Join("\n", summen.Select(s => string.Create(CultureInfo.InvariantCulture, $"(\"{s.Netz}\", {s.Werte}, {s.Summe:R}, {s.Betraege:R}),")));
        Assert.True(sollHash == hash, "Hash " + hash + "\n" + text);
        Assert.True(soll.Length == summen.Count, text);
        for (var i = 0; i < summen.Count; i++)
        {
            var (netz, werte, summe, betraege) = soll[i];
            Assert.Equal(netz, summen[i].Netz);
            Assert.Equal(werte, summen[i].Werte);
            Assert.True(Math.Abs(summe - summen[i].Summe) <= 1e-6 * werte && Math.Abs(betraege - summen[i].Betraege) <= 1e-6 * werte,
                string.Create(CultureInfo.InvariantCulture, $"{netz}: Summe {summen[i].Summe:R} (Soll {summe:R}), Betraege {summen[i].Betraege:R} (Soll {betraege:R})"));
        }
    }

    /// <summary>Stand A 0.55.0 (ohne Schild), Sommer, Farbverlauf je Ecke.</summary>
    private const string RechnerfestHashOhneSchild = "617286252aed87fdf219f3b30aa2ab5532d7e621b83eaa425b98737c2a12509a";

    private static readonly (string Netz, int Werte, double Summe, double Betraege)[] RechnerfestSummenOhneSchild =
    [
        ("stadt", 414612, 1372703.6164868153, 1427932.7092000921),
        ("haus:stellwerk#0", 8424, 30548.10409075847, 31880.311249242914),
        ("haus:zuhause", 122670, 672225.3644995703, 687474.079643785),
        ("haus:bitweiche", 171054, 678789.3083778784, 698513.5095457599),
        ("haus:talheim", 23742, 87682.90385822974, 90516.58266314254),
        ("haus:moebelhaus", 79686, 356870.552874337, 366605.3409091089),
        ("haus:elektromarkt", 279270, 1530092.074953526, 1562042.5535769162),
        ("haus:amt", 4446, 13133.225035086274, 13622.551111355424),
        ("haus:bank", 6264, 22379.477186098695, 23075.477186098695),
        ("haus:kaufhaus", 68832, 410625.3228750229, 420484.74333429337),
        ("gruen", 573912, 1767258.1505225452, 1853367.3578681687),
        ("paket-nah", 696384, 2397940.07526864, 2493014.076098815),
        ("paket-fern", 27000, 81547.90074151754, 84668.8502959609),
    ];

    private const string RechnerfestHash = "4cc4f16cff0f3ab7932ff13b19eee0095501695051706f7714ea62bbd5d098bc";

    private static readonly (string Netz, int Werte, double Summe, double Betraege)[] RechnerfestSummenJeDreieck =
    [
        ("stadt", 414612, 1372687.1576774246, 1427916.2503907015),
        ("haus:stellwerk#0", 8424, 30548.10409075847, 31880.311249242914),
        ("haus:zuhause", 122670, 672228.839014919, 687477.5541591337),
        ("haus:bitweiche", 171270, 679673.5366078183, 699421.7377756998),
        ("haus:talheim", 23742, 87682.90385822974, 90516.58266314254),
        ("haus:moebelhaus", 79902, 357871.5418382092, 367630.3298729811),
        ("haus:elektromarkt", 279486, 1531297.3462660608, 1563271.824889451),
        ("haus:amt", 4662, 13803.735612288117, 14317.061688557267),
        ("haus:bank", 6480, 23133.987797632813, 23853.987797632813),
        ("haus:kaufhaus", 69048, 411912.6019895077, 421796.02244877815),
        ("gruen", 573912, 1767293.4133009168, 1853402.6206465403),
        ("paket-nah", 696384, 2397953.4632801553, 2493027.46411033),
        ("paket-fern", 27000, 81547.90074151754, 84668.8502959609),
    ];

    private static readonly (string Netz, int Werte, double Summe, double Betraege)[] RechnerfestSummen =
    [
        ("stadt", 414612, 1372703.6164868153, 1427932.7092000921),
        ("haus:stellwerk#0", 8424, 30548.10409075847, 31880.311249242914),
        ("haus:zuhause", 122670, 672225.3644995703, 687474.079643785),
        ("haus:bitweiche", 171270, 679672.0072188542, 699420.2083867356),
        ("haus:talheim", 23742, 87682.90385822974, 90516.58266314254),
        ("haus:moebelhaus", 79902, 357869.8634355171, 367628.65147028904),
        ("haus:elektromarkt", 279486, 1531297.785466641, 1563272.2640900312),
        ("haus:amt", 4662, 13803.735612288117, 14317.061688557267),
        ("haus:bank", 6480, 23133.987797632813, 23853.987797632813),
        ("haus:kaufhaus", 69048, 411924.63337898254, 421808.053838253),
        ("gruen", 573912, 1767258.1505225452, 1853367.3578681687),
        ("paket-nah", 696384, 2397940.07526864, 2493014.076098815),
        ("paket-fern", 27000, 81547.90074151754, 84668.8502959609),
    ];

    [Fact]
    public void SzeneGespieltGleichVorher()
    {
        var gespielt = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var ausFassung = StadtSzeneHash.Stadt(SzeneAusFassung(null, Jahreszeit.Sommer, gespielt));
        Assert.Equal(StadtSzeneHash.Stadt(SzeneAusDateien(null, Jahreszeit.Sommer, gespielt)), ausFassung);
        if (SollwertSystem)
        {
            Assert.Equal("dcc4874347278bfdfc4782957fda4f8d063ec4ad1cd6d2c0017022e6b337f8bd", MitSchild(() => StadtSzeneHash.Stadt(SzeneAusFassung(null, Jahreszeit.Sommer, gespielt, JeDreieck.Value))));
            var mitSchild = MitSchild(() => StadtSzeneHash.Stadt(SzeneAusFassung(null, Jahreszeit.Sommer, gespielt)));
            Assert.True("a49d46342512a6e616cc36b494caf150f891a4e897bff8a50c201848c5d7b0d7" == mitSchild, "Stand A 0.43.6: " + mitSchild);   // mit Farbverlauf
            Assert.True("b94c1977dbc0f28a5a4cbe46a9d2922a4d18daa9e7a281a4178e096aaad7f801" == ausFassung, "Stand A 0.55.0: " + ausFassung);   // ohne Schild
        }
    }

    [Theory]
    [MemberData(nameof(InnenVorher))]
    public void InnenraumAusFassungGleichVorher(string name, string vorher, string jetzt)
    {
        var gespielt = name.EndsWith("-gespielt", StringComparison.Ordinal);
        var ansicht = gespielt ? name[..^"-gespielt".Length] : name;
        var stand = gespielt ? SpielFingerabdruckTests.Gespielt.Value.Stand : new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        string Hash(PyObjekt innenraeume, GrafikBibliothek? grafik = null) =>
            Innenraum.Bauen(ansicht, stand, innenraeume, grafik ?? SpielGrafikTests.Bibliothek.Value, SpielGeschaefteTests.Daten.Value) is { } innen
                ? StadtSzeneHash.Innen(innen) : "null";
        var ausFassung = Hash(Fassung0.Value.Innenraeume);
        Assert.Equal(Hash(SpielInnenraumTests.Innenraeume.Value), ausFassung);
        if (SollwertSystem)
        {
            Assert.Equal(vorher, Hash(Fassung0.Value.Innenraeume, JeDreieck.Value));
            Assert.True(jetzt == ausFassung, "Stand A 0.43.6: " + ausFassung);
        }
    }

    /// <summary>Gegenprobe: eine andere Laterne in der Fassung aendert die Szene (die Darstellung liest wirklich die Fassung).</summary>
    [Fact]
    public void Gegenprobe_LaterneAusFassungWirkt()
    {
        var f = Fassung0.Value;
        var laternen = f.Laternen.ToList();
        laternen[0] = (laternen[0].X + 1f, laternen[0].Z, laternen[0].Doppelt);
        var anders = new StadtFassung
        {
            StadtId = f.StadtId, Nummer = 1, Basis = 0, Werkzeug = f.Werkzeug, Erstellt = f.Erstellt, Nachricht = f.Nachricht,
            Karte = f.Karte, Stadtdaten = f.Stadtdaten, Innenraeume = f.Innenraeume, Laternen = laternen, Beete = [.. f.Beete.Select(b => b + new Vector2(0.5f, 0))],
        };
        var szene = StadtSzene.Bauen(anders.StadtFuer(anders.MitKarte(SpielInhaltTests.Inhalt)), null, SpielGrafikTests.Bibliothek.Value, null, FestDekoTests.Daten.Value, Jahreszeit.Sommer);
        Assert.NotEqual(StadtSzeneHash.Stadt(SzeneAusDateien(null, Jahreszeit.Sommer)), StadtSzeneHash.Stadt(szene));
        Assert.NotEqual(Nachtlicht.Skizze[0].X, szene.LaternenSkizze[0].X);
    }

    /// <summary>Gegenproben der Formatpruefung: jede Abweichung ist ein Fund, die Datei wird nicht gelesen.</summary>
    [Theory]
    [InlineData("pruefsumme", "Die Prüfsumme stimmt nicht")]
    [InlineData("unbekannt", "Unbekanntes Feld „extra“")]
    [InlineData("fehlt", "Feld „beete“ fehlt")]
    [InlineData("format", "keine Stadt-Datei")]
    [InlineData("neuer", "neueren Fassung des Formats")]
    [InlineData("id", "„stadt.id“")]
    [InlineData("laterne", "„laternen“ Nr. 1")]
    [InlineData("zahl", "„stadt.fassung“")]
    public void Gegenprobe_FormatLehntAb(string art, string erwartet)
    {
        var text = System.Text.Encoding.UTF8.GetString(Quelle("inhalte_csharp", "spiel", "stadtbau", "stadt1_fassung0.fistadt"));
        string Ersetze(string a, string b)
        {
            Assert.Contains(a, text, StringComparison.Ordinal);
            return text.Replace(a, b, StringComparison.Ordinal);
        }
        var kaputt = art switch
        {
            "pruefsumme" => Ersetze("\"nachricht\": \"Stadt 1", "\"nachricht\": \"Stadt 2"),
            "unbekannt" => Ersetze("\n \"pruefsumme\"", "\n \"extra\": 1,\n \"pruefsumme\""),
            "fehlt" => Ersetze(" \"beete\": [[6.92, 0.65]],\n", ""),
            "format" => Ersetze("\"format\": \"fistadt\"", "\"format\": \"stadt\""),
            "neuer" => Ersetze("\"formatversion\": 1", "\"formatversion\": 2"),
            "id" => Ersetze("\"id\": \"stadt1\"", "\"id\": \"Stadt 1\""),
            "laterne" => Ersetze("\"laternen\": [[8.0, 0.65, false]", "\"laternen\": [[8.0, 0.65]"),
            "zahl" => Ersetze("\"fassung\": 0", "\"fassung\": 0.0"),
            _ => throw new ArgumentException(art),
        };
        var fehler = Assert.Throws<StadtbauFehler>(() => StadtFassung.Lesen(System.Text.Encoding.UTF8.GetBytes(kaputt)));
        Assert.Contains(fehler.Funde, f => f.Contains(erwartet, StringComparison.Ordinal));
    }

    /// <summary>Gegenprobe der Inhaltspruefung: ein Ort ohne Zugang und eine Laterne neben der Karte sind Funde.</summary>
    [Fact]
    public void Gegenprobe_InhaltFindetFehler()
    {
        var f = Fassung0.Value;
        var stadtdaten = new PyObjekt();
        foreach (var (k, v) in f.Stadtdaten)
        {
            if (k == "zugaenge")
            {
                var z = new PyObjekt();
                foreach (var (o, w) in (PyObjekt)v!)
                {
                    if (o != "bank")
                    {
                        z.Add(o, w);
                    }
                }
                stadtdaten.Add(k, z);
            }
            else
            {
                stadtdaten.Add(k, v);
            }
        }
        var anders = new StadtFassung
        {
            StadtId = f.StadtId, Nummer = 1, Basis = 0, Werkzeug = f.Werkzeug, Erstellt = f.Erstellt, Nachricht = f.Nachricht,
            Karte = f.Karte, Stadtdaten = stadtdaten, Innenraeume = f.Innenraeume, Laternen = [.. f.Laternen, (40f, 1f, false)], Beete = f.Beete,
        };
        var funde = StadtbauPruefung.Inhalt(anders, SpielInhaltTests.Inhalt);
        Assert.True(funde.Any(x => x.Contains("„bank“", StringComparison.Ordinal)) || funde.Any(x => x.StartsWith("Die Stadt lässt sich nicht laden", StringComparison.Ordinal)), string.Join(" | ", funde));
        Assert.Contains(funde, x => x.Contains("Laterne Nr. 23", StringComparison.Ordinal) || x.StartsWith("Die Stadt lässt sich nicht laden", StringComparison.Ordinal));
    }

    /// <summary>Nur zum Schreiben der Fassung 0 (einmal in Welle 1, nie von Hand): LERNPLATTFORM_FASSUNG0_SCHREIBEN=1.</summary>
    [Fact]
    public void Fassung0_Schreiben()
    {
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_FASSUNG0_SCHREIBEN") != "1")
        {
            return;
        }
        Directory.CreateDirectory(Ordner);
        File.WriteAllBytes(Path.Combine(Ordner, StadtFassung.Dateiname(AktiveFassung.Stadt1, 0)), Auszug0().Schreiben());
    }
}

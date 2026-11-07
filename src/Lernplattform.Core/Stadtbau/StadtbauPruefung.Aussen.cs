using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitt 4): die Regeln fuer die Stadt aussen als gemeinsame Pruefung fuer
/// Werkzeug, CI und Einbau. Es sind die Regeln, die bisher nur die Tests des Spiels fuer Stadt 1 prueften, gerechnet auf
/// derselben 3D-Szene wie im Spiel (<see cref="StadtSzene.Bauen"/> mit einem Spielstand, in dem alle Orte zu sehen sind):
/// <list type="bullet">
/// <item>jeder Fussweg (Ort und Zierhaus) endet auf einer Strasse (<c>StadtTuerenTests</c>);</item>
/// <item>Gehwegmitte = Tuermitte (hoechstens <see cref="MittigGrenze"/>), der Gehweg beginnt nicht im Haus, die Tuer liegt
/// auf der Wand ihres Hauses (<c>StadtTuerenTests.GehwegMuendetMittig</c>, <c>JedesBetretbareHausHatTuer</c>);</item>
/// <item>jede Laterne steht nach <see cref="Nachtlicht.Regeln"/> am Strassenrand, hoechstens <see cref="LaterneVerschoben"/>
/// von ihrem Wunschplatz und nicht im Lichtraum eines Gleises (<c>StadtDarstellungTests.LaternenNachDenRegeln</c>);</item>
/// <item>der Lichtraum am Bahnuebergang bleibt frei (<see cref="Bahnuebergang.LichtraumHalb"/>), auch von Haeusern, Baeumen
/// und Beeten (<c>StadtDarstellungTests.BahnuebergangLichtraumFrei</c>);</item>
/// <item>Haeuser ueberschneiden sich nicht und stehen nicht auf einer Strasse samt Gehweg; Baeume, Beete und Laternen
/// stehen nicht in einem Haus;</item>
/// <item>alles liegt auf der Karte, jede Strasse hat mindestens zwei Punkte, Gehwege sind 0 bis <see cref="GehwegHoechstens"/>
/// breit, jedes Haus nutzt ein Modell, das es im Spiel gibt;</item>
/// <item>der Paketplatz vor der Wohnung liegt ausserhalb jedes Hauses, neben dem Fussweg und frei von Fest-Deko und Baeumen
/// (<c>SpielSpielweltTests.KartonstapelNebenDerTuer</c>).</item>
/// </list>
/// Fassung 0 ist mit allen Regeln gruen (Test im Werkzeug). Rot sperrt den Export.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>Gehwegmitte = Tuermitte (Weisung P8-30, wie <c>StadtTuerenTests.MittigGrenze</c>).</summary>
    public const float MittigGrenze = 0.02f;

    /// <summary>Weiteste Verschiebung einer Laterne laengs der Strasse (Regel "Laternen", Test 0,21 mit Rundung).</summary>
    public const float LaterneVerschoben = 0.21f;

    /// <summary>Breitester erlaubter Gehweg je Strassenseite.</summary>
    public const double GehwegHoechstens = 0.6;

    /// <summary>
    /// So weit darf ein Haus ueber den Rand von Strasse oder Gehweg ragen (Bordstein; das Stellwerk in Stadt 1 steht 0,06
    /// ueber dem Rand der Strasse).
    /// </summary>
    public const float StrassenSpielraum = 0.1f;

    /// <summary>Spielraum der Tuer zur Wand mit CC0-Modellen (wie der Test mit Grafik).</summary>
    public const float WandAbstandModell = 0.4f;

    private static string Zf(double wert) => wert.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>
    /// Spielstand fuer Pruefung, 3D-Ansicht und Probe: ein neuer Stand ohne Speicher, in dem alle Orte zu sehen sind
    /// (200 fertige Tage, eigene Firma auf der hoechsten Stufe, Zweigstelle), wie die Tests des Spiels.
    /// </summary>
    public static Spielablauf Probestand(PyObjekt inhalt)
    {
        var ablauf = new Spielablauf(inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        ablauf.Stand.TageFertig = 200;
        ablauf.Stand.Firma = new PyObjekt { { "stufe", 5L }, { "name", "Probe" }, { "tag", 1L } };
        ablauf.Stand.Zweigstelle = new PyObjekt { { "stufe", 1L }, { "tag", 1L } };
        return ablauf;
    }

    /// <summary>Alle Regeln aussen fuer eine Fassung (Spielinhalte wie bei <see cref="Inhalt"/>); leer = in Ordnung.</summary>
    public static List<StadtbauFund> Aussen(StadtFassung fassung, PyObjekt inhalt, GrafikBibliothek? grafik, PyObjekt? feste = null)
    {
        var funde = new List<StadtbauFund>();
        try
        {
            var welt = Objekt(fassung.Karte["welt"]);
            var (breite, hoehe) = (Py.F(welt["breite"]), Py.F(welt["hoehe"]));
            // Welle K1: Radien und Kurven auf den Daten (kaputtes Feld "radien" = Szene nicht bauen)
            var kurvenHeil = KurvenDaten(welt, funde);
            if (!DatenPruefen(fassung, welt, breite, hoehe, funde) || !kurvenHeil)
            {
                // ohne Fussweg baut das Spiel die Stadt nicht: erst diese Funde loesen
                return funde;
            }
        }
        catch (Exception fehler) when (fehler is InvalidDataException or KeyNotFoundException or InvalidCastException or FormatException or ArgumentException)
        {
            funde.Add(new StadtbauFund("Die Stadt lässt sich nicht laden (" + fehler.GetType().Name + ").", "Letzte Änderung rückgängig machen.", true));
            return funde;
        }
        Stadt stadt;
        Spielstand stand;
        try
        {
            stadt = fassung.StadtFuer(fassung.MitKarte(inhalt));
            stand = Probestand(stadt.Inhalt).Stand;
        }
        catch (Exception fehler) when (fehler is InvalidDataException or IOException or KeyNotFoundException or InvalidCastException or FormatException or OverflowException or SpielFehler or ArgumentException)
        {
            funde.Add(new StadtbauFund("Die Stadt lässt sich nicht laden (" + fehler.GetType().Name + ").", "Letzte Änderung rückgängig machen.", true));
            return funde;
        }
        StadtSzene szene;
        try
        {
            szene = StadtSzene.Bauen(stadt, stand, grafik, null, feste);
        }
        catch (Exception fehler) when (fehler is InvalidOperationException or ArgumentException or KeyNotFoundException or InvalidCastException or IndexOutOfRangeException)
        {
            funde.Add(new StadtbauFund("Die Stadt lässt sich nicht zeichnen (" + fehler.GetType().Name + ").", "Letzte Änderung rückgängig machen.", true));
            return funde;
        }
        Fusswege(stadt, szene, funde);
        Tueren(stadt, stand, szene, funde);
        // Laternen wie der Test des Spiels mit einem neuen Spielstand (LaternenJeJahresUndFestzeit)
        StadtSzene? neu = null;
        try
        {
            neu = fassung.Laternen.Count == 0 ? szene : StadtSzene.Bauen(stadt, null, grafik, null, feste);
        }
        catch (Exception fehler) when (fehler is InvalidOperationException or ArgumentException or KeyNotFoundException or InvalidCastException or IndexOutOfRangeException)
        {
            funde.Add(new StadtbauFund("Die Stadt lässt sich nicht zeichnen (" + fehler.GetType().Name + ").", "Letzte Änderung rückgängig machen.", true));
        }
        if (neu is not null)
        {
            Laternen(neu, funde);
        }
        // Phase 13 Welle 4a (Weisung P12-30, Sammelliste 220): auch im spaeten Spielstand (alle Orte zu sehen), sonst
        // rueckt eine Laterne erst spaeter vor die Tuer eines neuen Orts
        if (!ReferenceEquals(neu, szene))
        {
            Laternen(szene, funde, spaet: true);
        }
        Lichtraum(stadt, stand, szene, fassung, funde);
        Ueberschneidung(szene, fassung, funde);
        StrassenAufeinander(fassung, funde);
        KatalogDraussen(szene, fassung, funde);
        Paketplatz(stadt, szene, funde);
        WasserRegeln(stadt, stand, szene, fassung, funde);
        KurvenRegeln(szene, fassung, funde);
        return funde;
    }

    /// <summary>
    /// Strassen, Gehwege, Lage auf der Karte, bekannte Modelle und Fusswege mit mindestens zwei Punkten (ohne Szene).
    /// false = ein Fussweg fehlt oder es gibt Laternen ohne Strasse (dann laesst sich die Szene nicht bauen).
    /// </summary>
    private static bool DatenPruefen(StadtFassung fassung, PyObjekt welt, double breite, double hoehe, List<StadtbauFund> funde)
    {
        bool Drauf(double x, double y) => x >= 0 && y >= 0 && x <= breite && y <= hoehe;
        var modelle = new HashSet<string>(StringComparer.Ordinal);
        foreach (var quelle in new[] { Get(fassung.Karte, "modelle"), Get(fassung.Stadtdaten, "neue_modelle") })
        {
            if (quelle is PyObjekt m)
            {
                foreach (var (k, _) in m)
                {
                    modelle.Add(k);
                }
            }
        }
        var nr = 0;
        foreach (var l in ListeOderLeer(welt, "landschaft").OfType<PyObjekt>())
        {
            switch (l["typ"] as string)
            {
                case "strasse":
                    nr++;
                    var punkte = ListeOderLeer(l, "punkte").OfType<List<object?>>().ToList();
                    if (punkte.Count < 2)
                    {
                        funde.Add(new StadtbauFund($"Straße Nr. {nr} hat weniger als zwei Punkte.", "Straße löschen und neu setzen.", true));
                        break;
                    }
                    foreach (var p in punkte)
                    {
                        if (!Drauf(Py.F(p[0]), Py.F(p[1])))
                        {
                            funde.Add(new StadtbauFund($"Straße Nr. {nr} reicht über den Rand der Karte ({Zf(Py.F(p[0]))}; {Zf(Py.F(p[1]))}).",
                                "Straße zurück auf die Karte schieben.", true, Py.F(p[0]), Py.F(p[1])));
                            break;
                        }
                    }
                    if (Py.Hat(l, "gehweg") && (l["gehweg"] is not (long or double) || Py.F(l["gehweg"]) < 0 || Py.F(l["gehweg"]) > GehwegHoechstens))
                    {
                        funde.Add(new StadtbauFund($"Der Gehweg an Straße Nr. {nr} ist breiter als {Zf(GehwegHoechstens)} oder keine Zahl.",
                            "Eine Breite von 0 bis " + Zf(GehwegHoechstens) + " eintragen.", true, Py.F(punkte[0][0]), Py.F(punkte[0][1])));
                    }
                    break;
                case "gebaeude":
                    var gm = Py.Str(l["modell"]);
                    if (!modelle.Contains(gm))
                    {
                        funde.Add(new StadtbauFund($"Das Haus-Modell „{gm}“ gibt es im Spiel nicht.", "Haus löschen und aus dem Katalog neu setzen.", true, Py.F(l["x"]), Py.F(l["y"])));
                    }
                    if (!Drauf(Py.F(l["x"]), Py.F(l["y"])))
                    {
                        funde.Add(new StadtbauFund($"Ein Haus („{gm}“) liegt außerhalb der Karte.", "Haus zurück auf die Karte schieben.", true, Py.F(l["x"]), Py.F(l["y"])));
                    }
                    break;
                case "baum":
                    foreach (var p in ListeOderLeer(l, "punkte").OfType<List<object?>>())
                    {
                        if (!Drauf(Py.F(p[0]), Py.F(p[1])))
                        {
                            funde.Add(new StadtbauFund($"Ein Baum liegt außerhalb der Karte ({Zf(Py.F(p[0]))}; {Zf(Py.F(p[1]))}).", "Baum zurück auf die Karte schieben.", true, Py.F(p[0]), Py.F(p[1])));
                        }
                    }
                    break;
                case "park":
                    if (Py.F(l["w"]) <= 0 || Py.F(l["h"]) <= 0 || !Drauf(Py.F(l["x"]), Py.F(l["y"])) || !Drauf(Py.F(l["x"]) + Py.F(l["w"]), Py.F(l["y"]) + Py.F(l["h"])))
                    {
                        funde.Add(new StadtbauFund("Ein Park liegt nicht ganz auf der Karte.", "Park verschieben oder kleiner machen.", true, Py.F(l["x"]), Py.F(l["y"])));
                    }
                    break;
            }
        }
        foreach (var quelle in new[] { Get(fassung.Karte, "orte"), Get(fassung.Stadtdaten, "neue_orte") })
        {
            foreach (var o in quelle is List<object?> liste ? liste.OfType<PyObjekt>() : [])
            {
                var name = o["name"] as string ?? Py.Str(o["id"]);
                if (!modelle.Contains(Py.Str(o["modell"])))
                {
                    funde.Add(new StadtbauFund($"Das Modell von „{name}“ gibt es im Spiel nicht.", "Bitte CC Bescheid geben.", true, Py.F(o["x"]), Py.F(o["y"])));
                }
                if (!Drauf(Py.F(o["x"]), Py.F(o["y"])))
                {
                    funde.Add(new StadtbauFund($"„{name}“ liegt außerhalb der Karte.", "Haus zurück auf die Karte schieben.", true, Py.F(o["x"]), Py.F(o["y"])));
                }
            }
        }
        nr = 0;
        foreach (var (x, z, _) in fassung.Laternen)
        {
            nr++;
            if (!Drauf(x, z))
            {
                funde.Add(new StadtbauFund($"Laterne Nr. {nr} liegt außerhalb der Karte.", "Laterne zurück auf die Karte schieben.", true, x, z));
            }
        }
        nr = 0;
        foreach (var b in fassung.Beete)
        {
            nr++;
            if (!Drauf(b.X, b.Y))
            {
                funde.Add(new StadtbauFund($"Beet Nr. {nr} liegt außerhalb der Karte.", "Beet zurück auf die Karte schieben.", true, b.X, b.Y));
            }
        }
        var wege = true;
        var namen = new[] { Get(fassung.Karte, "orte"), Get(fassung.Stadtdaten, "neue_orte") }.SelectMany(q => q as List<object?> ?? []).OfType<PyObjekt>()
            .GroupBy(o => Py.Str(o["id"])).ToDictionary(g => g.Key, g => g.First()["name"] as string ?? g.Key, StringComparer.Ordinal);
        var darstellung = Get(fassung.Stadtdaten, "darstellung") as PyObjekt;
        // A 0.43.1 (Weisung P12-39): Schalter "kleingruen" nur true oder false (sonst bliebe unklar, was das Spiel zeichnet)
        if (darstellung is not null && Py.Hat(darstellung, "kleingruen") && darstellung["kleingruen"] is not bool)
        {
            funde.Add(new StadtbauFund("Der Schalter „kleingruen“ hat den Wert " + PythonJson.Text(darstellung["kleingruen"]) + " statt true oder false.",
                "In der Stadt-Datei „kleingruen“ auf true (mit Kleingrün) oder false (ganz frei) setzen.", true));
        }
        var zugaenge = new[] { "zugaenge", "zugaenge_neu" }.Select(k => Get(fassung.Stadtdaten, k) as PyObjekt ?? []).SelectMany(z => z)
            .Select(z => (Name: namen.GetValueOrDefault(z.Key, z.Key), Zugang: z.Value))
            .Concat((darstellung is null ? [] : ListeOderLeer(darstellung, "zierwege")).OfType<PyObjekt>().Select(z => (Name: z["name"] as string ?? Py.Str(z["modell"]), Zugang: (object?)z)));
        foreach (var (name, zugang) in zugaenge.Where(z => !z.Name.StartsWith('_')))
        {
            if (zugang is not PyObjekt z || Linie(Get(z, "weg")).Count < 2)
            {
                funde.Add(new StadtbauFund($"„{name}“ hat keinen Fußweg.", "Haus an eine Straße schieben.", true));
                wege = false;
            }
        }
        // Laternen brauchen eine Strasse (sonst baut das Spiel die Stadt nicht)
        if (fassung.Laternen.Count > 0 && !ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().Any(l => Py.Gleich(l["typ"], "strasse") && Linie(l["punkte"]).Count >= 2))
        {
            funde.Add(new StadtbauFund("Es gibt Laternen, aber keine Straße.", "Erst eine Straße bauen, dann Laternen an ihren Rand setzen.", true));
            wege = false;
        }
        return wege;
    }

    private static List<Vector2> Linie(object? liste) =>
        [.. (liste as List<object?> ?? []).OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])))];

    /// <summary>Jeder Fussweg (Orte und Zierhaeuser) endet auf einer Strasse.</summary>
    private static void Fusswege(Stadt stadt, StadtSzene szene, List<StadtbauFund> funde)
    {
        var strassen = szene.Strassen;
        void Pruefen(string name, object? zugang)
        {
            if (zugang is not PyObjekt z)
            {
                return;
            }
            var weg = Linie(z["weg"]);
            if (weg.Count == 0)
            {
                funde.Add(new StadtbauFund($"„{name}“ hat keinen Fußweg.", "Haus an eine Straße schieben.", true));
                return;
            }
            var ende = weg[^1];
            if (strassen.Count == 0 || strassen.Min(s => Kurve.Abstand(ende, s)) > 0.02f)
            {
                funde.Add(new StadtbauFund($"Der Fußweg von „{name}“ erreicht keine Straße.", "Haus näher an eine Straße schieben oder eine Straße bis vor die Tür bauen.", true, ende.X, ende.Y));
            }
        }
        foreach (var (id, zugang) in stadt.Zugaenge)
        {
            if (szene.Ort(id) is { } ort)
            {
                Pruefen(ort.Name, zugang);
            }
        }
        foreach (var z in StadtTueren.Zierwege(stadt))
        {
            Pruefen(z["name"] as string ?? Py.Str(z["modell"]), z);
        }
    }

    /// <summary>
    /// Gehwegmitte = Tuermitte, der Gehweg beginnt nicht im Haus, die Tuer sitzt auf einer Wand eines Blocks ihres Hauses
    /// (mit CC0-Modellen bis <see cref="WandAbstandModell"/> dahinter; Rechnung wie <c>StadtTuerenTests</c>).
    /// </summary>
    private static void Tueren(Stadt stadt, Spielstand stand, StadtSzene szene, List<StadtbauFund> funde)
    {
        var modelle = StadtModelle.Modelle(stadt.Inhalt, stadt.Daten);
        var kontext = StadtModelle.Kontext(stand, stadt.Inhalt);
        foreach (var tuer in szene.Tueren)
        {
            var name = szene.Ort(tuer.Ort)?.Name ?? tuer.Ort;
            var versatz = TuerVersatz(tuer);
            if (versatz > MittigGrenze)
            {
                funde.Add(new StadtbauFund($"Der Fußweg von „{name}“ trifft nicht die Mitte der Tür ({Zf(versatz)} daneben, erlaubt {Zf(MittigGrenze)}).",
                    "Haus oder Weg so verschieben, dass der Weg gerade auf die Tür zuläuft.", true, tuer.Mitte.X, tuer.Mitte.Y));
            }
            if (Vector2.Dot(tuer.WegAnfang - tuer.Mitte, tuer.Aussen) < -0.05f)
            {
                funde.Add(new StadtbauFund($"Der Fußweg von „{name}“ beginnt im Haus.", "Weg an die Hauswand legen.", true, tuer.Mitte.X, tuer.Mitte.Y));
            }
            if (stadt.Regeln.FirstOrDefault(r => Py.Gleich(r["id"], tuer.Ort)) is { } ort)
            {
                float ox = (float)Py.F(ort["x"]), oz = (float)Py.F(ort["y"]);
                var modell = Py.Str(ort["modell"]);
                var bloecke = StadtModelle.Teile(modelle, modell, kontext)
                    .Where(t => Py.Gleich(t.Teil["art"], "block") && StadtSzene.Darstellung(modell, t.Nr) is not null)
                    .Select(t => (X: ox + (float)Py.F(t.Teil["x"]) + StadtSzene.Darstellung(modell, t.Nr)!.Value, Z: oz + (float)Py.F(t.Teil["y"]), W: (float)Py.F(t.Teil["w"]), T: (float)Py.F(t.Teil["t"])))
                    .ToList();
                var grenze = szene.Grafik is null ? 0.02f : WandAbstandModell;
                if (bloecke.Count > 0 && bloecke.Min(b => AufWand(b.X, b.Z, b.W, b.T, tuer.Mitte)) > grenze)
                {
                    funde.Add(new StadtbauFund($"Die Tür von „{name}“ liegt nicht auf der Wand des Hauses.", "Weg und Tür an die Hauswand legen.", true, tuer.Mitte.X, tuer.Mitte.Y));
                }
            }
        }
    }

    /// <summary>Abstand quer zur Tuer zwischen Tuermitte und Anfang des Fusswegs (Gehwegmitte = Tuermitte).</summary>
    internal static float TuerVersatz(StadtTuer tuer) =>
        MathF.Abs(Vector2.Dot(tuer.WegAnfang - tuer.Mitte, new Vector2(-tuer.Aussen.Y, tuer.Aussen.X)));

    /// <summary>Abstand eines Punkts zur naechsten Wand eines Blocks (innerhalb ihrer Spanne, sonst gross).</summary>
    internal static float AufWand(float x, float z, float w, float t, Vector2 p)
    {
        var inX = p.X >= x - 0.02f && p.X <= x + w + 0.02f;
        var inZ = p.Y >= z - 0.02f && p.Y <= z + t + 0.02f;
        var d = float.MaxValue;
        if (inX)
        {
            d = MathF.Min(d, MathF.Min(MathF.Abs(p.Y - z), MathF.Abs(p.Y - (z + t))));
        }
        if (inZ)
        {
            d = MathF.Min(d, MathF.Min(MathF.Abs(p.X - x), MathF.Abs(p.X - (x + w))));
        }
        return d;
    }

    /// <summary>Laternen nach den Regeln des Spiels (Rand, laengs verschoben, nicht im Lichtraum).</summary>
    private static void Laternen(StadtSzene szene, List<StadtbauFund> funde, bool spaet = false)
    {
        if (szene.LaternenSkizze.Count == 0)
        {
            return;
        }
        IReadOnlyList<Nachtlicht.Laterne> laternen;
        try
        {
            laternen = szene.Laternen;
        }
        catch (InvalidOperationException fehler)
        {
            // "Laterne n: kein freier Platz an ihrer Strasse"
            var nr = fehler.Message.Split(' ', ':')[1];
            var (x, z, _) = int.TryParse(nr, out var n) && n >= 1 && n <= szene.LaternenSkizze.Count ? szene.LaternenSkizze[n - 1] : (0f, 0f, false);
            if (!funde.Any(f => f.Text.StartsWith($"Laterne Nr. {nr} ", StringComparison.Ordinal)))
            {
                funde.Add(new StadtbauFund($"Laterne Nr. {nr} findet an ihrer Straße keinen freien Platz{(spaet ? ", sobald alle Orte zu sehen sind" : "")}.",
                    "Laterne an eine freie Stelle der Straße schieben.", true, x, z));
            }
            return;
        }
        foreach (var l in laternen)
        {
            if (l.Verschoben > LaterneVerschoben && !funde.Any(f => f.Text.StartsWith($"Laterne Nr. {l.Nr} ", StringComparison.Ordinal)))
            {
                funde.Add(new StadtbauFund(spaet
                        ? $"Laterne Nr. {l.Nr} muss um {Zf(l.Verschoben)} verschoben werden, sobald alle Orte zu sehen sind (erlaubt {Zf(0.2)})."
                        : $"Laterne Nr. {l.Nr} muss um {Zf(l.Verschoben)} verschoben werden, damit sie frei steht (erlaubt {Zf(0.2)}).",
                    "Laterne an eine freie Stelle neben der Straße schieben.", true, l.Ziel.X, l.Ziel.Y));
            }
            if (szene.Gleise.Any(g => Kurve.Abstand(l.Ort, g) < Bahnuebergang.LichtraumHalb + Nachtlicht.AbstandModell))
            {
                funde.Add(new StadtbauFund($"Laterne Nr. {l.Nr} steht im Lichtraum der Bahn.", "Laterne weiter weg vom Gleis setzen.", true, l.Ort.X, l.Ort.Y));
            }
        }
    }

    /// <summary>
    /// Lichtraum der Gleise (<see cref="Bahnuebergang.LichtraumHalb"/> von der Gleismitte): die Ausstattung der
    /// Bahnuebergaenge (wie <c>StadtDarstellungTests</c>), Stamm eines Baums und Beete bleiben draussen; kein Haus steht auf
    /// dem Gleis (Bahnhoefe stehen dicht daneben, das ist erlaubt).
    /// </summary>
    private static void Lichtraum(Stadt stadt, Spielstand stand, StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        if (szene.Gleise.Count == 0)
        {
            return;
        }
        bool Drin(Vector2 p) => szene.Gleise.Any(g => Kurve.Abstand(p, g) < Bahnuebergang.LichtraumHalb);
        foreach (var k in szene.Andreaskreuze)
        {
            foreach (var p in Bahnuebergang.Grundpunkte(k).Where(Drin).Take(1))
            {
                funde.Add(new StadtbauFund("Ein Andreaskreuz am Bahnübergang steht im Lichtraum des Zugs.", "Straße am Bahnübergang verschieben.", true, p.X, p.Y));
            }
        }
        foreach (var (m, _) in szene.Baeume)
        {
            if (Drin(m))
            {
                funde.Add(new StadtbauFund("Ein Baum steht im Lichtraum der Bahn.", "Baum weiter weg vom Gleis setzen.", true, m.X, m.Y));
            }
        }
        foreach (var b in fassung.Beete)
        {
            if (Drin(b))
            {
                funde.Add(new StadtbauFund("Ein Beet liegt im Lichtraum der Bahn.", "Beet weiter weg vom Gleis setzen.", true, b.X, b.Y));
            }
        }
        // Bloecke (Gebaeude) der Haeuser, nicht der ganze Umriss: Bahnhoefe haben ihre Bahnsteige am Gleis
        var modelle = StadtModelle.Modelle(stadt.Inhalt, stadt.Daten);
        var kontext = StadtModelle.Kontext(stand, stadt.Inhalt);
        var gleis = szene.Gleise.SelectMany(Abgetastet).ToList();
        var haeuser = stadt.Regeln.Where(r => szene.Ort(Py.Str(r["id"])) is not null).Select(r => (Name: szene.Ort(Py.Str(r["id"]))!.Name, Modell: Py.Str(r["modell"]), X: Py.F(r["x"]), Y: Py.F(r["y"])))
            .Concat(ListeOderLeer(Objekt(Objekt(stadt.Inhalt["orte"])["welt"]), "landschaft").OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "gebaeude"))
                .Select(t => (Name: Py.Str(t["modell"]), Modell: Py.Str(t["modell"]), X: Py.F(t["x"]), Y: Py.F(t["y"]))));
        foreach (var (name, modell, ox, oz) in haeuser)
        {
            foreach (var t in StadtModelle.Teile(modelle, modell, kontext).Where(t => Py.Gleich(t.Teil["art"], "block") && StadtSzene.Darstellung(modell, t.Nr) is not null))
            {
                var x = (float)(ox + Py.F(t.Teil["x"]) + StadtSzene.Darstellung(modell, t.Nr)!.Value);
                var z = (float)(oz + Py.F(t.Teil["y"]));
                var (w, tief) = ((float)Py.F(t.Teil["w"]), (float)Py.F(t.Teil["t"]));
                if (gleis.FirstOrDefault(p => p.X > x + 0.05f && p.X < x + w - 0.05f && p.Y > z + 0.05f && p.Y < z + tief - 0.05f) is var treffer && treffer != default)
                {
                    funde.Add(new StadtbauFund($"„{name}“ steht auf dem Gleis.", "Haus vom Gleis weg schieben.", true, treffer.X, treffer.Y));
                    break;
                }
            }
        }
    }

    /// <summary>Punkte einer Linie alle 0,05.</summary>
    private static IEnumerable<Vector2> Abgetastet(IReadOnlyList<Vector2> linie)
    {
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var schritte = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(linie[i], linie[i + 1]) / 0.05f));
            for (var k = 0; k <= schritte; k++)
            {
                yield return Vector2.Lerp(linie[i], linie[i + 1], k / (float)schritte);
            }
        }
    }

    /// <summary>Haeuser ueberschneiden sich nicht, stehen nicht auf Strassen und Gehwegen; nichts steht in einem Haus.</summary>
    /// <summary>So nah darf ein Stueck aus dem Katalog dem Stamm eines Baums oder dem Mast einer Laterne kommen (Sammelliste 223).</summary>
    public const float KatalogStammAbstand = 0.15f;

    /// <summary>
    /// Phase 13 Welle 4a (Sammelliste 223, Fund Chat): ein Stueck aus dem Katalog draussen steht nicht in einem Haus (auch
    /// ein Effekt nicht, er waere nicht zu sehen), und ein Modell steht nicht auf dem Stamm eines Baums, am Mast einer
    /// Laterne oder auf der Fahrbahn (auf dem Gehweg darf es stehen). Texte mit der Kennung "cc0:..." (das Werkzeug setzt
    /// den Anzeigenamen ein).
    /// </summary>
    private static void KatalogDraussen(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        List<Katalogstueck.Draussen> stuecke;
        try
        {
            stuecke = Katalogstueck.AusStadtdaten(fassung.Stadtdaten);
        }
        catch (InvalidDataException fehler)
        {
            funde.Add(new StadtbauFund("Ein Stück aus dem Katalog ist falsch gespeichert (" + fehler.Message + ").", "Stück löschen und neu setzen.", true));
            return;
        }
        if (stuecke.Count == 0)
        {
            return;
        }
        static float Abstand((float X1, float Z1, float X2, float Z2) r, Vector2 p)
        {
            var dx = MathF.Max(MathF.Max(r.X1 - p.X, 0), p.X - r.X2);
            var dz = MathF.Max(MathF.Max(r.Z1 - p.Y, 0), p.Y - r.Z2);
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }
        static float AbstandLinie((float X1, float Z1, float X2, float Z2) r, IReadOnlyList<Vector2> linie)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                var (a, b) = (linie[i], linie[i + 1]);
                var schritte = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / 0.02f));
                for (var k = 0; k <= schritte; k++)
                {
                    best = MathF.Min(best, Abstand(r, Vector2.Lerp(a, b, (float)k / schritte)));
                }
            }
            return best;
        }
        string HausName(StadtHaus h) => szene.Ort(h.Id)?.Name ?? h.Id.Split('#')[0];
        IReadOnlyList<Nachtlicht.Laterne> laternen;
        try
        {
            laternen = szene.LaternenSkizze.Count == 0 ? [] : szene.Laternen;
        }
        catch (InvalidOperationException)
        {
            laternen = [];   // schon ein Fund der Laternen-Regel
        }
        foreach (var k in stuecke)
        {
            var r = k.Rechteck;
            if (szene.Haeuser.FirstOrDefault(h => MathF.Min(r.X2, h.Max.X) - MathF.Max(r.X1, h.Min.X) > Toleranz
                    && MathF.Min(r.Z2, h.Max.Z) - MathF.Max(r.Z1, h.Min.Z) > Toleranz) is { } haus)
            {
                funde.Add(new StadtbauFund($"„{k.Typ}“ steht im Haus „{HausName(haus)}“.", "Stück neben das Haus schieben.", true, k.X, k.Z));
                continue;
            }
            if (k.Effekt)
            {
                continue;
            }
            if (szene.Baeume.Any(b => Abstand(r, b.Mitte) < KatalogStammAbstand))
            {
                funde.Add(new StadtbauFund($"„{k.Typ}“ steht auf einem Baum.", "Stück neben den Baum schieben.", true, k.X, k.Z));
            }
            if (laternen.Any(l => Abstand(r, l.Ort) < KatalogStammAbstand))
            {
                funde.Add(new StadtbauFund($"„{k.Typ}“ steht an einer Laterne.", "Stück neben die Laterne schieben.", true, k.X, k.Z));
            }
            if (szene.Strassen.Any(l => AbstandLinie(r, l) < (StadtSzene.StrassenBreite / 2) - Toleranz))
            {
                funde.Add(new StadtbauFund($"„{k.Typ}“ steht auf der Fahrbahn.", "Stück auf den Gehweg oder neben die Straße schieben.", true, k.X, k.Z));
            }
            if (szene.Gleise.Any(l => AbstandLinie(r, l) < (StadtSzene.GleisBreite / 2) - Toleranz))
            {
                funde.Add(new StadtbauFund($"„{k.Typ}“ steht auf dem Gleis.", "Stück neben das Gleis schieben.", true, k.X, k.Z));
            }
        }
    }

    /// <summary>
    /// Patch S 0.5.1 Nachbesserung (Weisung P12-40 Punkt 3): laengste Strecke von <paramref name="a"/>, auf der sich die
    /// Fahrbahnen beider Strassen ueberdecken (Mitten naeher als eine Strassenbreite). Das Spiel laesst die Striche der
    /// Mittellinie weg, wo eine andere Strasse naeher als die halbe Breite (plus 0,05) liegt (sonst laegen auf einer
    /// Kreuzung zwei Striche als "+" uebereinander); liegen zwei Strassen aufeinander, fehlt sie deshalb auf der ganzen
    /// Strecke. Eine Kreuzung im rechten Winkel ergibt 1,44, eine Einmuendung ebenso, eine Verlaengerung 0,72; ab
    /// <see cref="StrassenAufeinanderAb"/> ist es ein roter Fund.
    /// </summary>
    public static (float Laenge, Vector2 Mitte) StrassenNaehe(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    {
        const float Schritt = 0.02f;
        var grenze = StadtSzene.StrassenBreite;
        var beste = (Laenge: 0f, Mitte: Vector2.Zero);
        var lauf = 0f;
        var start = Vector2.Zero;
        for (var i = 0; i + 1 < a.Count; i++)
        {
            var laenge = Vector2.Distance(a[i], a[i + 1]);
            var schritte = Math.Max(1, (int)MathF.Ceiling(laenge / Schritt));
            for (var k = i == 0 ? 0 : 1; k <= schritte; k++)
            {
                var q = Vector2.Lerp(a[i], a[i + 1], k / (float)schritte);
                if (Kurve.Abstand(q, b) <= grenze)
                {
                    if (lauf == 0)
                    {
                        start = q;
                    }
                    lauf += k == 0 && i == 0 ? 0 : laenge / schritte;
                    if (lauf > beste.Laenge)
                    {
                        beste = (lauf, (start + q) / 2);
                    }
                }
                else
                {
                    lauf = 0;
                }
            }
        }
        return beste;
    }

    /// <summary>Ab dieser Strecke liegen zwei Strassen aufeinander (dreifache Breite; eine Kreuzung bis 45 Grad bleibt erlaubt).</summary>
    public const float StrassenAufeinanderAb = 3 * StadtSzene.StrassenBreite;

    /// <summary>Zwei Strassen duerfen sich nur kreuzen oder ineinander muenden, nie aufeinander liegen.</summary>
    private static void StrassenAufeinander(StadtFassung fassung, List<StadtbauFund> funde)
    {
        var strassen = ListeOderLeer(Objekt(fassung.Karte["welt"]), "landschaft").OfType<PyObjekt>()
            .Where(l => Py.Gleich(l["typ"], "strasse"))
            .Select(l => ListeOderLeer(l, "punkte").OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]))).ToList())
            .ToList();
        for (var i = 0; i < strassen.Count; i++)
        {
            for (var j = i + 1; j < strassen.Count; j++)
            {
                if (strassen[i].Count < 2 || strassen[j].Count < 2)
                {
                    continue;
                }
                var ab = StrassenNaehe(strassen[i], strassen[j]);
                var ba = StrassenNaehe(strassen[j], strassen[i]);
                var n = ab.Laenge >= ba.Laenge ? ab : ba;
                if (n.Laenge >= StrassenAufeinanderAb)
                {
                    funde.Add(new StadtbauFund($"Straße Nr. {i + 1} und Straße Nr. {j + 1} liegen auf {Zf(n.Laenge)} Länge übereinander oder dicht nebeneinander, die Fahrbahnen überdecken sich. Dort fehlt im Spiel die Mittellinie.",
                        "Erlaubt sind nur Kreuzungen und Einmündungen: eine der beiden Straßen verschieben, drehen oder kürzen.", true, n.Mitte.X, n.Mitte.Y));
                }
            }
        }
    }

    private static void Ueberschneidung(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        string Name(StadtHaus h) => szene.Ort(h.Id)?.Name ?? h.Id.Split('#')[0];
        static bool Drin(Vector2 p, StadtHaus h, float rand = 0f) =>
            p.X > h.Min.X - rand && p.X < h.Max.X + rand && p.Y > h.Min.Z - rand && p.Y < h.Max.Z + rand;
        var haeuser = szene.Haeuser;
        for (var i = 0; i < haeuser.Count; i++)
        {
            for (var j = i + 1; j < haeuser.Count; j++)
            {
                var (a, b) = (haeuser[i], haeuser[j]);
                var bx = MathF.Min(a.Max.X, b.Max.X) - MathF.Max(a.Min.X, b.Min.X);
                var bz = MathF.Min(a.Max.Z, b.Max.Z) - MathF.Max(a.Min.Z, b.Min.Z);
                if (bx > 0.02f && bz > 0.02f)
                {
                    funde.Add(new StadtbauFund($"„{Name(a)}“ und „{Name(b)}“ stehen ineinander.", "Eines der Häuser verschieben.", true,
                        (MathF.Max(a.Min.X, b.Min.X) + MathF.Min(a.Max.X, b.Max.X)) / 2, (MathF.Max(a.Min.Z, b.Min.Z) + MathF.Min(a.Max.Z, b.Max.Z)) / 2));
                }
            }
        }
        var welt = Objekt(fassung.Karte["welt"]);
        foreach (var l in ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse")))
        {
            var linie = Linie(l["punkte"]);
            if (linie.Count < 2)
            {
                continue;
            }
            var halb = (StadtSzene.StrassenBreite / 2) + (float)(Py.Hat(l, "gehweg") ? Py.F(l["gehweg"]) : 0) - StrassenSpielraum;
            foreach (var h in haeuser)
            {
                if (StreckeImRechteck(linie, h, halb) is { } p)
                {
                    funde.Add(new StadtbauFund($"„{Name(h)}“ steht auf einer Straße oder ihrem Gehweg.", "Haus von der Straße weg schieben oder den Gehweg schmaler machen.", true, p.X, p.Y));
                }
            }
        }
        foreach (var (m, _) in szene.Baeume)
        {
            if (haeuser.FirstOrDefault(h => Drin(m, h)) is { } h)
            {
                funde.Add(new StadtbauFund($"Ein Baum steht im Haus „{Name(h)}“.", "Baum neben das Haus setzen.", true, m.X, m.Y));
            }
        }
        foreach (var b in fassung.Beete)
        {
            if (haeuser.FirstOrDefault(h => Drin(b, h)) is { } h)
            {
                funde.Add(new StadtbauFund($"Ein Beet liegt im Haus „{Name(h)}“.", "Beet neben das Haus setzen.", true, b.X, b.Y));
            }
        }
        var nr = 0;
        foreach (var (x, z, _) in fassung.Laternen)
        {
            nr++;
            if (haeuser.FirstOrDefault(h => Drin(new Vector2(x, z), h)) is { } h)
            {
                funde.Add(new StadtbauFund($"Laterne Nr. {nr} steht im Haus „{Name(h)}“.", "Laterne an die Straße setzen.", true, x, z));
            }
        }
    }

    /// <summary>Erster Punkt einer Strasse (mit halber Breite) im Grundriss eines Hauses, sonst null.</summary>
    private static Vector2? StreckeImRechteck(IReadOnlyList<Vector2> linie, StadtHaus h, float halb)
    {
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (a, b) = (linie[i], linie[i + 1]);
            var laenge = Vector2.Distance(a, b);
            var schritte = Math.Max(1, (int)MathF.Ceiling(laenge / 0.05f));
            for (var k = 0; k <= schritte; k++)
            {
                var p = Vector2.Lerp(a, b, k / (float)schritte);
                // naechster Punkt des Hauses zur Mittellinie
                var nx = Math.Clamp(p.X, h.Min.X, h.Max.X);
                var nz = Math.Clamp(p.Y, h.Min.Z, h.Max.Z);
                if (Vector2.Distance(p, new Vector2(nx, nz)) < halb)
                {
                    return p;
                }
            }
        }
        return null;
    }

    /// <summary>Paketplatz vor der Wohnung: ausserhalb der Haeuser, neben dem Fussweg, frei von Fest-Deko und Baeumen.</summary>
    private static void Paketplatz(Stadt stadt, StadtSzene szene, List<StadtbauFund> funde)
    {
        if (Lieferpaket.WohnOrt(szene) is not { } wohnort)
        {
            return;
        }
        if (Lieferpaket.Lage(szene, stadt) is not { } lage)
        {
            funde.Add(new StadtbauFund("Die Wohnung hat keine Tür, vor der die Pakete stehen können.", "Haus der Wohnung an eine Straße schieben.", true));
            return;
        }
        var (mitte, aussen) = lage;
        var halb = Lieferpaket.Breite * 1.6f;
        foreach (var haus in szene.Haeuser)
        {
            if (mitte.X + halb > haus.Min.X && mitte.X - halb < haus.Max.X && mitte.Y + halb > haus.Min.Z && mitte.Y - halb < haus.Max.Z)
            {
                funde.Add(new StadtbauFund($"Der Paketplatz liegt im Haus „{szene.Ort(haus.Id)?.Name ?? haus.Id}“.", "Paketplatz weiter vor die Tür schieben.", true, mitte.X, mitte.Y));
            }
        }
        if (stadt.Zugaenge.FirstOrDefault(z => z.Key == wohnort).Value is PyObjekt zugang && Linie(zugang["weg"]) is { Count: > 0 } weg
            && Kurve.Abstand(mitte, weg) < (StadtSzene.WegBreite / 2) + halb - 0.05f)
        {
            funde.Add(new StadtbauFund("Der Paketplatz liegt auf dem Fußweg der Wohnung.", "Paketplatz zur Seite schieben.", true, mitte.X, mitte.Y));
        }
        if (szene.FestPlaetzeAlle.Any(f => Vector2.Distance(f.P, mitte) < f.R + halb) || szene.Baeume.Any(b => Vector2.Distance(b.Mitte, mitte) < b.R + halb))
        {
            funde.Add(new StadtbauFund("Der Paketplatz liegt in einem Baum oder in der Fest-Deko.", "Paketplatz an eine freie Stelle schieben.", true, mitte.X, mitte.Y));
        }
        if (szene.Tueren.FirstOrDefault(t => t.Ort == wohnort) is { } tuer && Vector2.Dot(aussen, mitte - tuer.Mitte) <= 0)
        {
            funde.Add(new StadtbauFund("Der Paketplatz liegt hinter der Haustür.", "Paketplatz vor die Tür schieben.", true, mitte.X, mitte.Y));
        }
    }
}

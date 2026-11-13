using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>Was eine Strassenzug-Vorlage zur Strasse dazusetzt.</summary>
public enum ZugTeil
{
    Baum,
    Zebrastreifen,
    Haltestelle,
}

/// <summary>Ein Stueck der Ausstattung: Art, Stelle (Baum: Stamm, Zebrastreifen: Mitte, Haltestelle: Punkt neben der Strasse), Grund (null = wird gesetzt).</summary>
public sealed record ZugStueck(ZugTeil Teil, Vector2 Lage, string? Grund)
{
    public bool Frei => Grund is null;
}

/// <summary>Plan eines Strassenzugs: die Strasse (wie beim Ziehen) und alle Stuecke der Ausstattung, auch die ausgelassenen.</summary>
public sealed record StrassenzugPlan(Strassenzug Vorlage, Verbindung.Plan Plan, List<ZugStueck> Stuecke, bool Nachgeprueft)
{
    public int Frei => Stuecke.Count(s => s.Frei);

    public int Ausgelassen => Stuecke.Count - Frei;

    public bool Gefunden => Plan.Gefunden;
}

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241, Kurzplan P12-131 Punkt 3, Nico P12-231 Antwort 5c): „Straße ziehen“ mit einer
/// Ausstattung (<see cref="Strassenzug"/>). Die Ausstattung kommt mit der Strasse in EINEM Schritt dazu
/// (<see cref="StrassenzugZiehen"/>, einmal rueckgaengig). Rote Stellen werden ausgelassen und gezaehlt (wie bei der Reihe);
/// die Vorschau ist der Plan, den der Schritt setzt (<see cref="StrassenzugPlanen"/> mit derselben Rechnung).
/// <list type="bullet">
/// <item>Gehweg: die neuen Stuecke bekommen den Gehweg der Vorlage (<see cref="NeuerGehweg"/> oder keinen); eine verlaengerte
/// Strasse behaelt ihren.</item>
/// <item>Baeume: beidseitig entlang der gezeichneten Mittellinie, <see cref="ZugBaumRand"/> neben dem Gehweg bzw. der Fahrbahn,
/// erster Baum einen halben Abstand nach dem Anfang. Ausgelassen (je Baum gepruefert wie die Reihe, <see cref="ReiheGrund"/>):
/// ausserhalb der Karte, im Haus, auf Fahrbahn, Gehweg, Gleis, Wasser, Fussweg, naeher als <see cref="ZugKreuzungFrei"/> an einer
/// Kreuzung, an einem anderen Baum, an einer selbst gesetzten Laterne, am Haeuschen oder Schild einer Haltestelle.</item>
/// <item>Zebrastreifen: an jeder Kreuzung oder Einmuendung, die auf der neuen Strasse liegt, quer auf das gerade Stueck,
/// <see cref="ZugZebraAbstand"/> von ihr entfernt (Abstaende der Pruefung: Kreuzung, andere Zebrastreifen, Ampel, Gleis).</item>
/// <item>Haltestelle: in der Mitte des laengsten geraden Stuecks, wenn es mindestens <see cref="ZugHaltMindestens"/> lang ist,
/// zuerst rechts in Zugrichtung (Abstaende der Pruefung: Kreuzung, Zebrastreifen, Ampel, Gleis, andere Haltestellen).</item>
/// <item>Nachpruefung (mit <c>pruefen</c>): die ganze Pruefung draussen mit und ohne Ausstattung; jedes Stueck, das einen neuen
/// roten Fund bringt, wird ausgelassen (Grund = der Fund), bis kein neuer roter Fund mehr kommt. So bringt die Ausstattung nie
/// ein neues Rot.</item>
/// </list>
/// Laternen stellt das Werkzeug ohnehin an jede Strasse (<see cref="LaternenAutomatik"/>, im selben Schritt).
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>So weit stehen Baeume neben dem Gehweg (ohne Gehweg: neben der Fahrbahn).</summary>
    public const float ZugBaumRand = 0.4f;

    /// <summary>Ein Baum steht nicht naeher als so an einer Kreuzung oder Einmuendung.</summary>
    public const float ZugKreuzungFrei = 1.5f;

    /// <summary>Zebrastreifen: so weit von der Kreuzung bzw. Einmuendung (Regel 1,0 plus halbe Breite plus Rand).</summary>
    public const float ZugZebraAbstand = 1.5f;

    /// <summary>Haltestelle nur auf einem geraden Stueck mindestens so lang.</summary>
    public const float ZugHaltMindestens = 6f;

    /// <summary>Zugabe zu den Abstaenden der Pruefung (damit gerundete Lagen sicher frei sind).</summary>
    private const float ZugZugabe = 0.05f;

    /// <summary>So oft wird hoechstens nachgeprueft (dann bleibt die Ausstattung ganz weg).</summary>
    public const int ZugNachpruefenHoechstens = 12;

    /// <summary>Gegenprobe: ohne Nachpruefung (nur die schnellen Regeln je Stueck, nur Tests).</summary>
    public static bool ZugOhneNachpruefungFuerTests { get => ZugOhneNach.Value; set => ZugOhneNach.Value = value; }

    private static readonly AsyncLocal<bool> ZugOhneNach = new();

    /// <summary>
    /// Plan des Strassenzugs von (ax, ay) nach (bx, by) mit der Vorlage. <paramref name="pruefen"/> (Pruefung draussen auf einem
    /// Stand, mit Laternen-Automatik, wie <c>Bauen3D.Pruefen</c>) macht die Nachpruefung; null = nur die schnellen Regeln (Vorschau
    /// waehrend der Zeiger sich bewegt).
    /// </summary>
    public static StrassenzugPlan StrassenzugPlanen(PyObjekt aussen, double ax, double ay, double bx, double by, bool raster, Strassenzug vorlage,
        Func<PyObjekt, List<StadtbauFund>>? pruefen = null, CancellationToken abbruch = default)
    {
        var (_, _, plan) = ZiehPlan(aussen, ZiehArt.Strasse, ax, ay, bx, by, raster);
        if (!plan.Gefunden)
        {
            return new StrassenzugPlan(vorlage, plan, [], false);
        }
        var basis = (PyObjekt)Py.Kopie(aussen)!;
        NurStrasse(basis, ax, ay, bx, by, raster, vorlage);
        var stuecke = new List<ZugStueck>();
        var neueGerade = Verbindung.Stuecke(plan).Where(s => s.Grad == 0 && s.Punkte.Count == 2).Select(s => (A: s.Punkte[0], B: s.Punkte[1])).ToList();
        var linie = ZiehLinie(ZiehArt.Strasse, plan);
        var strassen = Aussen.Landschaft(basis).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse")).ToList();
        var linien = strassen.Select(t => (IReadOnlyList<Vector2>)Linie(t["punkte"])).ToList();
        var kreuzungen = Verkehr.KreuzungenAus(linien);
        var bahnen = Aussen.Landschaft(basis).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "bahn")).Select(t => Linie(t["punkte"])).ToList();
        var ampeln = AlleAmpeln(basis).Select(a => new Vector2((float)a.X, (float)a.Y)).ToList();
        var zebras = AlleZebras(basis).Select(z => new Vector2((float)z.X, (float)z.Y)).ToList();
        var ringe = strassen.Where(Kreisverkehr.IstRing).Select(t => Linie(t["punkte"])).ToList();

        // --- Zebrastreifen an jeder Kreuzung oder Einmuendung der neuen Strasse ---
        if (vorlage.Zebrastreifen)
        {
            foreach (var k in kreuzungen.Where(k => Kurve.Abstand(k, linie) < 0.05f))
            {
                ZugStueck? bester = null;
                foreach (var (a, b) in neueGerade.Where(g => Kurve.Abstand(k, [g.A, g.B]) < 0.05f))
                {
                    var laenge = Vector2.Distance(a, b);
                    var richtung = (b - a) / laenge;
                    // vom Kreuzungspunkt in das Stueck hinein (liegt er mitten darauf: beide Richtungen)
                    var t = Vector2.Dot(k - a, richtung);
                    foreach (var r in t < 0.05f ? [1f] : t > laenge - 0.05f ? [-1f] : (float[])[1f, -1f])
                    {
                        var ziel = t + (r * ZugZebraAbstand);
                        if (ziel < 0.3f || ziel > laenge - 0.3f)
                        {
                            bester ??= new ZugStueck(ZugTeil.Zebrastreifen, a + (richtung * Math.Clamp(ziel, 0, laenge)), Texte.ZugZuKurz);
                            continue;
                        }
                        var p = a + (richtung * ziel);
                        var grund = ZebraGrund(basis, vorlage, p, kreuzungen, zebras, ampeln, bahnen, ringe);
                        if (grund is null)
                        {
                            bester = new ZugStueck(ZugTeil.Zebrastreifen, p, null);
                            zebras.Add(p);
                            break;
                        }
                        bester = bester is { Frei: false } || bester is null ? new ZugStueck(ZugTeil.Zebrastreifen, p, grund) : bester;
                    }
                    if (bester is { Frei: true })
                    {
                        break;
                    }
                }
                if (bester is not null)
                {
                    stuecke.Add(bester);
                }
            }
        }

        // --- Haltestelle in der Mitte des laengsten geraden Stuecks ---
        if (vorlage.Haltestelle && neueGerade.Count > 0)
        {
            var (a, b) = neueGerade.OrderByDescending(g => Vector2.Distance(g.A, g.B)).First();
            var laenge = Vector2.Distance(a, b);
            var richtung = (b - a) / laenge;
            var mitte = (a + b) / 2;
            var abstand = (StadtSzene.StrassenBreite / 2) + (vorlage.Gehweg ? (float)NeuerGehweg : 0) + 0.3f;
            ZugStueck? halt = null;
            foreach (var seite in (float[])[1f, -1f])
            {
                var p = mitte + (Bahnuebergang.Rechts(richtung) * (abstand * seite));
                var grund = laenge < ZugHaltMindestens ? Texte.ZugHaltZuKurz : HaltGrund(basis, vorlage, p, a, b, kreuzungen, zebras, ampeln, bahnen, ringe);
                if (grund is null)
                {
                    halt = new ZugStueck(ZugTeil.Haltestelle, p, null);
                    break;
                }
                halt ??= new ZugStueck(ZugTeil.Haltestelle, p, grund);
            }
            stuecke.Add(halt!);
        }

        // --- Baeume beidseitig ---
        if (vorlage.Baeume && linie.Count >= 2)
        {
            var haltFrei = stuecke.Where(s => s is { Teil: ZugTeil.Haltestelle, Frei: true }).Select(s => HaltestelleVorschau(basis, s.Lage.X, s.Lage.Y)).OfType<Haltestelle>().ToList();
            haltFrei.AddRange(AlleHalte(basis).Where(h => h.Stelle.Art == AussenArt.Haltestelle).Select(h => HaltestelleVon(basis, h.Stelle)).OfType<Haltestelle>());
            var rand = (StadtSzene.StrassenBreite / 2) + (vorlage.Gehweg ? (float)NeuerGehweg : 0) + ZugBaumRand;
            var gesetzt = new List<Vector2>();
            var abstand = (float)Strassenzug.AbstandBegrenzt(vorlage.BaumAbstand);
            foreach (var seite in (float[])[1f, -1f])
            {
                var neben = Kurve.Versetzt(linie, rand * seite);
                foreach (var q in Punkte(neben, abstand))
                {
                    var p = new Vector2((float)Bearbeitung.AufRaster(q.X, Bearbeitung.RasterFein), (float)Bearbeitung.AufRaster(q.Y, Bearbeitung.RasterFein));
                    var grund = BaumGrund(basis, p, kreuzungen, strassen, gesetzt, haltFrei);
                    if (grund is null)
                    {
                        gesetzt.Add(p);
                    }
                    stuecke.Add(new ZugStueck(ZugTeil.Baum, p, grund));
                }
            }
        }

        if (pruefen is not null && !ZugOhneNach.Value && stuecke.Any(s => s.Frei))
        {
            stuecke = Nachpruefen(basis, stuecke, pruefen, abbruch);
            return new StrassenzugPlan(vorlage, plan, stuecke, true);
        }
        return new StrassenzugPlan(vorlage, plan, stuecke, false);
    }

    /// <summary>Punkte entlang eines Linienzugs: der erste einen halben Abstand nach dem Anfang, dann je <paramref name="abstand"/>.</summary>
    private static IEnumerable<Vector2> Punkte(IReadOnlyList<Vector2> linie, float abstand)
    {
        var gesamt = 0f;
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            gesamt += Vector2.Distance(linie[i], linie[i + 1]);
        }
        for (var s = abstand / 2; s <= gesamt - 0.3f; s += abstand)
        {
            var rest = s;
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                var d = Vector2.Distance(linie[i], linie[i + 1]);
                if (rest <= d || i + 2 == linie.Count)
                {
                    yield return Vector2.Lerp(linie[i], linie[i + 1], d < 1e-6f ? 0 : Math.Clamp(rest / d, 0, 1));
                    break;
                }
                rest -= d;
            }
        }
    }

    /// <summary>Nur die Strasse ziehen und ihre neuen Stuecke mit dem Gehweg der Vorlage versehen (Rueckgabe wie <see cref="Ziehen"/>).</summary>
    private static AussenStelle? NurStrasse(PyObjekt aussen, double ax, double ay, double bx, double by, bool raster, Strassenzug vorlage)
    {
        var land = Aussen.Landschaft(aussen);
        var vorher = land.ToList();
        var s = Ziehen(aussen, ZiehArt.Strasse, ax, ay, bx, by, raster, out _);
        foreach (var l in land.OfType<PyObjekt>().Where(l => !vorher.Any(v => ReferenceEquals(v, l)) && Py.Gleich(l["typ"], "strasse")))
        {
            if (vorlage.Gehweg)
            {
                Py.Setzen(l, "gehweg", Bearbeitung.Zahl(NeuerGehweg));
            }
            else
            {
                var rest = l.Where(p => p.Key != "gehweg").ToList();
                l.Clear();
                l.AddRange(rest);
            }
        }
        return s;
    }

    private static string? ZebraGrund(PyObjekt basis, Strassenzug vorlage, Vector2 p, List<Vector2> kreuzungen, List<Vector2> zebras, List<Vector2> ampeln,
        List<List<Vector2>> bahnen, List<List<Vector2>> ringe)
    {
        if (!vorlage.Gehweg)
        {
            return Texte.ZugOhneGehwegKeinZebra;
        }
        if (Zebrastreifen.Legen(0, p, ZebraStrassen(basis), 0.05f) is null)
        {
            return Texte.ZugZebraNichtGerade;
        }
        if (kreuzungen.Any(k => Vector2.Distance(k, p) < StadtbauPruefung.ZebraKreuzungAbstand + (Zebrastreifen.Breite / 2) + ZugZugabe))
        {
            return Texte.ZugZuNahKreuzung;
        }
        if (zebras.Any(z => Vector2.Distance(z, p) < StadtbauPruefung.ZebraZwischen + ZugZugabe))
        {
            return Texte.ZugZuNahZebra;
        }
        if (ampeln.Any(a => Vector2.Distance(a, p) < StadtbauPruefung.AmpelZebraAbstand + ZugZugabe))
        {
            return Texte.ZugZuNahAmpel;
        }
        if (bahnen.Any(b => b.Count >= 2 && Kurve.Abstand(p, b) < StadtbauPruefung.ZebraBahnAbstand + ZugZugabe))
        {
            return Texte.ZugZuNahGleis;
        }
        if (ringe.Any(r => r.Count >= 2 && Kurve.Abstand(p, r) < (StadtSzene.StrassenBreite / 2) + StadtbauPruefung.ZebraRingAbstand + ZugZugabe))
        {
            return Texte.ZugZuNahKreisverkehr;
        }
        return null;
    }

    private static string? HaltGrund(PyObjekt basis, Strassenzug vorlage, Vector2 p, Vector2 a, Vector2 b, List<Vector2> kreuzungen, List<Vector2> zebras,
        List<Vector2> ampeln, List<List<Vector2>> bahnen, List<List<Vector2>> ringe)
    {
        if (!vorlage.Gehweg)
        {
            return Texte.ZugOhneGehwegKeineHalt;
        }
        if (HaltListe(basis, AussenArt.Haltestelle).Count >= Haltestellen.Hoechstens)
        {
            return Texte.ZugHaltGenug;
        }
        if (HaltestelleVorschau(basis, p.X, p.Y) is not { } h || Kurve.Abstand(h.Mitte, [a, b]) > 0.05f)
        {
            return Texte.ZugHaltNichtGerade;
        }
        IReadOnlyList<Vector2> bereich = [h.Mitte + (h.Fahrt * (Haltestellen.SchildVor - Haltestellen.ZickzackLang)), h.Mitte + (h.Fahrt * Haltestellen.SchildVor)];
        if (kreuzungen.Any(k => Kurve.Abstand(k, bereich) < StadtbauPruefung.HaltKreuzungAbstand + ZugZugabe)
            || ringe.Any(r => r.Count >= 2 && (Kurve.Abstand(bereich[0], r) < StadtbauPruefung.HaltKreuzungAbstand + ZugZugabe || Kurve.Abstand(bereich[1], r) < StadtbauPruefung.HaltKreuzungAbstand + ZugZugabe)))
        {
            return Texte.ZugZuNahKreuzung;
        }
        if (zebras.Any(z => Kurve.Abstand(z, bereich) < StadtbauPruefung.HaltZebraAbstand + ZugZugabe) || ampeln.Any(am => Kurve.Abstand(am, bereich) < StadtbauPruefung.HaltZebraAbstand + ZugZugabe))
        {
            return Texte.ZugZuNahZebra;
        }
        if (bahnen.Any(bahn => bahn.Count >= 2 && (Kurve.Abstand(bereich[0], bahn) < StadtbauPruefung.HaltZebraAbstand + ZugZugabe || Kurve.Abstand(bereich[1], bahn) < StadtbauPruefung.HaltZebraAbstand + ZugZugabe)))
        {
            return Texte.ZugZuNahGleis;
        }
        if (AlleHalte(basis).Any(x => HaltMitte(basis, x.Stelle) is { } m && Vector2.Distance(m, h.Mitte) < StadtbauPruefung.HaltGleicheSeite + ZugZugabe))
        {
            return Texte.ZugZuNahHalt;
        }
        return null;
    }

    private static string? BaumGrund(PyObjekt basis, Vector2 p, List<Vector2> kreuzungen, List<PyObjekt> strassen, List<Vector2> gesetzt, List<Haltestelle> halte)
    {
        var grund = ReiheGrund(basis, AussenArt.Baum, null, new ReihenStueck(p.X, p.Y, 0, 2 * ReiheBaumRadius, 2 * ReiheBaumRadius, null));
        if (grund is not null)
        {
            return grund;
        }
        foreach (var l in strassen)
        {
            var linie = Linie(l["punkte"]);
            if (linie.Count >= 2 && Kurve.Abstand(p, linie) < (StadtSzene.StrassenBreite / 2) + StadtSzene.GehwegBreite(l) + (float)ReiheBaumRadius)
            {
                return Texte.ZugAufGehweg;
            }
        }
        if (kreuzungen.Any(k => Vector2.Distance(k, p) < ZugKreuzungFrei))
        {
            return Texte.ZugZuNahKreuzung;
        }
        if (gesetzt.Any(g => Vector2.Distance(g, p) < 0.5f) || Alle(basis).Any(a => a.Stelle.Art == AussenArt.Baum && Vector2.Distance(new Vector2((float)a.X, (float)a.Y), p) < 0.5f))
        {
            return Texte.ReiheAufBaum;
        }
        var laternen = Aussen.LaternenWerte(basis);
        for (var i = 0; i < laternen.Count; i++)
        {
            if (!LaternenAutomatik.IstAutomatisch(basis, i) && Vector2.Distance(new Vector2(laternen[i].X, laternen[i].Z), p) < 0.5f)
            {
                return Texte.ReiheAnLaterne;
            }
        }
        if (halte.Any(h => Vector2.Distance(h.Haeuschen, p) < 0.7f || Vector2.Distance(h.Schild, p) < 0.5f))
        {
            return Texte.ZugAnHaltestelle;
        }
        return null;
    }

    /// <summary>Die freien Stuecke auf <paramref name="aussen"/> setzen (nach der Strasse). Rueckgabe: neue Stellen.</summary>
    private static List<AussenStelle> ZugStueckeSetzen(PyObjekt aussen, IEnumerable<ZugStueck> stuecke)
    {
        var neu = new List<AussenStelle>();
        foreach (var s in stuecke.Where(s => s.Frei))
        {
            var stelle = s.Teil switch
            {
                ZugTeil.Baum => SetzenUndDrehen(aussen, AussenArt.Baum, s.Lage.X, s.Lage.Y, false, null, 0),
                ZugTeil.Zebrastreifen => ZebraSetzen(aussen, s.Lage.X, s.Lage.Y),
                _ => HaltSetzen(aussen, AussenArt.Haltestelle, s.Lage.X, s.Lage.Y),
            };
            if (stelle is not null)
            {
                neu.Add(stelle);
            }
        }
        return neu;
    }

    /// <summary>
    /// Nachpruefung: die ganze Pruefung draussen auf der Strasse allein und mit der Ausstattung. Bringt die Ausstattung einen
    /// neuen roten Fund, faellt das Stueck weg, das am naechsten an seiner Stelle liegt (ohne Stelle: Haltestelle, dann
    /// Zebrastreifen, dann Baeume), mit dem Fund als Grund; dann wird neu geprueft.
    /// </summary>
    private static List<ZugStueck> Nachpruefen(PyObjekt basis, List<ZugStueck> stuecke, Func<PyObjekt, List<StadtbauFund>> pruefen, CancellationToken abbruch)
    {
        var vorher = pruefen((PyObjekt)Py.Kopie(basis)!);
        for (var runde = 0; runde < ZugNachpruefenHoechstens; runde++)
        {
            abbruch.ThrowIfCancellationRequested();
            var mit = (PyObjekt)Py.Kopie(basis)!;
            ZugStueckeSetzen(mit, stuecke);
            if (Bauen3D.NeuRot(vorher, pruefen(mit)) is not { } f)
            {
                return stuecke;
            }
            var frei = Enumerable.Range(0, stuecke.Count).Where(i => stuecke[i].Frei).ToList();
            int weg;
            if (f.X is { } x && f.Y is { } y)
            {
                var stelle = new Vector2((float)x, (float)y);
                weg = frei.OrderBy(i => Vector2.Distance(stuecke[i].Lage, stelle)).First();
            }
            else
            {
                weg = frei.OrderBy(i => stuecke[i].Teil switch { ZugTeil.Haltestelle => 0, ZugTeil.Zebrastreifen => 1, _ => 2 }).First();
            }
            stuecke[weg] = stuecke[weg] with { Grund = Texte.ZugNachPruefung(f.Text) };
            if (!stuecke.Any(s => s.Frei))
            {
                return stuecke;
            }
        }
        return [.. stuecke.Select(s => s.Frei ? s with { Grund = Texte.ZugNachPruefungZuViel } : s)];
    }

    /// <summary>
    /// Strasse mit Ausstattung ziehen (ein Schritt fuer den Aufrufer): die Strasse wie beim Ziehen, ihre neuen Stuecke mit dem
    /// Gehweg der Vorlage, dann die freien Stuecke des Plans. <paramref name="plan"/> ist der Plan derselben Punkte (aus der
    /// Vorschau); null = neu planen (mit <paramref name="pruefen"/>). Rueckgabe: das zuletzt gesetzte Stueck der Strasse (null =
    /// keine Verbindung, dann aendert sich nichts), die Stellen der Ausstattung und der benutzte Plan.
    /// </summary>
    public static (AussenStelle? Strasse, List<AussenStelle> Neu, StrassenzugPlan Plan) StrassenzugZiehen(PyObjekt aussen, double ax, double ay, double bx, double by,
        bool raster, Strassenzug vorlage, StrassenzugPlan? plan = null, Func<PyObjekt, List<StadtbauFund>>? pruefen = null)
    {
        plan ??= StrassenzugPlanen(aussen, ax, ay, bx, by, raster, vorlage, pruefen);
        if (!plan.Gefunden)
        {
            return (null, [], plan);
        }
        var s = NurStrasse(aussen, ax, ay, bx, by, raster, vorlage);
        var neu = ZugStueckeSetzen(aussen, plan.Stuecke);
        return (s, neu, plan);
    }
}

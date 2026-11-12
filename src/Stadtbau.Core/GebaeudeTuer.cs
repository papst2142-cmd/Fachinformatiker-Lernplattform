using System.Numerics;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.24.0 (Weisung P12-100, Nico 1b 2a 3a 4a, Sammelliste 245): Tuer an einem Gebaeude ohne Innenraum auf Wunsch.
/// <list type="bullet">
/// <item><see cref="Dazu"/> (Knopf „Tür dazu“, ein Schritt): Tuer in der Mitte der Vorderseite (dreht mit dem Haus; das Spiel
/// setzt sie auf die echte Wand des Modells, <c>StadtTueren</c>), Fussweg gerade hinaus und bis zur naechsten Strasse
/// (<see cref="AussenBearbeitung.WegAnschliessen"/>), mit „Einrasten“ legt sich das Haus mit der Tuer an den Gehweg wie die
/// Laeden. Gespeichert wie beim Blueprint als Zierweg in <c>stadtdaten.darstellung.zierwege</c> (keine neuen Felder).</item>
/// <item>Das Spiel findet die Tuer eines Gebaeudes ohne Ort ueber den Namen des Modells und zeichnet sie nur am ersten Gebaeude
/// dieses Modells. Steht das Modell noch einmal (als Gebaeude oder als Ort), bekommt dieses Haus vorher eine eigene Kopie des
/// Modells (<c>wohnung_2</c>, Anzeige „Wohnung 2“) in <c>karte.modelle</c> (Nico 2a, Spiel unveraendert).</item>
/// <item><see cref="Entfernen"/> (Knopf „Tür entfernen“, ein Schritt): nimmt Tuer und Weg weg; die Kopie des Modells bleibt am Haus.
/// Loeschen des Hauses nimmt eine solche Kopie mit, wenn sie sonst niemand nutzt (<see cref="KopieAufraeumen"/>).</item>
/// </list>
/// </summary>
public static partial class GebaeudeTuer
{
    /// <summary>Weg aus der Tuer gerade hinaus, bevor er zur Strasse verlaengert wird.</summary>
    public const float WegStart = 0.5f;

    /// <summary>Pruefpunkt vor der Tuer (darf nicht in einem anderen Haus oder im Wasser liegen).</summary>
    public const float VorDerTuer = 0.3f;

    /// <summary>Bahnhof weiter als das vom Gleis: gelber Hinweis (Nico 3a).</summary>
    public const double BahnhofGleisAbstand = 1.5;

    /// <summary>Gegenprobe: Tuer ohne Kopie des Modells (zweites Haus gleichen Modells bekaeme keine eigene Tuer). Nur Tests.</summary>
    internal static readonly AsyncLocal<bool> OhneKopieFuerTests = new();

    /// <summary>Gegenprobe S 0.28.0: „Tür dazu“ wie bis S 0.27.0 immer auf die Vorderseite (Sued bei Drehung 0). Nur Tests.</summary>
    internal static readonly AsyncLocal<bool> SuedAltFuerTests = new();

    [GeneratedRegex("^(.+)_([0-9]+)$")]
    private static partial Regex KopieMuster();

    /// <summary>Hat das Gebaeude eine Tuer, die das Spiel zeichnet (Zierweg seines Modells, nur am ersten Gebaeude)?</summary>
    public static bool HatTuer(PyObjekt aussen, AussenStelle s) => Blueprint.ZierwegVon(aussen, s) is not null;

    /// <summary>Warum „Tür dazu“ hier nicht geht (null = geht).</summary>
    public static string? DazuNicht(PyObjekt aussen, AussenStelle s) =>
        s.Art != AussenArt.Gebaeude || Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is not PyObjekt g ? Texte.TuerNurGebaeude
        // ein Bahnsteig-Stueck (nur Flaechen) und ein Gebaeude mit Innenraum (Blueprint, Tuer kommt mit dem Innenraum) nicht
        : Bahnsteig.Nur(Get(AussenBearbeitung.Modelle(aussen), Py.Str(g["modell"]))) || Aussen.Bauten(aussen)[Py.Str(g["modell"])] is PyObjekt ? Texte.TuerNurGebaeude
        : HatTuer(aussen, s) ? Texte.TuerSchonDa
        : null;

    /// <summary>Zeigt die Auswahl den Knopf „Tür dazu“ bzw. „Tür entfernen“? (nur Gebaeude ohne Innenraum, kein Bahnsteig)</summary>
    public static bool KnopfZeigen(PyObjekt aussen, AussenStelle s) => DazuNicht(aussen, s) is null || DazuNicht(aussen, s) == Texte.TuerSchonDa;

    /// <summary>
    /// Tuer und Fussweg an das Gebaeude <paramref name="s"/>. Rueckgabe: gelber Hinweis (Weg erreicht keine Strasse) oder null.
    /// Wirft nichts; geht es nicht (<see cref="DazuNicht"/>), bleibt alles, Rueckgabe der Grund.
    /// </summary>
    /// <param name="punkt">S 0.28.0 (Weisung P12-121): Tuer an die Wand bei diesem Punkt (<see cref="TuerSetzen"/>); ohne Punkt
    /// an die Wand, die der naechsten Strasse zugewandt ist (<see cref="TuerSetzen.WandZurStrasse"/>), nicht mehr immer vorne.</param>
    /// <param name="modellTueren">Tueren des CC0-Modells zum Einrasten (nur mit <paramref name="punkt"/>).</param>
    public static string? Dazu(PyObjekt aussen, AussenStelle s, bool einrasten, Vector2? punkt = null, IReadOnlyList<Vector2>? modellTueren = null)
    {
        if (DazuNicht(aussen, s) is { } nicht)
        {
            return nicht;
        }
        var g = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var modell = Py.Str(g["modell"]);
        var teile = Get(AussenBearbeitung.Modelle(aussen), modell);
        if (Bahnsteig.Hat(teile) && (BahnsteigTeilenAlt.Value || !BlockAufBahnsteig(teile)))
        {
            // Bahnhof (Nico 3a): das Spiel setzt die Tuer auf den Kasten des ganzen Modells, der die Bahnsteige einschliesst.
            // Darum bekommt der Bahnhof eine Kopie ohne Bahnsteig-Flaechen, der Bahnsteig bleibt als eigenes Stueck an seiner
            // Stelle stehen (wie beim Tausch des Aussehens, S 0.21.0). Lage und Drehung bleiben (Mitte der Bloecke).
            var dreh = Hausdrehung.Von(g);
            var (gx, gy) = (Py.F(g["x"]), Py.F(g["y"]));
            modell = KopieAnlegen(aussen, modell, ohneBahnsteig: true);
            Py.Setzen(g, "modell", modell);
            OrtAussehen.BahnsteigStehenLassen(aussen, modell, teile, gx, gy, dreh);
        }
        else if (!OhneKopieFuerTests.Value && ModellAnderweitig(aussen, g, modell))
        {
            modell = KopieAnlegen(aussen, modell);
            Py.Setzen(g, "modell", modell);
        }
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(aussen, s);
        Vector2 tuer, n;
        if (punkt is { } p)
        {
            (tuer, n) = TuerSetzen.AufWand((x, y, w, h), p, modellTueren);
        }
        else
        {
            n = SuedAltFuerTests.Value ? AussenBearbeitung.HausFront(aussen, s) : TuerSetzen.WandZurStrasse(aussen, s);
            var mitte = new Vector2((float)(x + (w / 2)), (float)(y + (h / 2)));
            var tief = n.X != 0 ? w / 2 : h / 2;
            tuer = mitte + (n * (float)tief);
        }
        var weg = new List<object?> { Punkt(tuer), Punkt(tuer + (n * WegStart)) };
        Zierwege(aussen, anlegen: true)!.Add(new PyObjekt
        {
            { "name", ModellNamen.Anzeige(aussen, modell) },
            { "modell", modell },
            { "tuer", Punkt(tuer) },
            { "weg", weg },
        });
        if (einrasten)
        {
            AussenBearbeitung.HausAnGehweg(aussen, s);
        }
        var z = Blueprint.Zierweg(aussen, modell)!;
        var strassen = AussenBearbeitung.Strassen(aussen);
        // S 0.34.1 (Sammelliste 278/280): rechtwinklig mit runden Ecken
        return WegRechtwinklig(z, AussenBearbeitung.HausFlaeche(aussen, s), strassen, LaternenPunkte(aussen)) ? null : Texte.TuerWegOhneStrasse;
    }

    /// <summary>Gegenprobe Sammelliste 313: Bahnsteig auch abtrennen, wenn das Haus darauf steht (wie bis S 0.44.0, nur Tests, je Ablauf).</summary>
    public static bool BahnsteigTeilenAltFuerTests { get => BahnsteigTeilenAlt.Value; set => BahnsteigTeilenAlt.Value = value; }
    private static readonly AsyncLocal<bool> BahnsteigTeilenAlt = new();

    /// <summary>
    /// Sammelliste 313 (Weisung P12-207): steht ein Block des Modells auf einer Bahnsteig-Flaeche (Haltepunkt: Wartehaus auf dem
    /// Bahnsteig), gehoeren Haus und Bahnsteig zusammen. Abgetrennt stuenden sie ineinander (roter Fund); die Tuer kommt dann auf
    /// die Wand des Blocks (<see cref="TuerFassade"/>), nicht auf die Kante des Bahnsteigs.
    /// </summary>
    public static bool BlockAufBahnsteig(object? teile)
    {
        var liste = (teile as List<object?> ?? []).OfType<PyObjekt>().ToList();
        // Tiefe: Block „t“, Flaeche „h“ (wie Bahnsteig.Rechtecke)
        static (double X1, double Y1, double X2, double Y2) R(PyObjekt t, string tiefe) =>
            (Py.F(Get(t, "x") ?? 0L), Py.F(Get(t, "y") ?? 0L), Py.F(Get(t, "x") ?? 0L) + Py.F(Get(t, "w") ?? 0L), Py.F(Get(t, "y") ?? 0L) + Py.F(Get(t, tiefe) ?? 0L));
        var flaechen = liste.Where(Bahnsteig.IstFlaeche).Select(t => R(t, "h")).ToList();
        return liste.Where(t => Py.Gleich(Get(t, "art"), "block")).Select(t => R(t, "t"))
            .Any(b => flaechen.Any(f => b.X1 < f.X2 - 1e-6 && f.X1 < b.X2 - 1e-6 && b.Y1 < f.Y2 - 1e-6 && f.Y1 < b.Y2 - 1e-6));
    }

    /// <summary>Gegenprobe Sammelliste 293: „Tür dazu“ wie bis S 0.41.0 in der Wandmitte ohne Fassade (nur Tests, je Ablauf).</summary>
    public static bool MitteAltFuerTests { get => MitteAlt.Value; set => MitteAlt.Value = value; }
    private static readonly AsyncLocal<bool> MitteAlt = new();

    /// <summary>
    /// Sammelliste 293 (Weisung P12-196): Platz fuer „Tür dazu“ auf der gezeichneten Fassade (rechnet auf einer Kopie, darf im
    /// Hintergrund laufen). Wand zur naechsten Strasse wie bisher; dort zuerst eine Tuer des Modells (die naechste zur Wandmitte),
    /// sonst die glatte Stelle, die der Wandmitte am naechsten liegt (<see cref="TuerFassade.Finden"/> mit Einrasten); hat diese
    /// Wand keine glatte Stelle, die naechste Wand mit einer (nach Abstand der Wandmitte zur Strasse). null = alte Mitte
    /// (Gegenprobe, kein Haus, „Tür dazu“ geht nicht).
    /// </summary>
    public static TuerFassade.Platz? DazuPlatz(Projekt p, PyObjekt aussen, AussenStelle s, bool einrasten)
    {
        if (MitteAlt.Value || DazuNicht(aussen, s) is not null)
        {
            return null;
        }
        var k = (PyObjekt)Py.Kopie(aussen)!;
        var grund = Dazu(k, s, einrasten);
        if (grund is not null && grund != Texte.TuerWegOhneStrasse || Blueprint.ZierwegVon(k, s) is not { } z
            || TuerFassade.Rechnen(p, k, s) is not { } f || f.Waende.Count == 0)
        {
            return null;
        }
        var t = Vektor(z["tuer"]);
        var strassen = AussenBearbeitung.Strassen(k).Where(l => l.Count >= 2).ToList();
        var start = f.WandFuer(t, null);
        float ZurStrasse(int w)
        {
            var m = f.Waende[w].Bei(f.Waende[w].Laenge / 2) + f.Waende[w].Aussen;
            return strassen.Count == 0 ? 0 : strassen.Min(l => Kurve.Abstand(m, l));
        }
        var reihe = new[] { start }.Concat(Enumerable.Range(0, f.Waende.Count).Where(i => i != start).OrderBy(ZurStrasse).ThenBy(i => i)).ToList();
        // Welle S 0.45.0 (Weisung P12-212 Punkt 1, Nico): an einem Gebaeude mit Toren (Betriebswerkstatt, Lokschuppen, Hallen)
        // kommt der Eingang auf eine glatte Wand OHNE Tor, nie in ein Tor; erst wenn keine solche Wand eine glatte Stelle hat,
        // gilt die alte Reihe (Tor als letzte Wahl). Von Hand bleibt die Tuer frei (TuerSetzen).
        var tore = TorAlt.Value ? [] : TorWaende(k, s, f);
        if (tore.Count > 0)
        {
            foreach (var w in reihe.Where(i => !tore.Contains(i)))
            {
                var wand = f.Waende[w];
                var mitte = wand.Bei(wand.Laenge / 2);
                var ziel = f.TuerZiele(w, ohneTore: true).OrderBy(m => MathF.Abs(m.T - (wand.Laenge / 2))).Select(m => (Vector2?)wand.Bei(m.T)).FirstOrDefault() ?? mitte;
                var platz = f.Finden(ziel + (wand.Aussen * 0.01f), w, einrasten: true);
                if (!platz.OhneGlatte && platz.Wand == w && !platz.Eigene && platz.Art != TuerFassade.Stelle.Nicht)
                {
                    return platz;
                }
            }
        }
        foreach (var w in reihe)
        {
            var wand = f.Waende[w];
            var mitte = wand.Bei(wand.Laenge / 2);
            var ziel = f.TuerZiele(w).OrderBy(m => MathF.Abs(m.T - (wand.Laenge / 2))).Select(m => (Vector2?)wand.Bei(m.T)).FirstOrDefault() ?? mitte;
            var platz = f.Finden(ziel + (wand.Aussen * 0.01f), w, einrasten: true);
            if (!platz.OhneGlatte && platz.Wand == w)
            {
                return platz;
            }
        }
        return f.Finden(t, start, einrasten: true);
    }

    /// <summary>Gegenprobe Weisung P12-212 Punkt 1: „Tür dazu“ darf wie bis S 0.44.1 ein Tor nehmen (nur Tests, je Ablauf).</summary>
    public static bool TorAltFuerTests { get => TorAlt.Value; set => TorAlt.Value = value; }
    private static readonly AsyncLocal<bool> TorAlt = new();

    /// <summary>
    /// Weisung P12-212 Punkt 1: Waende der Fassade mit einem Tor. Ein Tor ist (a) ein Teil <c>tor</c> des Modells (Lokschuppen,
    /// Betriebswerkstatt: liegt an der Seite des Kastens, der es am naechsten ist, gedreht mit dem Haus) oder (b) eine eigene
    /// Tuer des Modells, die das Spiel nicht als Tuer erkennt (helles Rolltor, <see cref="TuerFassade.HatTor"/>).
    /// </summary>
    public static HashSet<int> TorWaende(PyObjekt aussen, AussenStelle s, TuerFassade f)
    {
        var tore = new HashSet<int>(Enumerable.Range(0, f.Waende.Count).Where(f.HatTor));
        var modell = s.Art == AussenArt.Haus ? Py.Str(AussenBearbeitung.Ort(aussen, s.Ort)!["modell"]) : Py.Str(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["modell"]);
        var teile = (Get(AussenBearbeitung.Modelle(aussen), modell) as List<object?> ?? []).OfType<PyObjekt>().ToList();
        var bloecke = teile.Where(t => Py.Gleich(Get(t, "art"), "block")).ToList();
        if (bloecke.Count == 0)
        {
            return tore;
        }
        static double F(PyObjekt t, string k) => Py.F(Get(t, k) ?? 0L);
        var (x1, y1) = (bloecke.Min(b => F(b, "x")), bloecke.Min(b => F(b, "y")));
        var (x2, y2) = (bloecke.Max(b => F(b, "x") + F(b, "w")), bloecke.Max(b => F(b, "y") + F(b, "t")));
        var dreh = AussenBearbeitung.HausDreh(aussen, s);
        foreach (var tor in teile.Where(t => Py.Gleich(Get(t, "art"), "tor")))
        {
            var (tx1, tx2, tyU) = (F(tor, "x"), F(tor, "x") + F(tor, "w"), F(tor, "y") + F(tor, "h"));
            var seiten = new (double D, Vector2 N)[]
            {
                (Math.Abs(y2 - tyU), new(0, 1)), (Math.Abs(F(tor, "y") - y1), new(0, -1)), (Math.Abs(tx1 - x1), new(-1, 0)), (Math.Abs(x2 - tx2), new(1, 0)),
            };
            var n = Hausdrehung.Drehen(seiten.MinBy(x => x.D).N, Vector2.Zero, dreh);
            for (var w = 0; w < f.Waende.Count; w++)
            {
                if (Vector2.Distance(f.Waende[w].Aussen, n) < 0.01f)
                {
                    tore.Add(w);
                }
            }
        }
        return tore;
    }

    /// <summary>
    /// „Tür dazu“ als ein Schritt: wie <see cref="Dazu"/>, danach die Tuer an den Platz aus <see cref="DazuPlatz"/> (gerechnet
    /// auf demselben Stand). Ohne Platz bleibt die Tuer in der Wandmitte (wie bis S 0.41.0).
    /// </summary>
    public static string? DazuAn(PyObjekt aussen, AussenStelle s, bool einrasten, TuerFassade.Platz? platz)
    {
        var grund = Dazu(aussen, s, einrasten);
        if (platz is null || (grund is not null && grund != Texte.TuerWegOhneStrasse))
        {
            return grund;
        }
        // die verschobene Stelle ist hier gewollt (Mitte war nur der Anfang), darum kein Hinweis „eingerastet“
        var h = TuerSetzen.SetzenAn(aussen, s, platz);
        if (platz.Eigene && !TorAlt.Value)
        {
            // P12-212 Punkt 1: im Tor nur, wenn keine Wand ohne Tor eine glatte Stelle hat (DazuPlatz sucht dort zuerst)
            return Texte.TuerImTorKeineSeite;
        }
        return h == Texte.TuerEingerastetGlatt ? null : h;
    }

    private static Vector2 Vektor(object? o) => o is List<object?> l ? new Vector2((float)Py.F(l[0]), (float)Py.F(l[1])) : default;

    /// <summary>Tuer und Fussweg des Gebaeudes wegnehmen (die Kopie des Modells bleibt am Haus). false = es hatte keine.</summary>
    public static bool Entfernen(PyObjekt aussen, AussenStelle s)
    {
        if (!HatTuer(aussen, s) || Zierwege(aussen, anlegen: false) is not { } zw)
        {
            return false;
        }
        var modell = Py.Str(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["modell"]);
        zw.RemoveAll(e => e is PyObjekt o && Py.Gleich(o["modell"], modell));
        return true;
    }

    /// <summary>
    /// Nach dem Loeschen eines Gebaeudes: eine Kopie (<c>modell_n</c> mit denselben Teilen wie <c>modell</c>), die kein Haus und
    /// kein Ort mehr nutzt, faellt aus <c>karte.modelle</c> weg.
    /// </summary>
    public static void KopieAufraeumen(PyObjekt aussen, string modell)
    {
        if (!IstKopie(aussen, modell) || Genutzt(aussen, modell) || Get(Aussen.Karte(aussen), "modelle") is not PyObjekt m)
        {
            return;
        }
        var rest = m.Where(e => e.Key != modell).ToList();
        m.Clear();
        m.AddRange(rest);
    }

    /// <summary>Ist <paramref name="modell"/> eine Kopie, die „Tür dazu“ angelegt hat (gleiche Teile wie das Grundmodell)?</summary>
    public static bool IstKopie(PyObjekt aussen, string modell)
    {
        if (KopieMuster().Match(modell) is not { Success: true } t)
        {
            return false;
        }
        var alle = AussenBearbeitung.Modelle(aussen);
        return Get(alle, t.Groups[1].Value) is { } grund && Get(alle, modell) is { } kopie
            && (PythonJson.Standard(grund) == PythonJson.Standard(kopie) || PythonJson.Standard(OhneBahnsteig(grund)) == PythonJson.Standard(kopie));
    }

    /// <summary>Grundmodell einer Kopie (sonst das Modell selbst) und ihre Nummer (sonst null).</summary>
    public static (string Grund, int? Nummer) Teile(PyObjekt aussen, string modell) =>
        IstKopie(aussen, modell) && KopieMuster().Match(modell) is { } t ? (t.Groups[1].Value, int.Parse(t.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) : (modell, null);

    /// <summary>Steht das Modell noch woanders (anderes Gebaeude oder ein Ort)? Dann braucht dieses Haus eine eigene Kopie.</summary>
    private static bool ModellAnderweitig(PyObjekt aussen, PyObjekt g, string modell) =>
        Aussen.Landschaft(aussen).OfType<PyObjekt>().Any(t => !ReferenceEquals(t, g) && Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], modell))
        || AussenBearbeitung.Orte(aussen).Any(o => Py.Gleich(o["modell"], modell));

    private static bool Genutzt(PyObjekt aussen, string modell) =>
        Aussen.Landschaft(aussen).OfType<PyObjekt>().Any(t => Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], modell))
        || AussenBearbeitung.Orte(aussen).Any(o => Py.Gleich(o["modell"], modell));

    /// <summary>Kopie <c>modell_2</c>, <c>_3</c> … (naechste freie Nummer) mit denselben Teilen in <c>karte.modelle</c>.</summary>
    private static List<object?> OhneBahnsteig(object? teile) => [.. (teile as List<object?> ?? []).Where(x => !Bahnsteig.IstFlaeche(x))];

    private static string KopieAnlegen(PyObjekt aussen, string modell, bool ohneBahnsteig = false)
    {
        var alle = AussenBearbeitung.Modelle(aussen);
        var grund = Teile(aussen, modell).Grund;
        var nr = 2;
        while (Get(alle, grund + "_" + nr.ToString(System.Globalization.CultureInfo.InvariantCulture)) is not null)
        {
            nr++;
        }
        var neu = grund + "_" + nr.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var karte = Aussen.Karte(aussen);
        if (Get(karte, "modelle") is not PyObjekt km)
        {
            km = new PyObjekt();
            Py.Setzen(karte, "modelle", km);
        }
        Py.Setzen(km, neu, ohneBahnsteig ? Py.Kopie(OhneBahnsteig(Get(alle, grund))) : Py.Kopie(Get(alle, grund)));
        return neu;
    }

    private static List<object?>? Zierwege(PyObjekt aussen, bool anlegen)
    {
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(sd, "darstellung") is not PyObjekt d)
        {
            if (!anlegen)
            {
                return null;
            }
            d = new PyObjekt();
            Py.Setzen(sd, "darstellung", d);
        }
        if (Get(d, "zierwege") is not List<object?> zw)
        {
            if (!anlegen)
            {
                return null;
            }
            zw = [];
            Py.Setzen(d, "zierwege", zw);
        }
        return zw;
    }

    private static List<Vector2> LaternenPunkte(PyObjekt aussen) =>
        [.. Aussen.Laternen(aussen).OfType<List<object?>>().Select(l => new Vector2((float)Py.F(l[0]), (float)Py.F(l[1])))];

    private static List<object?> Punkt(Vector2 p) => [Bearbeitung.Zahl(Math.Round(p.X, 2)), Bearbeitung.Zahl(Math.Round(p.Y, 2))];

    // ---- Pruefung -----------------------------------------------------------------------------------

    /// <summary>
    /// Funde zu Tueren an Gebaeuden ohne Ort (Weisung P12-100, Kurzplan P12-65 Punkt 4): rot, wenn der Platz vor der Tuer in
    /// einem anderen Haus oder im Wasser liegt; gelb, wenn ein Bahnhof weiter als <see cref="BahnhofGleisAbstand"/> vom Gleis
    /// steht. Ein Weg ohne Strasse meldet schon die Pruefung der Fusswege des Spiels (Zierweg) nicht; darum hier gelb.
    /// </summary>
    public static List<StadtbauFund> Pruefen(PyObjekt aussen)
    {
        var funde = new List<StadtbauFund>();
        var haeuser = AussenBearbeitung.Alle(aussen).Where(a => a.Stelle.Art is AussenArt.Haus or AussenArt.Gebaeude).Select(a => a.Stelle).ToList();
        var strassen = AussenBearbeitung.Strassen(aussen);
        var land = Aussen.Landschaft(aussen);
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is not PyObjekt g || !Py.Gleich(g["typ"], "gebaeude"))
            {
                continue;
            }
            var s = new AussenStelle(AussenArt.Gebaeude, i);
            var modell = Py.Str(g["modell"]);
            var name = ModellNamen.Anzeige(aussen, modell);
            var (x, y, w, h) = AussenBearbeitung.HausFlaeche(aussen, s);
            var (mx, my) = (x + (w / 2), y + (h / 2));
            if (ModellNamen.IstBahnhof(Teile(aussen, modell).Grund) && GleisAbstand(aussen, x, y, w, h) > BahnhofGleisAbstand)
            {
                funde.Add(new StadtbauFund(Texte.BahnhofNichtAmGleis(name), Texte.BahnhofNichtAmGleisTipp, false, mx, my));
            }
            if (Blueprint.ZierwegVon(aussen, s) is not { } z || z["tuer"] is not List<object?> t || z["weg"] is not List<object?> weg)
            {
                continue;
            }
            var tuer = new Vector2((float)Py.F(t[0]), (float)Py.F(t[1]));
            var n = AussenBearbeitung.HausTuerSeite(aussen, s);
            var vor = tuer + (n * VorDerTuer);
            foreach (var anderes in haeuser.Where(a => a != s))
            {
                var (ax, ay, aw, ah) = AussenBearbeitung.HausFlaeche(aussen, anderes);
                if (vor.X > ax && vor.X < ax + aw && vor.Y > ay && vor.Y < ay + ah)
                {
                    funde.Add(new StadtbauFund(Texte.TuerAnHaus(name), Texte.TuerAnHausTipp, true, tuer.X, tuer.Y));
                    break;
                }
            }
            if (ImWasser(aussen, vor))
            {
                funde.Add(new StadtbauFund(Texte.TuerImWasser(name), Texte.TuerImWasserTipp, true, tuer.X, tuer.Y));
            }
            var ende = weg.OfType<List<object?>>().LastOrDefault();
            if (ende is null || !strassen.Any(l => l.Count >= 2 && Kurve.Abstand(new Vector2((float)Py.F(ende[0]), (float)Py.F(ende[1])), l) <= 0.02f))
            {
                funde.Add(new StadtbauFund(Texte.TuerWegOhneStrasse + " („" + name + "“)", Texte.TuerWegOhneStrasseTipp, false, tuer.X, tuer.Y));
            }
        }
        return funde;
    }

    private static bool ImWasser(PyObjekt aussen, Vector2 p)
    {
        foreach (var teich in Aussen.Teiche(aussen))
        {
            if (Wasser.Punkte(teich) is { Count: >= 3 } eck && Wasser.Drin(new Teich(eck).MitUfer, p))
            {
                return true;
            }
        }
        return Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "fluss"))
            .Any(l => Linie(l["punkte"]) is { Count: >= 2 } f && Kurve.Abstand(p, f) <= AussenBearbeitung.LinienHalb(AussenArt.Fluss));
    }

    /// <summary>Kleinster Abstand der Grundflaeche zu einem Gleis (ohne Gleis: unendlich).</summary>
    private static double GleisAbstand(PyObjekt aussen, double x, double y, double w, double h)
    {
        var rand = new List<Vector2>();
        for (var k = 0; k <= 8; k++)
        {
            var f = k / 8f;
            rand.Add(new((float)(x + (w * f)), (float)y));
            rand.Add(new((float)(x + (w * f)), (float)(y + h)));
            rand.Add(new((float)x, (float)(y + (h * f))));
            rand.Add(new((float)(x + w), (float)(y + (h * f))));
        }
        var best = double.PositiveInfinity;
        foreach (var l in Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "bahn")))
        {
            if (Linie(l["punkte"]) is { Count: >= 2 } gleis)
            {
                best = Math.Min(best, rand.Min(p => Kurve.Abstand(p, gleis)));
            }
        }
        return best;
    }

    private static List<Vector2> Linie(object? punkte) =>
        [.. (punkte as List<object?> ?? []).OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])))];
}

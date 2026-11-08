using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Eine Stelle, an der ein Uebergang haelt: Ort (Innenraum), Etage (0 = Erdgeschoss) und die Ankunftsstelle (x, y im
/// Grundriss dieser Etage, Blick in Grad um die Hochachse wie <c>StadtSpiel.FigurRichtung</c>: 0 = nach +y, 90 = nach +x).
/// </summary>
public sealed record Halt(string Ort, int Etage, double X, double Y, long Blick);

/// <summary>
/// Phase 13 Welle 5 (Weisung P12-73, Plan P12-23b Abschnitte 1 bis 3): ein Uebergang von einer Stelle zu einer anderen,
/// allgemein gebaut. Heute verbindet er nur Etagen desselben Hauses (Art <see cref="Etagen.Treppe"/> oder
/// <see cref="Etagen.Fahrstuhl"/>); spaeter nutzt der Zug dieselbe Form mit einem Halt in einer anderen Stadt (Art "zug",
/// bis Beta 1 unbekannt und rot). Eine Treppe hat genau zwei Halte uebereinander, ein Fahrstuhl einen Halt je Etage, auf
/// der er steht.
/// </summary>
public sealed record Uebergang(string Id, string Art, IReadOnlyList<Halt> Halte)
{
    /// <summary>Der Halt auf Etage <paramref name="etage"/> (null = der Uebergang haelt dort nicht).</summary>
    public Halt? Auf(int etage) => Halte.FirstOrDefault(h => h.Etage == etage);

    /// <summary>Ziel eines Wechsels von Etage <paramref name="von"/> nach <paramref name="nach"/> (null = geht nicht).</summary>
    public Halt? Ziel(int von, int nach) => von != nach && Auf(von) is not null ? Auf(nach) : null;

    /// <summary>Etagen, die man von <paramref name="von"/> aus erreicht (ohne die eigene), aufsteigend.</summary>
    public IReadOnlyList<int> Ziele(int von) => Auf(von) is null ? [] : [.. Halte.Select(h => h.Etage).Where(e => e != von).Distinct().Order()];
}

/// <summary>
/// Phase 13 Welle 5 „Etagen“ (Weisung P12-73 mit P12-34, Nico 1a 2b 3a 4a): Daten der Etagen eines Innenraums. Sie stehen im
/// Grundriss des Erdgeschosses (<c>innenraeume[ort]["gebaeude"]</c>) in zwei Feldern, die das Spiel bis W5 nicht kannte und
/// die ohne Etagen fehlen (Fassung 0 hat keine, sie bleibt Byte fuer Byte gleich):
/// <list type="bullet">
/// <item><c>etagen</c>: Liste der Obergeschosse, je Eintrag <c>{"name", "gebaeude"}</c> (Grundriss wie das Erdgeschoss, ohne
/// Eingangstuer); Eintrag 1 ist das 1. Obergeschoss.</item>
/// <item><c>uebergaenge</c>: Liste <c>{"id", "art", "halte": [{"etage", "ankunft": [x, y, blick]}]}</c>; das Stueck dazu ist
/// eine Einrichtung der Etage (Typ <see cref="Treppe"/> oder <see cref="Fahrstuhl"/>) mit dem Feld <c>"uebergang": id</c>.</item>
/// </list>
/// Im Werkzeug heisst eine Etage <c>ort@k</c> (<see cref="Schluessel"/>), im Spiel ist die Ansicht <c>geschaeft:ort@k</c>.
/// </summary>
public static class Etagen
{
    public const string Treppe = "treppe";

    public const string Fahrstuhl = "fahrstuhl";

    /// <summary>Arten, die W5 kennt. "zug" kommt mit Beta 1 (eigene Weisung) und ist bis dahin unbekannt.</summary>
    public static readonly IReadOnlyList<string> Arten = [Treppe, Fahrstuhl];

    /// <summary>Hoechstens so viele Etagen ueber dem Erdgeschoss (Nico 4a).</summary>
    public const int Hoechstens = 4;

    public const string FeldEtagen = "etagen";

    public const string FeldUebergaenge = "uebergaenge";

    /// <summary>Feld an der Einrichtung, die das Stueck eines Uebergangs ist.</summary>
    public const string FeldLink = "uebergang";

    /// <summary>Trennzeichen zwischen Ort und Etage in Schluesseln und Ansichten.</summary>
    public const char Trenner = '@';

    /// <summary>Abstand der Ankunftsstelle vor dem Stueck (ein Schritt).</summary>
    public const double Schritt = 0.6;

    /// <summary>
    /// Neue Ereignisart (Nico 2b, Weisung P12-34): Aufenthalt = Stadt, Ort, Etage. Daten <c>{"tag", "stadt", "ort", "etage",
    /// "uebergang"}</c>; "etage" ist null, wenn die Figur das Haus verlassen hat. Geschrieben nur beim Etagenwechsel und beim
    /// Verlassen eines Hauses mit Etagen; der Spielstand rechnet nichts damit (Fingerabdruecke bleiben).
    /// </summary>
    public const string Aufenthalt = "aufenthalt";

    /// <summary>Schluessel einer Etage im Werkzeug (Etage 0 = der Ort selbst).</summary>
    public static string Schluessel(string ort, int etage) => etage == 0 ? ort : ort + Trenner + etage.ToString(CultureInfo.InvariantCulture);

    /// <summary>Ort und Etage aus einem Schluessel oder einer Ansicht ("moebelhaus@2" = moebelhaus, 2).</summary>
    public static (string Ort, int Etage) Teilen(string schluessel)
    {
        var i = schluessel.LastIndexOf(Trenner);
        if (i > 0 && int.TryParse(schluessel.AsSpan(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var k) && k > 0)
        {
            return (schluessel[..i], k);
        }
        return (schluessel, 0);
    }

    /// <summary>Name einer Etage ohne eigenen Namen.</summary>
    public static string StandardName(int etage) => etage == 0 ? "Erdgeschoss" : etage.ToString(CultureInfo.InvariantCulture) + ". Obergeschoss";

    /// <summary>Die Obergeschosse (leer = keine).</summary>
    public static List<object?> Liste(PyObjekt eg) => Get(eg, FeldEtagen) as List<object?> ?? [];

    /// <summary>Zahl der Obergeschosse.</summary>
    public static int Anzahl(PyObjekt eg) => Liste(eg).Count;

    /// <summary>Hat der Grundriss Etagen oder Uebergaenge?</summary>
    public static bool Hat(PyObjekt eg) => Py.Hat(eg, FeldEtagen) || Py.Hat(eg, FeldUebergaenge);

    /// <summary>Grundriss der Etage (0 = das Erdgeschoss selbst). Fehlt sie: <see cref="KeyNotFoundException"/>.</summary>
    public static PyObjekt Gebaeude(PyObjekt eg, int etage)
    {
        if (etage == 0)
        {
            return eg;
        }
        var liste = Liste(eg);
        return etage > 0 && etage <= liste.Count && liste[etage - 1] is PyObjekt e && Get(e, "gebaeude") is PyObjekt g
            ? g
            : throw new KeyNotFoundException("Etage " + etage.ToString(CultureInfo.InvariantCulture) + " fehlt.");
    }

    /// <summary>Gibt es diese Etage?</summary>
    public static bool Gibt(PyObjekt eg, int etage) => etage == 0 || (etage > 0 && etage <= Anzahl(eg) && Liste(eg)[etage - 1] is PyObjekt e && Get(e, "gebaeude") is PyObjekt);

    /// <summary>Name der Etage (eigener Name oder <see cref="StandardName"/>).</summary>
    public static string Name(PyObjekt eg, int etage) =>
        etage > 0 && etage <= Anzahl(eg) && Liste(eg)[etage - 1] is PyObjekt e && Get(e, "name") is string { Length: > 0 } n ? n : StandardName(etage);

    /// <summary>
    /// Vorschlag fuer eine neue Etage: die Aussenwaende (Breite, Tiefe, Fenster) und der Flur des Erdgeschosses, ohne
    /// Eingangstuer, ohne Raeume und ohne Einrichtung (Plan Abschnitt 1).
    /// </summary>
    public static PyObjekt Vorschlag(PyObjekt eg)
    {
        var flur = Objekt(eg["flur"]);
        var neuFlur = new PyObjekt { { "y", Py.Kopie(flur["y"]) }, { "h", Py.Kopie(flur["h"]) }, { "name", "Flur" } };
        if (Get(flur, "boden") is string boden)
        {
            neuFlur.Add("boden", boden);
        }
        var breite = Py.F(eg["breite"]);
        neuFlur.Add("spieler", new List<object?> { Math.Round(breite / 2, 2), Math.Round(Py.F(flur["y"]) + (Py.F(flur["h"]) / 2), 2) });
        neuFlur.Add("deko", new List<object?>());
        return new PyObjekt
        {
            { "breite", Py.Kopie(eg["breite"]) }, { "hoehe", Py.Kopie(eg["hoehe"]) }, { "fenster", Py.Kopie(Get(eg, "fenster") ?? new List<object?>()) },
            { "flur", neuFlur }, { "raeume", new List<object?>() },
        };
    }

    /// <summary>
    /// Feld nur in der Darstellung des Spiels (Weisung P12-74): eine Treppe, die von ihrer Etage nur nach unten fuehrt,
    /// traegt <c>"richtung": "unten"</c> und wird als Treppenloch mit Stufen nach unten gezeichnet. Nie gespeichert.
    /// </summary>
    public const string FeldRichtung = "richtung";

    /// <summary>Wert von <see cref="FeldRichtung"/> fuer eine Treppe nur nach unten.</summary>
    public const string NachUnten = "unten";

    /// <summary>
    /// Grundriss der Etage fuer die Darstellung im Spiel (Weisung P12-74): Kopie, in der jede Treppe, deren Ziele alle
    /// unter <paramref name="etage"/> liegen, <see cref="FeldRichtung"/> = <see cref="NachUnten"/> traegt. Ohne solche
    /// Treppe der Grundriss selbst (keine Kopie).
    /// </summary>
    public static PyObjekt FuerDarstellung(PyObjekt eg, string ort, int etage)
    {
        var g = Gebaeude(eg, etage);
        var unten = Uebergaenge(eg, ort)
            .Where(u => u.Art == Treppe && u.Ziele(etage) is { Count: > 0 } z && z.All(e => e < etage))
            .Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        if (unten.Count == 0)
        {
            return g;
        }
        var kopie = (PyObjekt)Py.Kopie(g)!;
        foreach (var d in Einrichtung(kopie).Where(d => Py.Gleich(Get(d, "typ"), Treppe) && Get(d, FeldLink) is string id && unten.Contains(id)))
        {
            Py.Setzen(d, FeldRichtung, NachUnten);
        }
        return kopie;
    }

    /// <summary>Ist der Typ das Stueck eines Uebergangs (Treppe, Fahrstuhl)?</summary>
    public static bool IstStueck(string typ) => Arten.Contains(typ);

    /// <summary>Alle Einrichtungen eines Grundrisses (Raeume und Flur).</summary>
    public static IEnumerable<PyObjekt> Einrichtung(PyObjekt g) =>
        InnenGrundriss.Einrichtung(g).Select(e => e.Deko);

    /// <summary>Die Stuecke des Uebergangs <paramref name="id"/> auf einem Grundriss.</summary>
    public static List<PyObjekt> Stuecke(PyObjekt g, string id) =>
        [.. Einrichtung(g).Where(d => Get(d, FeldLink) is string s && s == id)];

    /// <summary>
    /// Die Uebergaenge des Grundrisses, so weit lesbar (kaputte Eintraege fehlen hier; die Pruefung meldet sie rot).
    /// <paramref name="ort"/> ist der Ort, zu dem der Grundriss gehoert.
    /// </summary>
    public static List<Uebergang> Uebergaenge(PyObjekt eg, string ort)
    {
        var liste = new List<Uebergang>();
        foreach (var e in Get(eg, FeldUebergaenge) as List<object?> ?? [])
        {
            if (e is not PyObjekt u || Get(u, "id") is not string id || Get(u, "art") is not string art || Get(u, "halte") is not List<object?> halte)
            {
                continue;
            }
            var hs = new List<Halt>();
            foreach (var h in halte)
            {
                if (h is PyObjekt ho && Get(ho, "etage") is var et && Py.IstNurInt(et) && Get(ho, "ankunft") is List<object?> { Count: 3 } a
                    && IstZahl(a[0]) && IstZahl(a[1]) && Py.IstNurInt(a[2]))
                {
                    hs.Add(new Halt(ort, (int)Py.Int(et), Py.F(a[0]), Py.F(a[1]), Py.Int(a[2])));
                }
            }
            liste.Add(new Uebergang(id, art, hs));
        }
        return liste;
    }

    internal static bool IstZahl(object? w) => w is long or int or double;

    /// <summary>Der Uebergang mit der Kennung (null = keiner).</summary>
    public static Uebergang? Von(PyObjekt eg, string ort, string id) => Uebergaenge(eg, ort).FirstOrDefault(u => u.Id == id);

    /// <summary>
    /// Ankunftsstelle einen Schritt vor dem Stueck <paramref name="d"/>: vorn ist die Seite in Drehrichtung (Drehung 0 =
    /// nach +y, 1 = nach -x, 2 = nach -y, 3 = nach +x wie die Fassaden im Spiel); der Blick zeigt vom Stueck weg.
    /// </summary>
    public static (double X, double Y, long Blick) Vor(PyObjekt d)
    {
        double x = Py.F(d["x"]), y = Py.F(d["y"]), w = Py.F(d["w"]), h = Py.F(d["h"]);
        var dreh = (int)(((Py.Int(Get(d, "dreh", 0L)) % 4) + 4) % 4);
        var (mx, my) = (x + (w / 2), y + (h / 2));
        var (ax, ay, blick) = dreh switch
        {
            1 => (x - Schritt, my, 270L),
            2 => (mx, y - Schritt, 180L),
            3 => (x + w + Schritt, my, 90L),
            _ => (mx, y + h + Schritt, 0L),
        };
        return (Math.Round(ax, 2), Math.Round(ay, 2), blick);
    }

    /// <summary>Richtung (x, y) eines Blicks in Grad (0 = +y, 90 = +x).</summary>
    public static (double X, double Y) Richtung(long blick) => (((blick % 360) + 360) % 360) switch
    {
        90 => (1, 0),
        180 => (0, -1),
        270 => (-1, 0),
        _ => (0, 1),
    };

    /// <summary>
    /// Das naechste Uebergangs-Stueck auf Etage <paramref name="etage"/> in Reichweite der Figur (wie ein Moebel: 0,9 vom
    /// Rand), mit seinem Uebergang; null = keins.
    /// </summary>
    public static (Uebergang Uebergang, PyObjekt Stueck)? Nahe(PyObjekt eg, string ort, int etage, double x, double y)
    {
        if (!Gibt(eg, etage))
        {
            return null;
        }
        var uebergaenge = Uebergaenge(eg, ort);
        return Einrichtung(Gebaeude(eg, etage))
            .Where(d => Get(d, FeldLink) is string && IstStueck(Py.Str(d["typ"])))
            .Select(d => (d, u: uebergaenge.FirstOrDefault(u => u.Id == Py.Str(d[FeldLink]) && u.Auf(etage) is not null && Arten.Contains(u.Art)),
                a: Spielwelt.Abstand(Py.F(d["x"]), Py.F(d["y"]), Py.F(d["w"]), Py.F(d["h"]), x, y)))
            .Where(p => p.u is not null && p.a <= Wohnung.MoebelReichweite)
            .OrderBy(p => p.a).Select(p => ((Uebergang, PyObjekt)?)(p.u!, p.d)).FirstOrDefault();
    }

    /// <summary>Text eines Knopfs: „Treppe hoch zum 1. Obergeschoss“, „Treppe runter zum Erdgeschoss“, „Fahrstuhl“.</summary>
    public static string KnopfText(PyObjekt eg, Uebergang u, int von)
    {
        if (u.Art == Fahrstuhl)
        {
            return FahrstuhlKnopf;
        }
        var ziel = u.Ziele(von).FirstOrDefault();
        return (ziel > von ? "Treppe hoch zum " : "Treppe runter zum ") + Name(eg, ziel);
    }

    public const string FahrstuhlKnopf = "Fahrstuhl";

    public const string FahrstuhlTitel = "Fahrstuhl";

    public const string FahrstuhlFrage = "Wohin möchtest du fahren?";

    public const string Abbrechen = "Abbrechen";

    /// <summary>Text, wenn die Figur oben „Zur Stadt“ will.</summary>
    public const string NurUnten = "Hinaus geht es nur im Erdgeschoss. Nimm die Treppe oder den Fahrstuhl nach unten.";

    /// <summary>Aktion eines Knopfs: Treppe ("etage:id:ziel") und Fahrstuhl ("fahrstuhl:id").</summary>
    public const string AktionEtage = "etage:";

    public const string AktionFahrstuhl = "fahrstuhl:";

    /// <summary>
    /// Wo stand die Figur zuletzt (Nico 2b)? Der letzte Eintrag des heutigen Tags unter den Stadt-Schritten und
    /// <see cref="Aufenthalt"/> zaehlt: ist er ein Aufenthalt mit Etage in dieser Stadt am Ort, an dem die Stadt-Uhr die Figur
    /// sieht, steht sie dort; sonst (alte Spielstaende, nach dem Verlassen, nach einem Weg, an einem neuen Tag) wie bisher
    /// vor dem Haus (null).
    /// </summary>
    public static (string Ort, int Etage, string? Uebergang)? Zuletzt(Stadt stadt, Spielstand stand, string stadtId)
    {
        SpielEreignis? letzter = null;
        foreach (var e in stand.Verlauf)
        {
            if (e.Art is not (Aufenthalt or Stadt.Gegangen or Stadt.Gelaufen or Stadt.Gewartet))
            {
                continue;
            }
            if (Py.IstNurInt(Get(e.Daten, "tag")) && Py.Int(e.Daten["tag"]) == stand.Tag)
            {
                letzter = e;
            }
        }
        if (letzter is not { Art: Aufenthalt } a || Get(a.Daten, "stadt") as string != stadtId || Get(a.Daten, "ort") is not string ort
            || !Py.IstNurInt(Get(a.Daten, "etage")) || Py.Int(a.Daten["etage"]) < 0 || Py.Int(a.Daten["etage"]) > Hoechstens)
        {
            return null;
        }
        return (ort, (int)Py.Int(a.Daten["etage"]), Get(a.Daten, "uebergang") as string);
    }

    /// <summary>Daten eines Aufenthalts (Etage null = draussen).</summary>
    public static PyObjekt AufenthaltDaten(long tag, string stadtId, string ort, int? etage, string? uebergang) => new()
    {
        { "tag", tag }, { "stadt", stadtId }, { "ort", ort }, { "etage", etage is { } e ? (long)e : null }, { "uebergang", uebergang },
    };
}

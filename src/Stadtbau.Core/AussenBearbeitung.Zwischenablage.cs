using System.Numerics;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.47.0 W1 (Weisung P12-226): Stuecke aus einem Projekt kopieren und in ein anderes (oder dasselbe) einfuegen.
/// Regeln (E1):
/// <list type="bullet">
/// <item>Kopieren nimmt mit, was das Stueck auch beim Schieben mitnimmt: Zebrastreifen, Ampeln, Haltestellen und Parkplaetze
/// einer Strasse, Zufahrten und Laternen eines Kreisverkehrs. Der Paketplatz (genau einer je Stadt) bleibt.</item>
/// <item>Haeuser (Orte und Gebaeude ohne Ort) kommen als Blueprint mit Innenraum und Etagen, auf Drehung 0 zurueckgedreht und
/// im Ziel wieder so gedreht wie in der Quelle (auch 15-Grad-Schritte). Ein Modell, das das Ziel mit denselben Teilen schon
/// nutzt, bekommt einen Kopienamen (<c>modell_2</c>, wie „Tür dazu“), damit jedes Haus seine Tuer hat; gleicher Name mit
/// anderen Teilen = Grund, nichts wird eingefuegt (nie still ueberschreiben).</item>
/// <item>Spielorte werden nie doppelt: hat das Ziel den Ort schon, kommt das Haus als Gebaeude ohne Ort (mit Hinweis). Fehlt
/// er einer Stadt mit Spielorten der Stadt 1, wird das Haus dieser Spielort (wie „Als Spielort festlegen“); sein Innenraum
/// ist dann der der Stadt 1 (Hinweis).</item>
/// <item>Einfuegen ist EIN Schritt; die Stuecke liegen zueinander wie in der Quelle, die Mitte am getippten Punkt (auf dem
/// Raster). Ragt ein Stueck ueber die Karte, wird nichts eingefuegt (Grund). Danach gilt dieselbe Pruefung wie nach dem
/// Setzen (rote Funde in der Liste rechts).</item>
/// </list>
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Ergebnis des Einfuegens: neue Auswahl, eingefuegt, uebersprungen, Grund (nichts eingefuegt) und gelbe Hinweise.</summary>
    public sealed record EinfuegenErgebnis(List<AussenStelle> Auswahl, int Erledigt, int Uebersprungen, string? Grund, List<string> Hinweise);

    /// <summary>Gegenprobe W1: Modellnamen wie gespeichert uebernehmen (keine Kopie bei schon genutztem Modell; nur Tests, je Ablauf).</summary>
    public static bool ZwischenablageAltFuerTests { get => ZwischenablageAlt.Value; set => ZwischenablageAlt.Value = value; }

    private static readonly AsyncLocal<bool> ZwischenablageAlt = new();

    /// <summary>Kann dieses Stueck in die Zwischenablage? (Paketplatz nicht.)</summary>
    public static bool Kopierbar(AussenStelle s) => s.Art is not (AussenArt.Paketplatz or AussenArt.Blueprint or AussenArt.Kurve or AussenArt.Kreisverkehr or AussenArt.Ziehen);

    /// <summary>
    /// Stuecke <paramref name="gruppe"/> des Projekts als Zwischenablage (aendert nichts am Projekt). Null = nichts Kopierbares.
    /// </summary>
    public static Zwischenablage? InZwischenablage(Projekt projekt, IReadOnlyList<AussenStelle> gruppe, out int uebersprungen)
    {
        var a = projekt.AussenStand;
        uebersprungen = 0;
        var alle = new List<AussenStelle>();
        foreach (var s in gruppe.Select(Ganz).Distinct().Where(s => Gueltig(a, s)))
        {
            if (!Kopierbar(s))
            {
                uebersprungen++;
                continue;
            }
            alle.Add(s);
            if (s.Art != AussenArt.Strasse)
            {
                continue;
            }
            alle.AddRange(ZebrasAuf(a, s).Select(z => new AussenStelle(AussenArt.Zebrastreifen, z)));
            alle.AddRange(AmpelnAuf(a, s).Select(z => new AussenStelle(AussenArt.Ampel, z.Nummer)));
            alle.AddRange(HalteAuf(a, s));
            if (IstKreisverkehr(a, s))
            {
                var (zufahrten, laternen) = KreisverkehrTeile(a, s);
                alle.AddRange(zufahrten.Select(z => new AussenStelle(AussenArt.Strasse, z)));
                alle.AddRange(laternen.Select(l => new AussenStelle(AussenArt.Laterne, l)));
            }
        }
        alle = [.. alle.Distinct()];
        var mitten = alle.Select(s => MitteVon(a, s)).OfType<Vector2>().ToList();
        if (alle.Count == 0 || mitten.Count == 0)
        {
            return null;
        }
        var mitte = new Vector2((mitten.Min(p => p.X) + mitten.Max(p => p.X)) / 2, (mitten.Min(p => p.Y) + mitten.Max(p => p.Y)) / 2);
        var stuecke = new List<object?>();
        var umrisse = new List<List<Vector2>>();
        foreach (var s in alle)
        {
            if (Stueck(projekt, a, s) is not { } stueck)
            {
                uebersprungen++;
                continue;
            }
            stuecke.Add(stueck);
            umrisse.AddRange(Bauen3D.Umriss(a, s).Select(u => u.Select(p => p - mitte).ToList()));
        }
        return stuecke.Count == 0 ? null : new Zwischenablage { Projekt = projekt.Name, Mitte = mitte, Stuecke = stuecke, Umrisse = umrisse };
    }

    /// <summary>Ein Stueck als Eintrag der Zwischenablage (null = laesst sich nicht kopieren).</summary>
    private static PyObjekt? Stueck(Projekt projekt, PyObjekt a, AussenStelle s)
    {
        object? Roh(object? wert) => Py.Kopie(wert);
        var land = Aussen.Landschaft(a);
        object? daten = s.Art switch
        {
            AussenArt.Strasse or AussenArt.Park or AussenArt.Fluss or AussenArt.Bahn => Roh(land[s.Nummer]),
            AussenArt.Baum => Roh(Liste(Objekt(land[s.Nummer])["punkte"])[s.Punkt]),
            AussenArt.Beet => Roh(Aussen.Beete(a)[s.Nummer]),
            AussenArt.Laterne => Roh(Aussen.Laternen(a)[s.Nummer]),
            AussenArt.Katalog => Roh(Aussen.KatalogStuecke(a)[s.Nummer]),
            AussenArt.Teich => Roh(Aussen.Teiche(a)[s.Nummer]),
            AussenArt.Steg => Roh(Aussen.Stege(a)[s.Nummer]),
            AussenArt.Fussweg => Roh(Aussen.Fusswege(a)[s.Nummer]),
            AussenArt.Notiz => Roh(NotizListe(a)[s.Nummer]),
            AussenArt.Huegel => Roh(HuegelListe(a)[s.Nummer]),
            AussenArt.Zebrastreifen => Roh(ZebraListe(a)[s.Nummer]),
            AussenArt.Ampel => Roh(AmpelListe(a)[s.Nummer]),
            AussenArt.Haltestelle or AussenArt.Parkplatz => Roh(HaltListe(a, s.Art)[s.Nummer]),
            _ => null,
        };
        if (s.Art is AussenArt.Haus or AussenArt.Gebaeude)
        {
            return HausStueck(projekt, a, s);
        }
        return daten is null ? null : new PyObjekt { { "art", s.Art.ToString() }, { "daten", daten } };
    }

    /// <summary>Ein Haus als Blueprint (auf Drehung 0 zurueckgedreht) mit Winkel und Mitte.</summary>
    private static PyObjekt? HausStueck(Projekt projekt, PyObjekt a, AussenStelle s)
    {
        if (MitteVon(a, s) is not { } mitte)
        {
            return null;
        }
        var winkel = (HausDreh(a, s) * 90) + HausGrad(a, s);
        var kopie = (PyObjekt)Py.Kopie(a)!;
        if (winkel % 360 != 0 && !Drehen(kopie, s, 360 - (winkel % 360)))
        {
            return null;
        }
        var ort = s.Art == AussenArt.Haus ? s.Ort : "";
        var name = BlueprintName(s.Art == AussenArt.Haus && Ort(a, ort) is { } o ? Spielort.Name(o)
            : Blueprint.NameVon(a, Py.Str(Objekt(Aussen.Landschaft(a)[s.Nummer])["modell"])));
        if (Blueprint.Aus(projekt, kopie, s, name) is not { } b)
        {
            return null;
        }
        return new PyObjekt
        {
            { "art", "haus" },
            { "blueprint", Zwischenablage.BlueprintText(b) },
            { "ort", ort },
            { "winkel", (long)(winkel % 360) },
            { "mitte", new List<object?> { Math.Round(mitte.X, 4), Math.Round(mitte.Y, 4) } },
        };
    }

    /// <summary>Gueltiger Name fuer den Blueprint eines Hauses (Zeichen wie ein Projektname, hoechstens 40).</summary>
    internal static string BlueprintName(string name)
    {
        var text = new StringBuilder();
        foreach (var c in name)
        {
            text.Append(char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : ' ');
        }
        var n = string.Join(' ', text.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        n = n.Length > 40 ? n[..40].TrimEnd() : n;
        return Projekt.NameGueltig(n) ? n : "Gebaeude";
    }

    // ---- Einfuegen ---------------------------------------------------------------------------------

    /// <summary>Versatz der Stuecke, wenn die Mitte der Zwischenablage bei (x, y) liegt (Raster 0,5 bzw. 0,1); auch fuer die Vorschau.</summary>
    public static (double Dx, double Dy) EinfuegenVersatz(Zwischenablage ablage, double x, double y, bool raster)
    {
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        return (Bearbeitung.AufRaster(x - ablage.Mitte.X, r), Bearbeitung.AufRaster(y - ablage.Mitte.Y, r));
    }

    /// <summary>Liegen alle Umrisse mit dem Versatz ganz auf der Karte? (gleiche Regel fuer Vorschau und Einfuegen)</summary>
    public static bool EinfuegenPasst(PyObjekt aussen, Zwischenablage ablage, double dx, double dy)
    {
        var (breite, hoehe) = Aussen.Groesse(aussen);
        return !ablage.Umrisse.SelectMany(u => u).Any(p => p.X + ablage.Mitte.X + dx < -0.001 || p.Y + ablage.Mitte.Y + dy < -0.001
            || p.X + ablage.Mitte.X + dx > breite + 0.001 || p.Y + ablage.Mitte.Y + dy > hoehe + 0.001);
    }

    /// <summary>
    /// Die Zwischenablage mit ihrer Mitte bei (<paramref name="x"/>, <paramref name="y"/>) einfuegen (in
    /// <c>Projekt.AussenAendern</c>, ein Schritt). Mit <paramref name="raster"/> rueckt der Versatz auf 0,5, sonst auf 0,1.
    /// Gibt es einen Grund, bleibt <paramref name="aussen"/> unveraendert. <paramref name="stadt1"/> = Stadt 1 (Spielorte).
    /// </summary>
    public static EinfuegenErgebnis Einfuegen(PyObjekt aussen, Zwischenablage ablage, double x, double y, bool raster, StadtFassung stadt1)
    {
        var (dx, dy) = EinfuegenVersatz(ablage, x, y, raster);
        var k = (PyObjekt)Py.Kopie(aussen)!;
        if (!EinfuegenPasst(k, ablage, dx, dy))
        {
            return new EinfuegenErgebnis([], 0, 0, Texte.EinfuegenAusserhalb, []);
        }
        var neu = new List<AussenStelle>();
        var hinweise = new List<string>();
        var uebersprungen = 0;
        // Linien zuerst: Haeuser legen ihren Weg danach an die schon eingefuegten Strassen
        var reihe = ablage.Stuecke.OfType<PyObjekt>().OrderBy(st => Py.Gleich(Get(st, "art"), "haus") ? 1 : 0).ToList();
        foreach (var st in reihe)
        {
            if (Py.Gleich(Get(st, "art"), "haus"))
            {
                var (stelle, grund, hinweis) = HausEinfuegen(k, st, dx, dy, stadt1);
                if (grund is not null)
                {
                    return new EinfuegenErgebnis([], 0, 0, grund, []);
                }
                if (hinweis is not null)
                {
                    hinweise.Add(hinweis);
                }
                if (stelle is null)
                {
                    uebersprungen++;
                }
                else
                {
                    neu.Add(stelle);
                }
                continue;
            }
            if (!Enum.TryParse<AussenArt>(Get(st, "art") as string, out var art) || Get(st, "daten") is not { } roh)
            {
                uebersprungen++;
                continue;
            }
            var daten = Py.Kopie(roh);
            Versetzen(daten, dx, dy);
            if (StueckAnhaengen(k, art, daten) is { } s)
            {
                neu.Add(s);
            }
            else
            {
                uebersprungen++;
            }
        }
        // alles gelungen: der neue Stand ersetzt den alten (ein Schritt im Verlauf)
        aussen.Clear();
        aussen.AddRange(k);
        return new EinfuegenErgebnis([.. neu.Where(s => Gueltig(aussen, s))], neu.Count, uebersprungen, null, hinweise);
    }

    /// <summary>Lage-Felder um (dx, dy) verschieben: x/y, Punkte [x, y], Punktlisten (punkte, weg), tuer und schild.</summary>
    private static void Versetzen(object? daten, double dx, double dy)
    {
        object Neu(object? alt, double d)
        {
            var w = Math.Round(Py.F(alt) + d, 4, MidpointRounding.AwayFromZero);
            return alt is long && w == Math.Floor(w) ? (long)w : w;
        }
        void Punkt(object? p)
        {
            if (p is List<object?> { Count: >= 2 } l && l[0] is long or double && l[1] is long or double)
            {
                l[0] = Neu(l[0], dx);
                l[1] = Neu(l[1], dy);
            }
        }
        switch (daten)
        {
            case List<object?> punkt:
                Punkt(punkt);
                break;
            case PyObjekt o:
                if (Get(o, "x") is long or double && Get(o, "y") is long or double)
                {
                    Py.Setzen(o, "x", Neu(o["x"], dx));
                    Py.Setzen(o, "y", Neu(o["y"], dy));
                }
                foreach (var feld in (string[])["punkte", "weg"])
                {
                    if (Get(o, feld) is List<object?> liste)
                    {
                        liste.ForEach(Punkt);
                    }
                }
                foreach (var feld in (string[])["tuer", "schild"])
                {
                    Punkt(Get(o, feld));
                }
                break;
        }
    }

    /// <summary>Ein (schon verschobenes) Stueck an seine Liste haengen; Rueckgabe: seine Stelle.</summary>
    private static AussenStelle? StueckAnhaengen(PyObjekt k, AussenArt art, object? daten)
    {
        var land = Aussen.Landschaft(k);
        switch (art)
        {
            case AussenArt.Strasse or AussenArt.Park or AussenArt.Fluss or AussenArt.Bahn when daten is PyObjekt o:
                // Weisung P12-220: ein Bahnsteig gehoert hoechstens zu einem Bahnhof (hier ohnehin keine Gebaeude)
                land.Add(o);
                return new AussenStelle(art, land.Count - 1, art is AussenArt.Fluss or AussenArt.Bahn ? -1 : 0);
            case AussenArt.Baum when daten is List<object?> p:
            {
                var i = land.FindIndex(t => t is PyObjekt b && Py.Gleich(b["typ"], "baum"));
                if (i < 0)
                {
                    land.Add(new PyObjekt { { "typ", "baum" }, { "punkte", new List<object?>() } });
                    i = land.Count - 1;
                }
                var punkte = Liste(Objekt(land[i])["punkte"]);
                punkte.Add(p);
                return new AussenStelle(AussenArt.Baum, i, punkte.Count - 1);
            }
            case AussenArt.Beet when daten is List<object?> p:
                Aussen.Beete(k).Add(p);
                return new AussenStelle(art, Aussen.Beete(k).Count - 1);
            case AussenArt.Laterne when daten is List<object?> p:
                Aussen.Laternen(k).Add(p);
                return new AussenStelle(art, Aussen.Laternen(k).Count - 1);
            case AussenArt.Katalog when daten is PyObjekt o:
                Aussen.KatalogStueckeAnlegen(k).Add(o);
                return new AussenStelle(art, Aussen.KatalogStuecke(k).Count - 1);
            case AussenArt.Teich when daten is PyObjekt o:
                Liste(Aussen.WasserAnlegen(k)["teiche"]).Add(o);
                return new AussenStelle(art, Aussen.Teiche(k).Count - 1, -1);
            case AussenArt.Steg when daten is PyObjekt o:
                Liste(Aussen.WasserAnlegen(k)["stege"]).Add(o);
                return new AussenStelle(art, Aussen.Stege(k).Count - 1);
            case AussenArt.Fussweg when daten is PyObjekt o:
                Aussen.FusswegeAnlegen(k).Add(o);
                return new AussenStelle(art, Aussen.Fusswege(k).Count - 1);
            case AussenArt.Notiz when daten is PyObjekt o:
                NotizListeAnlegen(k).Add(o);
                return new AussenStelle(art, NotizListe(k).Count - 1);
            case AussenArt.Huegel when daten is PyObjekt o:
                HuegelListeAnlegen(k).Add(o);
                return new AussenStelle(art, HuegelListe(k).Count - 1);
            case AussenArt.Zebrastreifen when daten is PyObjekt o:
                ZebraListeAnlegen(k).Add(o);
                return new AussenStelle(art, ZebraListe(k).Count - 1);
            case AussenArt.Ampel when daten is PyObjekt o:
                AmpelListeAnlegen(k).Add(o);
                return new AussenStelle(art, AmpelListe(k).Count - 1);
            case AussenArt.Haltestelle or AussenArt.Parkplatz when daten is PyObjekt o:
                HaltListeAnlegen(k, art).Add(o);
                return new AussenStelle(art, HaltListe(k, art).Count - 1);
            default:
                return null;
        }
    }

    /// <summary>Ein Haus aus der Zwischenablage einfuegen: (Stelle, Grund = nichts einfuegen, gelber Hinweis).</summary>
    private static (AussenStelle? Stelle, string? Grund, string? Hinweis) HausEinfuegen(PyObjekt k, PyObjekt st, double dx, double dy, StadtFassung stadt1)
    {
        Blueprint b;
        try
        {
            b = Blueprint.Lesen(Encoding.UTF8.GetBytes(Get(st, "blueprint") as string ?? ""));
        }
        catch (StadtbauFehler)
        {
            return (null, null, null);
        }
        var ort = Get(st, "ort") as string ?? "";
        var winkel = (int)(Get(st, "winkel") is long w ? w : 0);
        var mitte = Get(st, "mitte") is List<object?> { Count: 2 } m ? new Vector2((float)Py.F(m[0]), (float)Py.F(m[1])) : Vector2.Zero;
        // Spielort: fehlt der Ort einer Stadt mit Spielorten der Stadt 1, wird das Haus dieser Ort (Innenraum der Stadt 1)
        var wirdSpielort = ort != "" && Ort(k, ort) is null && Spielort.Stadt1Orte(stadt1).Any(x => Py.Gleich(x.Ort["id"], ort));
        if (ModellFuerZiel(k, b) is not { } modell)
        {
            return (null, Texte.BlueprintModellAnders(b.Modell), null);
        }
        b = b.Mit(modell, ohneInnenraum: wirdSpielort);
        var s = b.SetzenUndDrehen(k, mitte.X + dx, mitte.Y + dy, false, (winkel / 90) % 4, out var grund);
        if (s is null)
        {
            return (null, grund ?? Texte.BlueprintAusserhalb, null);
        }
        if (winkel % 90 != 0)
        {
            Drehen(k, s, winkel % 90);
        }
        if (MitteVon(k, s) is { } jetzt)
        {
            var (ddx, ddy) = (Math.Round(mitte.X + dx - jetzt.X, 4), Math.Round(mitte.Y + dy - jetzt.Y, 4));
            if (Math.Abs(ddx) > 0.001 || Math.Abs(ddy) > 0.001)
            {
                VerschiebenGenau(k, s, ddx, ddy);
            }
        }
        if (ort == "")
        {
            return (s, null, null);
        }
        var name = b.Name;
        if (!wirdSpielort)
        {
            return (s, null, Ort(k, ort) is not null ? Texte.EinfuegenOrtSchonDa(name) : Texte.EinfuegenOhneOrt(name));
        }
        if (Spielort.Festlegen(k, s, ort, stadt1) is { } nicht)
        {
            return (s, null, Texte.EinfuegenOhneOrt(name) + " " + nicht);
        }
        return (new AussenStelle(AussenArt.Haus, -1, 0, ort), null, Texte.EinfuegenSpielort(name));
    }

    /// <summary>
    /// Modellname fuer das Ziel: wie gespeichert (oder sein Grundmodell bei einer Kopie), wenn das Ziel es nicht hat oder mit
    /// denselben Teilen ungenutzt; mit denselben Teilen schon genutzt: naechste freie Kopie <c>_2</c>, <c>_3</c> …; andere
    /// Teile unter dem Namen: null (Grund).
    /// </summary>
    private static string? ModellFuerZiel(PyObjekt k, Blueprint b)
    {
        var alle = Modelle(k);
        var grund = GebaeudeTuer.KopieMuster().Match(b.Modell) is { Success: true } t && b.Bauteile is null ? t.Groups[1].Value : b.Modell;
        var teile = PythonJson.Standard(b.Teile);
        if (ZwischenablageAlt.Value)
        {
            return b.Modell;
        }
        string Frei()
        {
            var n = 2;
            while (Get(alle, grund + "_" + n.ToString(System.Globalization.CultureInfo.InvariantCulture)) is not null)
            {
                n++;
            }
            return grund + "_" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (Get(alle, grund) is not { } da)
        {
            // eine Kopie ohne ihr Grundmodell im Ziel heisst wieder wie das Grundmodell
            return grund;
        }
        var gleich = PythonJson.Standard(da) == teile;
        if (gleich && Get(Lernplattform.Core.Stadtbau.Bauteile.Gebaeude(Aussen.Stadtdaten(k)), grund) is { } gebaut)
        {
            // eigenes Gebaeude: dieselben Bauteile? sonst anderer Inhalt
            gleich = PythonJson.Standard(gebaut) == PythonJson.Standard(b.BauteileEintrag());
        }
        if (gleich)
        {
            return GebaeudeTuer.Genutzt(k, grund) ? Frei() : grund;
        }
        if (PythonJson.Standard(GebaeudeTuer.OhneBahnsteig(da)) == teile)
        {
            return Frei();
        }
        return null;
    }
}

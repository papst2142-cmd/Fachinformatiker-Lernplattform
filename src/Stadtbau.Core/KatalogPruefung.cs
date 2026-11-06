using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 3b (Weisung P12-19 Abschnitte 2 bis 4): Pruefung und Grenzen mit Stuecken aus dem vollen CC0-Katalog.
/// Die Regeln des gemeinsamen Teils (<see cref="StadtbauPruefung"/>) bleiben unveraendert und gelten weiter fuer alles;
/// das Werkzeug ergaenzt nur, was der Katalog neu bringt:
/// <list type="bullet">
/// <item>Ein Katalog-Modell ("cc0:paket/name") ist ein gueltiges Stueck. Im Grundriss steht es fest wie jedes Moebel
/// (Ueberschneidung und Wege wie bisher, mit seinem Rechteck); ein unbekannter Katalog-Name bleibt rot.</item>
/// <item>Ein Effekt (Partikel-Bild, "Nur Aussehen") versperrt nichts: er geht nicht in Ueberschneidung und Wege ein,
/// muss aber im Haus bzw. auf der Karte liegen.</item>
/// <item>Grenzen zaehlen jedes Katalog-Stueck mit seinen echten Dreiecken (Effekt: leuchtendes Zeichen, 2 Dreiecke)
/// im Netz des Raums bzw. der Stadt, ohne eigenen Zeichenaufruf; nichts gelockert (160 000 / 40).</item>
/// </list>
/// Texte nennen Katalog-Stuecke mit ihrem Anzeigenamen statt der Kennung.
/// </summary>
public static class KatalogPruefung
{
    /// <summary>Dreiecke eines Effekts (leuchtendes Zeichen, Weisung P12-23 3a).</summary>
    public const int EffektDreiecke = 2;

    /// <summary>Kopie des Grundrisses ohne die Stuecke, fuer die <paramref name="weg"/> gilt.</summary>
    public static PyObjekt Ohne(PyObjekt gebaeude, Func<string, bool> weg)
    {
        var g = (PyObjekt)Py.Kopie(gebaeude)!;
        foreach (var bereich in new[] { Objekt(g["flur"]) }.Concat(Liste(g["raeume"]).OfType<PyObjekt>()))
        {
            if (bereich["deko"] is List<object?> liste)
            {
                liste.RemoveAll(d => d is PyObjekt o && weg(Py.Str(o["typ"])));
            }
        }
        return g;
    }

    private static bool IstEffekt(Katalog? k, string typ) => k?.Eintrag(typ) is { Art: KatalogArt.Effekt };

    /// <summary>Dreiecke der Katalog-Stuecke eines Grundrisses (unbekannte zaehlen nicht, sie sind ohnehin rot).</summary>
    public static long Dreiecke(Katalog? k, PyObjekt gebaeude) =>
        InnenGrundriss.Einrichtung(gebaeude).Select(e => k?.Eintrag(Py.Str(e.Deko["typ"]))).Where(e => e is not null)
            .Sum(e => (long)(e!.Art == KatalogArt.Effekt ? EffektDreiecke : e.Dreiecke));

    /// <summary>Anzeigename eines Typs: Katalog-Stueck mit Anzeige, sonst wie der einfache Katalog.</summary>
    public static string Name(Katalog? k, string typ) => k?.Eintrag(typ) is { } e ? e.Anzeige : InnenKatalog.Name(typ);

    /// <summary>
    /// Alle Regeln fuer einen Innenraum wie <see cref="StadtbauPruefung.Innen"/>, dazu die Katalog-Stuecke, und die Grenzen
    /// (Dreiecke, Aufrufe) mit den echten Dreiecken der Katalog-Stuecke.
    /// </summary>
    public static (List<StadtbauFund> Funde, (long Dreiecke, int Aufrufe) Grenzen) Innen(Katalog? k, string ort, PyObjekt gebaeude,
        IReadOnlyList<PyObjekt> personen, GrafikBibliothek? grafik, Geschaefte? geschaefte, PyObjekt? innenraeume)
    {
        var ohneEffekte = Ohne(gebaeude, t => IstEffekt(k, t));
        // Der gemeinsame Teil kennt keine Katalog-Stuecke: ein unbekannter Typ ist dort ein roter Grundriss-Fund, und danach
        // rechnet er weder Ueberschneidung noch Wege. Deshalb zwei Laeufe: (a) wie gebaut (alles ausser bekannten Katalog-
        // Stuecken genau wie bisher), (b) jedes bekannte Katalog-Stueck als Stellvertreter "kartons" mit seinem Rechteck
        // (ein festes Teil ohne Aufliegen); was nur (b) findet, kommt von den Katalog-Stuecken und nennt sie beim Namen.
        var gemeinsam = StadtbauPruefung.Innen(ort, ohneEffekte, personen, grafik, geschaefte, innenraeume);
        var funde = new List<StadtbauFund>();
        foreach (var f in gemeinsam)
        {
            // Grenzen rechnet diese Pruefung selbst (mit den echten Dreiecken); bekannte Katalog-Stuecke sind gueltig
            if (f.Text.StartsWith("Die Grenzen sind", StringComparison.Ordinal) || IstBekanntesKatalogStueck(k, f.Text))
            {
                continue;
            }
            funde.Add(Umbenannt(k, f));
        }
        var nurKatalogRot = gemeinsam.Where(f => f.Rot).All(f => IstBekanntesKatalogStueck(k, f.Text));
        var stellvertreter = Stellvertreter(k, ohneEffekte);
        if (nurKatalogRot && stellvertreter.Count > 0)
        {
            var (mit, zuordnung) = (stellvertreter[0].Gebaeude, stellvertreter.Skip(1).Select(s => (s.Typ, s.Rechteck)).ToList());
            var vorhanden = new HashSet<string>(gemeinsam.Select(Schluessel), StringComparer.Ordinal);
            foreach (var f in StadtbauPruefung.Innen(ort, mit, personen, grafik, geschaefte, innenraeume))
            {
                if (f.Text.StartsWith("Die Grenzen sind", StringComparison.Ordinal) || vorhanden.Contains(Schluessel(f)))
                {
                    continue;
                }
                funde.Add(StellvertreterBenannt(k, f, zuordnung));
            }
        }
        foreach (var (bereich, d) in InnenGrundriss.Einrichtung(gebaeude))
        {
            var typ = Py.Str(d["typ"]);
            if (!IstEffekt(k, typ))
            {
                continue;
            }
            double x = Py.F(d["x"]), y = Py.F(d["y"]), w = Py.F(d["w"]), h = Py.F(d["h"]);
            if (w <= 0 || h <= 0 || x < -1e-9 || y < -1e-9 || x + w > Py.F(gebaeude["breite"]) + 1e-9 || y + h > Py.F(gebaeude["hoehe"]) + 1e-9)
            {
                funde.Add(new StadtbauFund($"Der Effekt „{Name(k, typ)}“ liegt nicht ganz im Haus.", "Effekt ins Haus schieben.", true, x + (w / 2), y + (h / 2)));
            }
        }
        (long, int) grenzen = (0, 0);
        if (!gemeinsam.Any(f => f.Rot && (f.Text.StartsWith("Das Haus", StringComparison.Ordinal) || f.Text.StartsWith("Der Innenraum lässt sich", StringComparison.Ordinal))))
        {
            try
            {
                var ohneKatalog = Ohne(gebaeude, Katalog.IstKatalogTyp);
                var raum = Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), ohneKatalog, personen, grafik,
                    Geschaefte.Laeden.Contains(ort) ? geschaefte?.Daten : null);
                var (dreiecke, aufrufe) = StadtbauGrenzen.Innen(raum);
                grenzen = (dreiecke + Dreiecke(k, gebaeude), aufrufe);
                StadtbauPruefung.Grenzen(grenzen.Item1, grenzen.Item2, funde);
            }
            catch (Exception fehler) when (fehler is InvalidDataException or KeyNotFoundException or InvalidCastException or FormatException or NullReferenceException or ArgumentException)
            {
                // der gemeinsame Teil hat den Fund schon ("Der Innenraum laesst sich nicht bauen")
            }
        }
        return (funde, grenzen);
    }

    /// <summary>Typ des Stellvertreters fuer ein bekanntes Katalog-Stueck im zweiten Lauf (festes Teil, liegt auf nichts auf).</summary>
    public const string StellvertreterTyp = "kartons";

    private static string Schluessel(StadtbauFund f) => f.Text + "|" + f.Rot + "|" + f.X?.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
        + "|" + f.Y?.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Kopie mit Stellvertretern (erstes Element: Gebaeude, Rechteck leer) und je ersetztem Stueck sein Typ und Rechteck.
    /// Leer, wenn es kein bekanntes Katalog-Stueck gibt.
    /// </summary>
    private static List<(PyObjekt Gebaeude, string Typ, (double X, double Y, double W, double H) Rechteck)> Stellvertreter(Katalog? k, PyObjekt gebaeude)
    {
        var g = (PyObjekt)Py.Kopie(gebaeude)!;
        var liste = new List<(PyObjekt, string, (double, double, double, double))> { (g, "", default) };
        foreach (var (_, d) in InnenGrundriss.Einrichtung(g))
        {
            var typ = Py.Str(d["typ"]);
            if (Katalog.IstKatalogTyp(typ) && k?.Eintrag(typ) is { Art: KatalogArt.Modell })
            {
                liste.Add((g, typ, (Py.F(d["x"]), Py.F(d["y"]), Py.F(d["w"]), Py.F(d["h"]))));
                Py.Setzen(d, "typ", StellvertreterTyp);
            }
        }
        return liste.Count > 1 ? liste : [];
    }

    /// <summary>
    /// „Kartons“ im Text eines Funds aus dem zweiten Lauf durch den Namen des Katalog-Stuecks ersetzen: das erste Vorkommen
    /// ist das Stueck an der Stelle des Funds (oder das naechste), ein zweites das Stueck, das sich mit dem ersten ueberschneidet.
    /// </summary>
    private static StadtbauFund StellvertreterBenannt(Katalog? k, StadtbauFund f, List<(string Typ, (double X, double Y, double W, double H) R)> stuecke)
    {
        var platz = "„" + InnenKatalog.Name(StellvertreterTyp) + "“";
        var text = f.Text;
        (string Typ, (double X, double Y, double W, double H) R)? erstes = null;
        var start = text.IndexOf(platz, StringComparison.Ordinal);
        if (start < 0 || stuecke.Count == 0)
        {
            return f;
        }
        double fx = f.X ?? 0, fy = f.Y ?? 0;
        static double Abstand((double X, double Y, double W, double H) r, double x, double y)
        {
            var dx = Math.Max(Math.Max(r.X - x, 0), x - (r.X + r.W));
            var dy = Math.Max(Math.Max(r.Y - y, 0), y - (r.Y + r.H));
            return (dx * dx) + (dy * dy);
        }
        erstes = stuecke.OrderBy(s => Abstand(s.R, fx, fy)).First();
        text = text[..start] + "„" + Name(k, erstes.Value.Typ) + "“" + text[(start + platz.Length)..];
        var zweite = text.IndexOf(platz, start + 1, StringComparison.Ordinal);
        if (zweite >= 0)
        {
            var e = erstes.Value.R;
            var anderes = stuecke.Where(s => s != erstes.Value).OrderBy(s => Abstand(s.R, e.X + (e.W / 2), e.Y + (e.H / 2))).FirstOrDefault();
            if (anderes.Typ is not null)
            {
                text = text[..zweite] + "„" + Name(k, anderes.Typ) + "“" + text[(zweite + platz.Length)..];
            }
        }
        return f with { Text = text };
    }

    private static bool IstBekanntesKatalogStueck(Katalog? k, string text)
    {
        const string ende = "“ ist kein Teil aus dem Katalog.";
        if (k is null || !text.StartsWith('„') || !text.EndsWith(ende, StringComparison.Ordinal))
        {
            return false;
        }
        var typ = text[1..^ende.Length];
        return Katalog.IstKatalogTyp(typ) && k.Eintrag(typ) is not null;
    }

    /// <summary>„cc0:paket/name“ in einem Text durch „Anzeige“ ersetzen.</summary>
    public static StadtbauFund Umbenannt(Katalog? k, StadtbauFund f)
    {
        if (k is null || !f.Text.Contains("„" + Katalog.Praefix, StringComparison.Ordinal))
        {
            return f;
        }
        var text = f.Text;
        var start = 0;
        while ((start = text.IndexOf("„" + Katalog.Praefix, start, StringComparison.Ordinal)) >= 0)
        {
            var ende = text.IndexOf('“', start);
            if (ende < 0)
            {
                break;
            }
            var typ = text[(start + 1)..ende];
            var neu = k.Eintrag(typ)?.Anzeige ?? typ;
            text = text[..(start + 1)] + neu + text[ende..];
            start += neu.Length + 2;
        }
        return f with { Text = text };
    }

    // ---- draussen --------------------------------------------------------------------------------

    /// <summary>
    /// Regeln fuer die Katalog-Stuecke draussen (<c>stadtdaten.katalog</c>): Name im Katalog, Groesse, ganz auf der Karte.
    /// Alles andere pruefen die Regeln des gemeinsamen Teils wie bisher.
    /// </summary>
    public static List<StadtbauFund> Aussen(Katalog? k, PyObjekt aussen)
    {
        var funde = new List<StadtbauFund>();
        var (breite, hoehe) = Core.Aussen.Groesse(aussen);
        foreach (var o in Core.Aussen.KatalogStuecke(aussen).OfType<PyObjekt>())
        {
            var id = Py.Str(o["id"]);
            double x = Py.F(o["x"]), y = Py.F(o["y"]), w = Py.F(o["w"]), t = Py.F(o["t"]);
            var name = k?.Eintrag(id)?.Anzeige ?? id;
            if (k?.Eintrag(id) is null)
            {
                funde.Add(new StadtbauFund($"„{id}“ gibt es im Katalog nicht.", "Stück löschen und aus dem Katalog neu setzen.", true, x, y));
            }
            if (w <= 0 || t <= 0)
            {
                funde.Add(new StadtbauFund($"„{name}“ hat keine Größe.", "Breite und Tiefe größer als 0 eintragen.", true, x, y));
                continue;
            }
            if (x - (w / 2) < -1e-9 || y - (t / 2) < -1e-9 || x + (w / 2) > breite + 1e-9 || y + (t / 2) > hoehe + 1e-9)
            {
                funde.Add(new StadtbauFund($"„{name}“ ragt über den Rand der Karte.", "Stück ganz auf die Karte schieben.", true, x, y));
            }
        }
        return funde;
    }

    /// <summary>Dreiecke der Katalog-Stuecke draussen (kommen zur gemessenen Gesamtansicht dazu, ohne eigenen Aufruf).</summary>
    public static long DreieckeAussen(Katalog? k, PyObjekt aussen) =>
        Core.Aussen.KatalogStuecke(aussen).OfType<PyObjekt>().Select(o => k?.Eintrag(Py.Str(o["id"]))).Where(e => e is not null)
            .Sum(e => (long)(e!.Art == KatalogArt.Effekt ? EffektDreiecke : e.Dreiecke));
}

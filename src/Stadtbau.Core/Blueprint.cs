using System.Globalization;
using System.Numerics;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 4c (Weisung P12-32): ein Blueprint ist ein Gebaeude, das Nico gestaltet hat, als Vorlage unter eigenem
/// Namen: Modell (alle Teile), Tuer und Fussweg, Schild und der Innenraum mit Einrichtung und Stuecken aus dem Katalog.
/// Alle Lagen stehen relativ zur Ecke des Hauses (x, y des Orts bzw. Gebaeudes), damit er in jeder Stadt und an jeder
/// Stelle wieder gesetzt werden kann. Datei <c>blueprints/&lt;Name&gt;.fisblueprint</c> im Datenordner, JSON, je Feld eine
/// Zeile, mit Formatnummer und Pruefsumme (Muster wie <c>.fistadt</c> und <c>.fisbau</c>). Eine kaputte Datei oder eine aus
/// einer neueren Werkzeug-Version wird nie still uebergangen oder ueberschrieben: sie steht mit einem Hinweis in der Liste.
/// </summary>
public sealed class Blueprint
{
    public const string Format = "fisblueprint";

    /// <summary>S 0.9.0 (Weisung P12-48): Format 2 mit den Feldern <c>bauteile</c> und <c>eingang</c> (Gebaeude aus dem
    /// Editor); Format 1 (bis S 0.8.0) bleibt lesbar.</summary>
    public const long FormatVersion = 2;

    public const string Endung = ".fisblueprint";

    public static readonly string[] Felder = ["format", "formatversion", "werkzeug", "name", "herkunft", "modell", "teile", "tuer", "weg", "schild", "innenraum", "personen", "bauteile", "eingang", "pruefsumme"];

    /// <summary>Felder von Format 1 (bis S 0.8.0).</summary>
    public static readonly string[] Felder1 = ["format", "formatversion", "werkzeug", "name", "herkunft", "modell", "teile", "tuer", "weg", "schild", "innenraum", "personen", "pruefsumme"];

    /// <summary>Name fuer die Anzeige (Buchstaben mit Umlauten, Ziffern, Leerzeichen, - und _, wie ein Projektname).</summary>
    public required string Name { get; init; }

    /// <summary>Woher er stammt: Stadt, Ort (oder leer bei einem Gebaeude ohne Ort), Werkzeug.</summary>
    public required PyObjekt Herkunft { get; init; }

    /// <summary>Name des Modells (bleibt beim Setzen: das Spiel findet die Grafik ueber diesen Namen).</summary>
    public required string Modell { get; init; }

    /// <summary>Die Teile des Modells (Bloecke, Schild, ...), wie in <c>karte.modelle</c>.</summary>
    public required List<object?> Teile { get; init; }

    /// <summary>Tuer relativ zur Ecke des Hauses (null = keine Tuer).</summary>
    public List<object?>? Tuer { get; init; }

    /// <summary>Fussweg relativ zur Ecke des Hauses (Punkte), leer ohne Tuer.</summary>
    public List<object?> Weg { get; init; } = [];

    /// <summary>Schild relativ zur Ecke des Hauses (null = keines).</summary>
    public List<object?>? Schild { get; init; }

    /// <summary>Innenraum (fertiger Grundriss wie <see cref="InnenGrundriss.Grundriss"/>), null = keiner.</summary>
    public PyObjekt? Innenraum { get; init; }

    /// <summary>Personen des Innenraums mit Platz (Verkaeufer, Schalter), leer ohne Innenraum.</summary>
    public List<object?> Personen { get; init; } = [];

    /// <summary>
    /// S 0.9.0: die Teile eines selbst gebauten Gebaeudes (<c>[{teil, x, y, stockwerk, dreh}]</c>, <see cref="Bauteile"/>);
    /// null bei einem Gebaeude der Stadt (Format 1 oder aus einem Haus gespeichert).
    /// </summary>
    public List<object?>? Bauteile { get; init; }

    /// <summary>S 0.9.0: Eingang des selbst gebauten Gebaeudes (<c>[{x, y, seite}]</c>), null ohne <see cref="Bauteile"/>.</summary>
    public List<object?>? Eingang { get; init; }

    /// <summary>Datei, aus der er gelesen wurde (null = noch nicht gespeichert).</summary>
    public string? Datei { get; private set; }

    public static bool NameGueltig(string name) => Projekt.NameGueltig(name);

    /// <summary>Dateiname: Name in ASCII (<see cref="Projekt.DateiName"/>) mit Endung.</summary>
    public static string DateiName(string name) => Projekt.DateiName(name) + Endung;

    // ---- Aus einem Gebaeude -------------------------------------------------------------------------

    /// <summary>
    /// Blueprint aus einem Haus (Ort) oder Gebaeude ohne Ort der Stadt draussen. Der Innenraum kommt mit, wenn das Projekt
    /// einen bearbeitbaren Innenraum fuer den Ort hat (heute die Laeden und Schalter). Andere Stuecke: null.
    /// </summary>
    public static Blueprint? Aus(Projekt projekt, AussenStelle s, string name)
    {
        if (!NameGueltig(name))
        {
            throw new ArgumentException("Name ungültig", nameof(name));
        }
        var a = projekt.AussenStand;
        PyObjekt haus;
        List<object?>? tuer = null, schild = null;
        List<object?> weg = [];
        PyObjekt? innen = null;
        List<object?> personen = [];
        string ort = "";
        if (s.Art == AussenArt.Haus && AussenBearbeitung.Ort(a, s.Ort) is { } o)
        {
            haus = o;
            ort = s.Ort;
            if (AussenBearbeitung.Zugang(a, s.Ort) is { } z)
            {
                (tuer, weg) = (Liste(z["tuer"]), Liste(z["weg"]));
            }
            schild = o["schild"] as List<object?>;
            if (projekt.Orte.Contains(s.Ort))
            {
                innen = (PyObjekt)Py.Kopie(projekt.Grundriss(s.Ort))!;
                personen = [.. projekt.Personen(s.Ort).Select(p => (object?)Py.Kopie(p))];
            }
        }
        else if (s.Art == AussenArt.Gebaeude && Aussen.Landschaft(a).ElementAtOrDefault(s.Nummer) is PyObjekt g && Py.Gleich(g["typ"], "gebaeude"))
        {
            haus = g;
            if (Zierweg(a, Py.Str(g["modell"])) is { } z)
            {
                (tuer, weg) = (Liste(z["tuer"]), Liste(z["weg"]));
                // ein gesetzter Blueprint bringt seinen Innenraum aus dem Projekt mit (Teil "bauten")
                if (Get(Aussen.Bauten(a), Py.Str(g["modell"])) is PyObjekt bau)
                {
                    innen = (PyObjekt)Py.Kopie(bau["grundriss"])!;
                    personen = (List<object?>)Py.Kopie(Get(bau, "personen") ?? new List<object?>())!;
                }
            }
        }
        else
        {
            return null;
        }
        var modell = Py.Str(haus["modell"]);
        if (Get(AussenBearbeitung.Modelle(a), modell) is not List<object?> teile)
        {
            return null;
        }
        // S 0.9.0: ein selbst gebautes Gebaeude bringt seine Teile mit (stadtdaten.bauteile)
        var gebaut = s.Art == AussenArt.Gebaeude ? Get(Lernplattform.Core.Stadtbau.Bauteile.Gebaeude(Aussen.Stadtdaten(a)), modell) as PyObjekt : null;
        double hx = Py.F(haus["x"]), hy = Py.F(haus["y"]);
        return new Blueprint
        {
            Name = name,
            Herkunft = new PyObjekt { { "stadt", projekt.Basis.StadtId }, { "ort", ort }, { "projekt", projekt.Name } },
            Modell = modell,
            Teile = (List<object?>)Py.Kopie(teile)!,
            Tuer = tuer is null ? null : Relativ(tuer, hx, hy),
            Weg = [.. weg.Select(p => (object?)Relativ(Liste(p), hx, hy))],
            Schild = schild is null ? null : Relativ(schild, hx, hy),
            Innenraum = innen,
            Personen = personen,
            Bauteile = gebaut is null ? null : (List<object?>)Py.Kopie(gebaut["teile"])!,
            Eingang = gebaut is null ? null : (List<object?>)Py.Kopie(gebaut["eingang"])!,
        };
    }

    /// <summary>Der erste Zierweg (Tuer und Fussweg eines Gebaeudes ohne Ort) zu einem Modell.</summary>
    public static PyObjekt? Zierweg(PyObjekt aussen, string modell) =>
        Zierwege(aussen).FirstOrDefault(z => Py.Gleich(z["modell"], modell));

    private static IEnumerable<PyObjekt> Zierwege(PyObjekt aussen) =>
        (Get(Objekt(Get(Aussen.Stadtdaten(aussen), "darstellung") ?? new PyObjekt()), "zierwege") as List<object?> ?? []).OfType<PyObjekt>();

    private static List<object?> Relativ(List<object?> p, double x, double y) => [Bearbeitung.Zahl(Py.F(p[0]) - x), Bearbeitung.Zahl(Py.F(p[1]) - y)];

    private static List<object?> Absolut(List<object?> p, double x, double y) => [Bearbeitung.Zahl(Py.F(p[0]) + x), Bearbeitung.Zahl(Py.F(p[1]) + y)];

    // ---- Setzen -------------------------------------------------------------------------------------

    /// <summary>
    /// Den Blueprint mit der Mitte bei (x, y) als eigenstaendiges Gebaeude ohne Ort setzen (Raster 0,5 wie jedes Haus):
    /// Gebaeude in der Landschaft, Modell (fehlt es in <c>karte.modelle</c>, kommt es dorthin, S 0.22.1), Tuer und
    /// Fussweg als Zierweg (das Ende des Wegs wird an die naechste Strasse gelegt wie beim Verschieben). Danach ist es ein
    /// ganz normales Gebaeude, unabhaengig von der Vorlage. Gibt es das Modell in der Stadt schon mit anderen Teilen, wird
    /// nichts gesetzt (Grund in <paramref name="grund"/>).
    /// </summary>
    public AussenStelle? Setzen(PyObjekt aussen, double x, double y, out string? grund) => SetzenUndDrehen(aussen, x, y, false, 0, out grund);

    /// <summary>Welle E: setzen, in Vierteln drehen (mit Tuer und Fussweg), dann mit <paramref name="einrasten"/> an den Gehweg legen.</summary>
    public AussenStelle? SetzenUndDrehen(PyObjekt aussen, double x, double y, bool einrasten, int dreh, out string? grund)
    {
        var s = SetzenRoh(aussen, x, y, out grund);
        for (var i = 0; s is not null && i < dreh % 4; i++)
        {
            AussenBearbeitung.Drehen(aussen, s);
        }
        if (s is not null && einrasten)
        {
            AussenBearbeitung.NachSetzenEinrasten(aussen, s);
        }
        return s;
    }

    /// <summary>Gegenprobe S 0.22.1: fehlendes Modell nach stadtdaten.neue_modelle wie bis S 0.22.0 (nur Tests, je Ablauf).</summary>
    internal static readonly AsyncLocal<bool> NeueModelleAltFuerTests = new();

    private AussenStelle? SetzenRoh(PyObjekt aussen, double x, double y, out string? grund)
    {
        grund = null;
        var (breite, hoehe) = Aussen.Groesse(aussen);
        if (x < 0 || y < 0 || x > breite || y > hoehe)
        {
            grund = Texte.BlueprintAusserhalb;
            return null;
        }
        var vorhanden = Get(AussenBearbeitung.Modelle(aussen), Modell);
        var sd = Aussen.Stadtdaten(aussen);
        var gebaut = Get(Lernplattform.Core.Stadtbau.Bauteile.Gebaeude(sd), Modell);
        if ((vorhanden is not null && PythonJson.Standard(vorhanden) != PythonJson.Standard(Teile))
            || (gebaut is not null && PythonJson.Standard(gebaut) != PythonJson.Standard(BauteileEintrag())))
        {
            grund = Texte.BlueprintModellAnders(Modell);
            return null;
        }
        if (Bauteile is not null && gebaut is null)
        {
            // S 0.9.0: die Teile des selbst gebauten Gebaeudes kommen in die Stadt (stadtdaten.bauteile)
            if (Get(sd, "bauteile") is not PyObjekt alle)
            {
                alle = new PyObjekt();
                Py.Setzen(sd, "bauteile", alle);
            }
            Py.Setzen(alle, Modell, BauteileEintrag());
        }
        // S 0.22.1 (Weisung P12-94, selbst gefunden): ein Gebaeude der Landschaft sucht die Ladepruefung des Spiels
        // (SpielPruefung.Welt) nur in karte.modelle. Bis S 0.22.0 kam ein fehlendes Modell nach stadtdaten.neue_modelle;
        // in Stadt 1 scheiterte der Export dann an „unbekanntes Modell“ (selbst gebaute Gebaeude, oder ein Laden-Modell wie
        // das Moebelhaus, das nur in neue_modelle steht). Jetzt steht das Modell immer auch in karte.modelle.
        var karte = Aussen.Karte(aussen);
        if (Get(karte, "modelle") is not PyObjekt kartenModelle)
        {
            kartenModelle = new PyObjekt();
            Py.Setzen(karte, "modelle", kartenModelle);
        }
        if (Get(kartenModelle, Modell) is null && !NeueModelleAltFuerTests.Value)
        {
            Py.Setzen(kartenModelle, Modell, Py.Kopie(vorhanden ?? Teile));
        }
        else if (vorhanden is null && NeueModelleAltFuerTests.Value)
        {
            if (Get(sd, "neue_modelle") is not PyObjekt neu)
            {
                neu = new PyObjekt();
                Py.Setzen(sd, "neue_modelle", neu);
            }
            Py.Setzen(neu, Modell, Py.Kopie(Teile));
        }
        var (gx, gy, gw, gh) = AussenBearbeitung.Grundflaeche(aussen, Modell);
        var hx = Bearbeitung.AufRaster(x - (gw / 2) - gx, Bearbeitung.Raster);
        var hy = Bearbeitung.AufRaster(y - (gh / 2) - gy, Bearbeitung.Raster);
        var land = Aussen.Landschaft(aussen);
        land.Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", Modell }, { "x", Bearbeitung.Zahl(hx) }, { "y", Bearbeitung.Zahl(hy) } });
        if (Tuer is not null)
        {
            if (Get(sd, "darstellung") is not PyObjekt d)
            {
                d = new PyObjekt();
                Py.Setzen(sd, "darstellung", d);
            }
            if (Get(d, "zierwege") is not List<object?> zw)
            {
                zw = [];
                Py.Setzen(d, "zierwege", zw);
            }
            var weg = Weg.Select(p => (object?)Absolut(Liste(p), hx, hy)).ToList();
            zw.Add(new PyObjekt { { "name", Name }, { "modell", Modell }, { "tuer", Absolut(Tuer, hx, hy) }, { "weg", weg } });
            AussenBearbeitung.WegAnschliessen(weg, AussenBearbeitung.Strassen(aussen));
            // Meldung P12-23 Frage 1 a: der Innenraum kommt ins Projekt (Teil "bauten", nur Werkzeug). Steht das Modell schon
            // mit Innenraum in der Stadt, bleibt dieser (das zweite Gebaeude ist ohnehin rot, Pruefen).
            var bauten = Aussen.Bauten(aussen);
            if (Innenraum is not null && Get(bauten, Modell) is null)
            {
                Py.Setzen(bauten, Modell, new PyObjekt { { "name", Name }, { "grundriss", Py.Kopie(Innenraum) }, { "personen", Py.Kopie(Personen) } });
            }
        }
        return new AussenStelle(AussenArt.Gebaeude, land.Count - 1);
    }

    /// <summary>
    /// Pruefung im Werkzeug (Nachtrag zu Meldung P12-23): das Spiel setzt die Tuer eines Zierwegs an das erste Gebaeude
    /// seines Modells. Steht dasselbe Modell mit Tuer zweimal als Gebaeude ohne Ort, ist das zweite rot.
    /// </summary>
    /// <summary>
    /// Zierweg (Tuer und Fussweg) eines Gebaeudes ohne Ort: der des Modells, aber nur am ersten Gebaeude dieses Modells (dort
    /// zeichnet ihn das Spiel), sonst null.
    /// </summary>
    public static PyObjekt? ZierwegVon(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art != AussenArt.Gebaeude || Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is not PyObjekt g || g["modell"] is not string modell)
        {
            return null;
        }
        var erstes = Aussen.Landschaft(aussen).OfType<PyObjekt>().First(t => Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], modell));
        return ReferenceEquals(erstes, g) ? Zierweg(aussen, modell) : null;
    }

    /// <summary>Mitte eines Gebaeudes der Landschaft (Stelle eines Funds: die Ecke oben links traegt das Namensschild).</summary>
    private static (double X, double Y) GebaeudeMitte(PyObjekt aussen, PyObjekt g)
    {
        var nummer = Aussen.Landschaft(aussen).IndexOf(g);
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(aussen, new AussenStelle(AussenArt.Gebaeude, nummer));
        return (x + (w / 2), y + (h / 2));
    }

    /// <summary>Eintrag in <c>stadtdaten.bauteile</c> ({teile, eingang}), null bei einem Gebaeude der Stadt.</summary>
    private PyObjekt? BauteileEintrag() => Bauteile is null ? null : new PyObjekt { { "teile", Py.Kopie(Bauteile) }, { "eingang", Py.Kopie(Eingang ?? []) } };

    /// <summary>
    /// S 0.9.0 (Weisung P12-48): stehende Gebaeude, deren Modell selbst gebaut ist (<c>stadtdaten.bauteile</c>). Das Spiel
    /// zeichnet sie erst mit dem Einbau (W4b): gelber Fund. Seit S 0.22.1 (Weisung P12-94) sperrt er den Export nicht mehr.
    /// </summary>
    public static List<(string Modell, string Name, PyObjekt Gebaeude)> EigeneGebaeude(PyObjekt aussen)
    {
        var gebaut = Lernplattform.Core.Stadtbau.Bauteile.Gebaeude(Aussen.Stadtdaten(aussen));
        var liste = new List<(string, string, PyObjekt)>();
        foreach (var g in Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "gebaeude") && Py.Hat(gebaut, Py.Str(t["modell"]))))
        {
            var modell = Py.Str(g["modell"]);
            if (liste.Any(e => e.Item1 == modell))
            {
                continue;
            }
            liste.Add((modell, Zierweg(aussen, modell)?["name"] as string ?? modell, g));
        }
        return liste;
    }

    /// <summary>
    /// S 0.22.1 (Weisung P12-94, Nico 1a): Orte der Karte, deren Aussehen ein selbst gebautes Gebaeude ist („Aussehen tauschen“).
    /// Sie bekommen denselben gelben Hinweis wie ein frei stehendes eigenes Gebaeude (<see cref="EigeneGebaeude"/>).
    /// </summary>
    public static List<(string Ort, string Name)> EigeneOrte(PyObjekt aussen)
    {
        var gebaut = Lernplattform.Core.Stadtbau.Bauteile.Gebaeude(Aussen.Stadtdaten(aussen));
        return [.. AussenBearbeitung.Orte(aussen).Where(o => Py.Hat(gebaut, Py.Str(o["modell"])))
            .Select(o => (Py.Str(o["id"]), o["name"] as string is { Length: > 0 } n ? n : Py.Str(o["id"])))];
    }

    /// <summary>
    /// Welle Q (Sammelliste 229): ein Fund draussen nennt ein Gebaeude ohne Ort mit dem Modellnamen („bau:…“, so heisst es im
    /// gemeinsamen Teil). Das Werkzeug setzt dafuer den Namen ein, den der Blueprint traegt; ohne Namen bleibt der Text.
    /// Gegenprobe: <see cref="ModellnamenAltFuerTests"/>.
    /// </summary>
    public static StadtbauFund MitNamen(PyObjekt aussen, StadtbauFund f)
    {
        var such = "„" + Lernplattform.Core.Stadtbau.Bauteile.ModellPraefix;
        if (ModellnamenAltFuerTests || !f.Text.Contains(such, StringComparison.Ordinal))
        {
            return f;
        }
        var text = f.Text;
        var start = 0;
        while ((start = text.IndexOf(such, start, StringComparison.Ordinal)) >= 0)
        {
            var ende = text.IndexOf('“', start);
            if (ende < 0)
            {
                break;
            }
            var modell = text[(start + 1)..ende];
            var neu = Zierweg(aussen, modell)?["name"] as string is { Length: > 0 } name ? name : modell;
            text = text[..(start + 1)] + neu + text[ende..];
            start += neu.Length + 2;
        }
        return f with { Text = text };
    }

    /// <summary>Gegenprobe Sammelliste 229: Funde nennen den Modellnamen wie bis S 0.16.0.</summary>
    public static bool ModellnamenAltFuerTests { get; set; }

    public static List<StadtbauFund> Pruefen(PyObjekt aussen)
    {
        var funde = new List<StadtbauFund>();
        foreach (var (_, name, g) in EigeneGebaeude(aussen))
        {
            var (mx, my) = GebaeudeMitte(aussen, g);
            funde.Add(new StadtbauFund(Texte.EigenesGebaeudeNochNicht(name), Texte.EigenesGebaeudeTipp, false, mx, my));
        }
        foreach (var (ort, name) in EigeneOrte(aussen))
        {
            var (x, y, w, h) = AussenBearbeitung.HausFlaeche(aussen, new AussenStelle(AussenArt.Haus, -1, 0, ort));
            funde.Add(new StadtbauFund(Texte.EigenesGebaeudeNochNicht(name), Texte.EigenesGebaeudeTipp, false, x + (w / 2), y + (h / 2)));
        }
        var mitTuer = Zierwege(aussen).Select(z => Py.Str(z["modell"])).ToHashSet(StringComparer.Ordinal);
        foreach (var gruppe in Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "gebaeude") && mitTuer.Contains(Py.Str(t["modell"])))
            .GroupBy(t => Py.Str(t["modell"]), StringComparer.Ordinal))
        {
            foreach (var g in gruppe.Skip(1))
            {
                var (mx, my) = GebaeudeMitte(aussen, g);
                funde.Add(new StadtbauFund(Texte.BlueprintSchonDa + " („" + (Zierweg(aussen, gruppe.Key)?["name"] as string ?? gruppe.Key) + "“)", Texte.BlueprintSchonDaTipp, true, mx, my));
            }
        }
        // gelb: der Innenraum eines gesetzten Blueprints kommt noch nicht ins Spiel (nichts geht verloren: Projekt und Blueprint)
        foreach (var (modell, bau) in Aussen.Bauten(aussen))
        {
            if (Aussen.Landschaft(aussen).OfType<PyObjekt>().FirstOrDefault(t => Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], modell)) is { } g)
            {
                var (mx, my) = GebaeudeMitte(aussen, g);
                funde.Add(new StadtbauFund(Texte.BauInnenNochNicht(Get(Objekt(bau), "name") as string ?? modell), Texte.BauInnenNochNichtTipp, false, mx, my));
            }
        }
        return funde;
    }

    // ---- Datei --------------------------------------------------------------------------------------

    private PyObjekt OhnePruefsumme() => new()
    {
        { "format", Format },
        { "formatversion", FormatVersion },
        { "werkzeug", Werkzeug.Kennzeile },
        { "name", Name },
        { "herkunft", Herkunft },
        { "modell", Modell },
        { "teile", Teile },
        { "tuer", Tuer },
        { "weg", Weg },
        { "schild", Schild },
        { "innenraum", Innenraum },
        { "personen", Personen },
        { "bauteile", Bauteile },
        { "eingang", Eingang },
    };

    /// <summary>Inhalt der Datei: je Feld eine Zeile (json.dumps), Pruefsumme sha256 wie bei der Fassung.</summary>
    public byte[] Schreiben()
    {
        var objekt = OhnePruefsumme();
        objekt.Add("pruefsumme", StadtFassung.Pruefsumme(objekt));
        var text = new StringBuilder("{\n");
        for (var i = 0; i < objekt.Count; i++)
        {
            text.Append(' ').Append(PythonJson.Standard(objekt[i].Key)).Append(": ").Append(PythonJson.Standard(objekt[i].Value));
            text.Append(i + 1 < objekt.Count ? ",\n" : "\n");
        }
        return PythonJson.Utf8(text.Append("}\n").ToString());
    }

    /// <summary>
    /// Liest einen Blueprint. Kaputt (kein JSON, fehlendes oder unbekanntes Feld, falsche Pruefsumme, falsche Werte) oder
    /// aus einer neueren Werkzeug-Version: <see cref="StadtbauFehler"/> mit dem Grund in einfachen Worten.
    /// </summary>
    public static Blueprint Lesen(ReadOnlySpan<byte> daten)
    {
        object? roh;
        try
        {
            roh = Py.Lesen(daten);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or DecoderFallbackException)
        {
            throw new StadtbauFehler([Texte.BlueprintKaputt]);
        }
        if (roh is not PyObjekt d || !Py.Gleich(Get(d, "format"), Format))
        {
            throw new StadtbauFehler([Texte.BlueprintKeiner]);
        }
        if (!Py.IstNurInt(Get(d, "formatversion")))
        {
            throw new StadtbauFehler([Texte.BlueprintKaputt]);
        }
        if (Py.Int(d["formatversion"]) > FormatVersion)
        {
            throw new StadtbauFehler([Texte.BlueprintNeuer]);
        }
        if (Py.Int(d["formatversion"]) < 1)
        {
            throw new StadtbauFehler([Texte.BlueprintKaputt]);
        }
        var felder = Py.Int(d["formatversion"]) == 1 ? Felder1 : Felder;
        var funde = new List<string>();
        funde.AddRange(felder.Where(f => !Py.Hat(d, f)).Select(f => $"Feld „{f}“ fehlt."));
        funde.AddRange(d.Select(p => p.Key).Where(k => !felder.Contains(k)).Select(k => $"Feld „{k}“ ist unbekannt."));
        if (funde.Count > 0)
        {
            throw new StadtbauFehler(funde);
        }
        var ohne = new PyObjekt();
        ohne.AddRange(d.Where(p => p.Key != "pruefsumme"));
        if (!Py.Gleich(d["pruefsumme"], StadtFassung.Pruefsumme(ohne)))
        {
            throw new StadtbauFehler([Texte.BlueprintPruefsumme]);
        }
        static bool Punkt(object? p) => p is List<object?> { Count: 2 } l && l.All(z => z is long or double);
        if (d["name"] is not string name || !NameGueltig(name) || d["modell"] is not string { Length: > 0 } modell || d["teile"] is not List<object?> { Count: > 0 } teile
            || d["herkunft"] is not PyObjekt herkunft || d["weg"] is not List<object?> weg || !weg.All(Punkt) || d["personen"] is not List<object?> personen
            || (d["tuer"] is not null && !Punkt(d["tuer"])) || (d["schild"] is not null && !Punkt(d["schild"])) || (d["innenraum"] is not null and not PyObjekt)
            || (d["tuer"] is null && weg.Count > 0) || (d["tuer"] is not null && weg.Count < 2))
        {
            throw new StadtbauFehler([Texte.BlueprintKaputt]);
        }
        var bauteile = Get(d, "bauteile");
        var eingang = Get(d, "eingang");
        if ((bauteile is null) != (eingang is null))
        {
            throw new StadtbauFehler([Texte.BlueprintKaputt]);
        }
        if (bauteile is not null)
        {
            try
            {
                Lernplattform.Core.Stadtbau.Bauteile.TeileLesen(bauteile);
                Lernplattform.Core.Stadtbau.Bauteile.EingaengeLesen(eingang);
            }
            catch (InvalidDataException)
            {
                throw new StadtbauFehler([Texte.BlueprintKaputt]);
            }
        }
        return new Blueprint
        {
            Name = name,
            Herkunft = herkunft,
            Modell = modell,
            Teile = teile,
            Tuer = d["tuer"] as List<object?>,
            Weg = weg,
            Schild = d["schild"] as List<object?>,
            Innenraum = d["innenraum"] as PyObjekt,
            Personen = personen,
            Bauteile = bauteile as List<object?>,
            Eingang = eingang as List<object?>,
        };
    }

    /// <summary>Gleiches Gebaeude (alles ausser Name, Herkunft und Datei)?</summary>
    public bool GleichesGebaeude(Blueprint b) =>
        Modell == b.Modell && PythonJson.Standard(new List<object?> { Teile, Tuer, Weg, Schild, Innenraum, Personen, Bauteile, Eingang })
            == PythonJson.Standard(new List<object?> { b.Teile, b.Tuer, b.Weg, b.Schild, b.Innenraum, b.Personen, b.Bauteile, b.Eingang });

    /// <summary>Mitte der Grundflaeche relativ zur Ecke (fuer Tests und Anzeige).</summary>
    public Vector2 Mitte()
    {
        var bloecke = Teile.OfType<PyObjekt>().Where(t => Py.Gleich(t["art"], "block")).ToList();
        if (bloecke.Count == 0)
        {
            return new Vector2(0.5f, 0.5f);
        }
        var x1 = bloecke.Min(b => Py.F(b["x"]));
        var y1 = bloecke.Min(b => Py.F(b["y"]));
        var x2 = bloecke.Max(b => Py.F(b["x"]) + Py.F(b["w"]));
        var y2 = bloecke.Max(b => Py.F(b["y"]) + Py.F(b["t"]));
        return new Vector2((float)((x1 + x2) / 2), (float)((y1 + y2) / 2));
    }

    // ---- Ablage -------------------------------------------------------------------------------------

    /// <summary>Ein Eintrag der Liste: gelesener Blueprint oder der Grund, warum die Datei nicht lesbar ist.</summary>
    public sealed record Eintrag(string Datei, string Name, Blueprint? Vorlage, string? Fehler, bool Neuer);

    /// <summary>
    /// Alle Dateien im Ordner <c>blueprints/</c>, nach Namen. Unlesbare stehen mit Grund dabei (nichts verschwindet still);
    /// ihr Name ist dann der Dateiname.
    /// </summary>
    public static List<Eintrag> Alle(Werkzeugordner ablage)
    {
        if (!Directory.Exists(ablage.Blueprints))
        {
            return [];
        }
        var liste = new List<Eintrag>();
        foreach (var f in new DirectoryInfo(ablage.Blueprints).GetFiles("*" + Endung))
        {
            try
            {
                var b = Lesen(LesenGeteilt(f.FullName));
                b.Datei = f.FullName;
                liste.Add(new Eintrag(f.FullName, b.Name, b, null, false));
            }
            catch (StadtbauFehler e)
            {
                liste.Add(new Eintrag(f.FullName, Path.GetFileNameWithoutExtension(f.Name), null, string.Join(" ", e.Funde), e.Funde.SequenceEqual([Texte.BlueprintNeuer])));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                liste.Add(new Eintrag(f.FullName, Path.GetFileNameWithoutExtension(f.Name), null, Texte.BlueprintNichtLesbar, false));
            }
        }
        return [.. liste.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Datei, StringComparer.Ordinal)];
    }

    private static byte[] LesenGeteilt(string pfad)
    {
        using var s = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    /// <summary>Sicherungen der Blueprints: <c>sicherungen/(Blueprints)/&lt;Name&gt;/</c>, die letzten 20 je Name.</summary>
    public static Sicherungen SicherungenFuer(Werkzeugordner ablage, string name) =>
        new(ablage, Path.Combine(Sicherungen.BlueprintOrdner, Projekt.DateiName(name)), Endung);

    /// <summary>
    /// Neu speichern (ein Name, den es schon gibt, ist ein Fehler: <see cref="IOException"/>). Jede gespeicherte Fassung
    /// kommt zusaetzlich in die Sicherungen. Rueckgabe: Pfad.
    /// </summary>
    public string Speichern(Werkzeugordner ablage, DateTime zeit)
    {
        var pfad = Path.Combine(ablage.Blueprints, DateiName(Name));
        if (File.Exists(pfad) || Alle(ablage).Any(e => string.Equals(e.Name, Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new IOException(Texte.BlueprintGibtEs);
        }
        var daten = Schreiben();
        Werkzeugordner.SicherSchreiben(pfad, daten);
        SicherungenFuer(ablage, Name).Anlegen(daten, zeit);
        Datei = pfad;
        return pfad;
    }

    /// <summary>
    /// Umbenennen: neue Datei mit neuem Namen, dann die alte weg (beide Fassungen bleiben in den Sicherungen). Ein Name,
    /// den es schon gibt, ist ein Fehler (<see cref="IOException"/>). Rueckgabe: der umbenannte Blueprint.
    /// </summary>
    public static Blueprint Umbenennen(Werkzeugordner ablage, string datei, string neuerName, DateTime zeit)
    {
        if (!NameGueltig(neuerName))
        {
            throw new ArgumentException("Name ungültig", nameof(neuerName));
        }
        var alt = Lesen(LesenGeteilt(datei));
        var neu = new Blueprint
        {
            Name = neuerName,
            Herkunft = alt.Herkunft,
            Modell = alt.Modell,
            Teile = alt.Teile,
            Tuer = alt.Tuer,
            Weg = alt.Weg,
            Schild = alt.Schild,
            Innenraum = alt.Innenraum,
            Personen = alt.Personen,
            Bauteile = alt.Bauteile,
            Eingang = alt.Eingang,
        };
        var ziel = Path.Combine(ablage.Blueprints, DateiName(neuerName));
        // nur Gross-/Kleinschreibung anders: unter Windows dieselbe Datei, unter Linux und Android eine andere
        var selbe = string.Equals(Path.GetFullPath(ziel), Path.GetFullPath(datei), StringComparison.Ordinal);
        var nurSchreibweise = !selbe && string.Equals(Path.GetFullPath(ziel), Path.GetFullPath(datei), StringComparison.OrdinalIgnoreCase);
        if ((!selbe && !nurSchreibweise && File.Exists(ziel)) || Alle(ablage).Any(e => e.Datei != datei && string.Equals(e.Name, neuerName, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new IOException(Texte.BlueprintGibtEs);
        }
        SicherungenFuer(ablage, alt.Name).Anlegen(File.ReadAllBytes(datei), zeit);
        var daten = neu.Schreiben();
        if (nurSchreibweise)
        {
            var zwischen = ziel + ".neu";
            File.WriteAllBytes(zwischen, daten);
            File.Delete(datei);
            File.Move(zwischen, ziel);
        }
        else
        {
            Werkzeugordner.SicherSchreiben(ziel, daten);
            if (!selbe)
            {
                File.Delete(datei);
            }
        }
        SicherungenFuer(ablage, neuerName).Anlegen(daten, zeit);
        neu.Datei = ziel;
        return neu;
    }

    /// <summary>
    /// S 0.9.0 (Weiterbauen im Editor): die Datei eines vorhandenen Blueprints mit diesem (gleich benannten) ersetzen. Die
    /// alte Fassung kommt vorher in die Sicherungen, die neue danach auch. Rueckgabe: Pfad.
    /// </summary>
    public string Ersetzen(Werkzeugordner ablage, string datei, DateTime zeit)
    {
        var alt = File.ReadAllBytes(datei);
        SicherungenFuer(ablage, Name).Anlegen(alt, zeit);
        var daten = Schreiben();
        Werkzeugordner.SicherSchreiben(datei, daten);
        SicherungenFuer(ablage, Name).Anlegen(daten, zeit);
        Datei = datei;
        return datei;
    }

    /// <summary>Loeschen (nach der Rueckfrage in der Oberflaeche): die Datei kommt vorher in die Sicherungen, auch eine kaputte.</summary>
    public static void Loeschen(Werkzeugordner ablage, string datei, DateTime zeit)
    {
        var daten = File.ReadAllBytes(datei);
        string name;
        try
        {
            name = Lesen(daten).Name;
        }
        catch (StadtbauFehler)
        {
            name = Path.GetFileNameWithoutExtension(datei);
        }
        SicherungenFuer(ablage, name).Anlegen(daten, zeit);
        File.Delete(datei);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Blueprint {Name} ({Modell})");
}

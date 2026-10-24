using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Spielinhalte aus <c>inhalte/spiel/</c> laden wie <c>fisi_game.load_game_content</c>
/// (Python 0.63.2, Z. 262-415): Vorlagen mit "varianten" aufloesen (<c>expand_variants</c>),
/// Zeilenlisten zu Texten (<c>_lines</c>), Story-Texte (<c>_story_value</c>), Serverfarm-Teile je Art
/// (<c>_farm_file</c>). Die Werte bleiben Python-Werte (<see cref="Py"/>), damit Pruefsummen und
/// Rechnungen genau gleich sind. Vergleichswerte G42 (<c>tests/Vergleich/spiel_inhalte.json</c>).
/// </summary>
public static class SpielInhalt
{
    public const string PlatzhalterAuf = "{{";
    public const string VarianteZeichen = "#";

    /// <summary>Schluessel des Ergebnisses in Pythons Reihenfolge.</summary>
    public static readonly IReadOnlyList<string> Schluessel =
    [
        "aufgaben", "zwischenfaelle", "kollegen", "gebaeude", "kunden", "wohnungen", "story", "balancing",
        "hardware", "firma", "orte", "erfolge", "projektarbeiten", "serverfarm",
    ];

    private static readonly string[] TextFelder = ["ticket", "hilfe", "erklaerung"];

    /// <summary>
    /// load_game_content. <paramref name="lesen"/> liefert den Inhalt von <c>&lt;name&gt;.json</c>
    /// oder null, wenn die Datei fehlt (os.path.exists). <paramref name="projektarbeiten"/> ist
    /// PROJEKTARBEITEN aus dem Lernteil (fisi_core.load_content), schon umgewandelt.
    /// </summary>
    public static PyObjekt Laden(Func<string, byte[]?> lesen, object? projektarbeiten)
    {
        object? Lies(string name) => Py.Lesen(lesen(name) ?? throw new FileNotFoundException(name + ".json fehlt"));
        bool Gibt(string name) => lesen(name) is not null;

        var aufgaben = VariantenAufloesen(Lies("aufgaben"));
        if (Gibt("aufgaben_vorlagen"))
        {
            var vorlagen = Lies("aufgaben_vorlagen");
            aufgaben.AddRange(VariantenAufloesen(vorlagen is PyObjekt v && Py.Hat(v, "vorlagen") ? v["vorlagen"] : new List<object?>(), vorlage: true));
        }
        var faelle = VariantenAufloesen(Lies("zwischenfaelle"));
        foreach (var aufgabe in aufgaben.Concat(faelle))
        {
            ZeilenFelder(aufgabe);
        }
        foreach (var fall in faelle)
        {
            Py.Setzen(fall, "zwischenfall", true);
        }
        var story = Lies("story") as PyObjekt ?? throw new InvalidDataException("story.json ist kein Objekt");
        var storyWerte = new PyObjekt();
        foreach (var (k, wert) in story)
        {
            Py.Setzen(storyWerte, k, StoryWert(wert));
        }
        return new PyObjekt
        {
            { "aufgaben", aufgaben.Cast<object?>().ToList() },
            { "zwischenfaelle", faelle.Cast<object?>().ToList() },
            { "kollegen", Lies("kollegen") },
            { "gebaeude", Lies("gebaeude") },
            { "kunden", Lies("kunden") },
            { "wohnungen", Lies("wohnungen") },
            { "story", storyWerte },
            { "balancing", Lies("balancing") },
            { "hardware", Lies("hardware") },
            { "firma", Gibt("firma") ? Lies("firma") : new PyObjekt() },
            { "orte", Gibt("orte") ? Lies("orte") : new PyObjekt() },
            { "erfolge", Gibt("erfolge") ? Lies("erfolge") : new PyObjekt() },
            { "projektarbeiten", projektarbeiten },
            { "serverfarm", Gibt("serverfarm") ? Serverfarm(Lies("serverfarm")) : new PyObjekt() },
        };
    }

    /// <summary>Wie <see cref="Laden(Func{string, byte[]?}, object?)"/> aus einem Ordner.</summary>
    public static PyObjekt Laden(string ordner, object? projektarbeiten) =>
        Laden(name =>
        {
            var pfad = Path.Combine(ordner, name + ".json");
            return File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;
        }, projektarbeiten);

    /// <summary>_lines: Liste von Zeilen = ein Text mit "\n", sonst <c>value or ""</c>.</summary>
    public static object? Zeilen(object? wert) => wert switch
    {
        List<object?> liste => string.Join("\n", liste.Select(z => z as string
            ?? throw new InvalidDataException("Zeilenliste enthaelt keinen Text"))),
        _ => Py.Wahr(wert) ? wert : "",
    };

    private static void ZeilenFelder(PyObjekt aufgabe)
    {
        foreach (var feld in TextFelder)
        {
            Py.Setzen(aufgabe, feld, Zeilen(aufgabe[feld]));
        }
    }

    /// <summary>_story_value</summary>
    public static object? StoryWert(object? wert)
    {
        if (wert is PyObjekt d)
        {
            var neu = new PyObjekt();
            foreach (var (k, v) in d)
            {
                Py.Setzen(neu, k, Zeilen(v));
            }
            return neu;
        }
        if (wert is List<object?> { Count: > 0 } liste && liste[0] is List<object?>)
        {
            return liste.Select(Zeilen).ToList();
        }
        return Zeilen(wert);
    }

    /// <summary>_farm_file (ohne das Lesen): Teile je Art werden eine Liste mit "typ".</summary>
    private static object? Serverfarm(object? daten)
    {
        if (daten is not PyObjekt d)
        {
            throw new InvalidDataException("serverfarm.json ist kein Objekt");
        }
        if (d["teile"] is PyObjekt teile)
        {
            var liste = new List<object?>();
            foreach (var (art, eintraege) in teile)
            {
                if (art.StartsWith('_'))
                {
                    continue;
                }
                foreach (var eintrag in Liste(eintraege))
                {
                    var neu = Py.Flach(eintrag as PyObjekt ?? throw new InvalidDataException("Serverfarm-Teil ist kein Objekt"));
                    Py.Setzen(neu, "typ", art);
                    liste.Add(neu);
                }
            }
            Py.Setzen(d, "teile", liste);
        }
        if (Py.Wahr(d["aufgaben"]))
        {
            foreach (var aufgabe in Liste(d["aufgaben"]))
            {
                ZeilenFelder(aufgabe as PyObjekt ?? throw new InvalidDataException("Serverfarm-Auftrag ist kein Objekt"));
            }
        }
        return d;
    }

    /// <summary>_fill: Platzhalter {{name}} in allen Texten ersetzen (neue Listen und dicts).</summary>
    public static object? Fuellen(object? wert, PyObjekt werte)
    {
        switch (wert)
        {
            case string s:
                foreach (var (k, text) in werte)
                {
                    s = s.Replace("{{" + k + "}}", Py.Str(text), StringComparison.Ordinal);
                }
                return s;
            case List<object?> liste:
                return liste.Select(e => Fuellen(e, werte)).ToList();
            case PyObjekt d:
                var neu = new PyObjekt();
                foreach (var (k, v) in d)
                {
                    neu.Add(k, Fuellen(v, werte));
                }
                return neu;
            default:
                return wert;
        }
    }

    /// <summary>expand_variants: Eintraege mit "varianten" in einzelne Auftraege aufloesen.</summary>
    public static List<PyObjekt> VariantenAufloesen(object? eintraege, bool vorlage = false)
    {
        var ergebnis = new List<PyObjekt>();
        foreach (var roh in Liste(eintraege))
        {
            var eintrag = roh as PyObjekt ?? throw new InvalidDataException("Auftrag ist kein Objekt");
            var varianten = eintrag["varianten"];
            if (!Py.Wahr(varianten))
            {
                var aufgabe = Py.Flach(eintrag);
                if (vorlage)
                {
                    Py.Setzen(aufgabe, "vorlage", Pflicht(eintrag, "id"));
                }
                ergebnis.Add(aufgabe);
                continue;
            }
            var basis = new PyObjekt();
            basis.AddRange(eintrag.Where(p => p.Key is not ("varianten" or "wiederholen")));
            var wiederholen = Py.Wahr(eintrag["wiederholen"]) ? eintrag["wiederholen"] as PyObjekt
                ?? throw new InvalidDataException("wiederholen ist kein Objekt") : null;
            var nummer = 0;
            foreach (var rohVariante in Liste(varianten))
            {
                var variante = rohVariante as PyObjekt ?? throw new InvalidDataException("Variante ist kein Objekt");
                var werte = Py.Wahr(variante["werte"]) ? variante["werte"] as PyObjekt
                    ?? throw new InvalidDataException("werte ist kein Objekt") : new PyObjekt();
                var aufgabe = (PyObjekt)Fuellen(Py.Kopie(basis), werte)!;
                foreach (var (k, v) in variante)
                {
                    if (k is not ("werte" or "auffaellig"))
                    {
                        Py.Setzen(aufgabe, k, Py.Kopie(v));
                    }
                }
                if (Py.Hat(variante, "auffaellig"))
                {
                    var auffaellig = variante["auffaellig"];
                    if (Py.Wahr(aufgabe["pruefpunkte"]))
                    {
                        foreach (var punkt in Liste(aufgabe["pruefpunkte"]))
                        {
                            var p = punkt as PyObjekt ?? throw new InvalidDataException("Pruefpunkt ist kein Objekt");
                            Py.Setzen(p, "auffaellig", Py.Enthaelt(auffaellig, Pflicht(p, "id")));
                        }
                    }
                }
                if (!Py.Hat(variante, "ab_tag") && wiederholen is not null)
                {
                    var start = Py.Hat(wiederholen, "start") ? wiederholen["start"] : 1L;
                    var abstand = Py.Hat(wiederholen, "abstand") ? wiederholen["abstand"] : 1L;
                    Py.Setzen(aufgabe, "ab_tag", Py.Plus(start, Py.Mal(nummer, abstand)));
                }
                var id = Pflicht(eintrag, "id");
                Py.Setzen(aufgabe, "id", nummer == 0 ? id : Py.Str(id) + VarianteZeichen + (nummer + 1));
                Py.Setzen(aufgabe, "gruppe", id);
                if (vorlage)
                {
                    Py.Setzen(aufgabe, "vorlage", id);
                }
                ergebnis.Add(aufgabe);
                nummer++;
            }
        }
        return ergebnis;
    }

    /// <summary>_leftover_placeholder: steht irgendwo noch "{{"?</summary>
    public static bool PlatzhalterUebrig(object? wert) => wert switch
    {
        string s => s.Contains(PlatzhalterAuf, StringComparison.Ordinal),
        List<object?> liste => liste.Any(PlatzhalterUebrig),
        PyObjekt d => d.Any(p => PlatzhalterUebrig(p.Value)),
        _ => false,
    };

    private static object? Pflicht(PyObjekt d, string schluessel) =>
        Py.Hat(d, schluessel) ? d[schluessel] : throw new KeyNotFoundException(schluessel);

    private static List<object?> Liste(object? wert) =>
        wert as List<object?> ?? throw new InvalidDataException("Liste erwartet");
}

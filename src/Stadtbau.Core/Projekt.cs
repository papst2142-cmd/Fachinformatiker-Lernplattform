using System.Globalization;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Ein Schritt im Verlauf: Ort, was getan wurde, Grundriss vorher und nachher (JSON-Text). Ort <see cref="Projekt.StadtOrt"/>
/// = Stadt draussen: vorher und nachher halten nur die geaenderten Teile (<see cref="Aussen.Teile"/>) als Objekt
/// { Pfad: [Wert] } (leere Liste = Teil fehlt).
/// </summary>
public sealed record Schritt(string Ort, string Was, string Vorher, string Nachher);

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitte 3, 5 und 9): ein Projekt des Werkzeugs. Es baut auf einer Fassung von
/// Stadt 1 auf (heute Fassung 0, eine Kopie; das Original bleibt unberuehrt) und haelt die bearbeiteten Grundrisse der
/// Innenraeume. Nach jeder Aenderung wird automatisch gespeichert (Datei <c>projekte/&lt;Name&gt;.fisbau</c>, erst Kopie, dann
/// ersetzen); Rueckgaengig beliebig oft, solange das Projekt offen ist, die letzten <see cref="GespeicherteSchritte"/>
/// Schritte bleiben auch nach dem Schliessen. Sicherungen: <see cref="Sicherungen.Hoechstens"/> je Projekt. Eine Datei
/// aus einer neueren Werkzeug-Version wird nur gelesen und nie ueberschrieben (<see cref="NurLesen"/>).
/// </summary>
public sealed class Projekt
{
    public const string Format = "fisbau";

    /// <summary>
    /// 3 seit Welle 4c (Teil "bauten" in "aussen": Innenraeume gesetzter Blueprints, Meldung P12-23 Frage 1 a; ein Werkzeug S 0.4.0
    /// oeffnet die Datei damit nur zum Lesen), 2 seit Welle 3 (Felder "aussen" und "leer"). Dateien mit Formatnummer 1 und 2
    /// lassen sich weiter oeffnen.
    /// </summary>
    public const long FormatVersion = 3;

    /// <summary>Kennung des Innenraums eines gesetzten Blueprints: "bau:" und der Modellname (Teil "bauten" der Stadt draussen).</summary>
    public const string BauPraefix = "bau:";

    /// <summary>Ort der Schritte an der Stadt draussen.</summary>
    public const string StadtOrt = "_stadt";

    public const string Endung = ".fisbau";

    public const int GespeicherteSchritte = 200;

    public static readonly string[] Felder = ["format", "formatversion", "werkzeug", "name", "stadt", "basis", "leer", "ort", "nachricht", "grundrisse", "aussen", "verlauf", "zeiger"];

    /// <summary>Felder der Formatnummer 1 (Welle 2).</summary>
    public static readonly string[] FelderVersion1 = ["format", "formatversion", "werkzeug", "name", "stadt", "basis", "ort", "nachricht", "grundrisse", "verlauf", "zeiger"];

    private readonly List<Schritt> _verlauf = [];
    private readonly Dictionary<string, PyObjekt> _grundrisse = new(StringComparer.Ordinal);
    private int _zeiger;
    private PyObjekt? _aussen;
    private Dictionary<string, string>? _basisTeile;
    private string? _datei;

    private Projekt(string name, Spieldaten daten, Werkzeugordner ablage, (double Breite, double Hoehe)? leer = null, bool frei = false)
    {
        Name = name;
        Daten = daten;
        Werkzeugordner = ablage;
        Leer = leer;
        Frei = leer is not null && frei;
        Basis = leer is { } l ? Aussen.Leer(daten.Fassung, LeerStadtId(name), l.Breite, l.Hoehe, Frei) : daten.Fassung;
        Ort = Orte.FirstOrDefault() ?? "";
    }

    /// <summary>Fassung, auf der das Projekt aufbaut: die eingebaute Fassung von Stadt 1 oder eine leere Stadt.</summary>
    public StadtFassung Basis { get; }

    /// <summary>Groesse einer leeren Stadt (null = Stadt 1).</summary>
    public (double Breite, double Hoehe)? Leer { get; }

    /// <summary>
    /// Patch S 0.5.1 (Weisungen P12-37/P12-38/P12-39): leere Stadt ganz frei (Schalter darstellung.kleingruen = false,
    /// kein Gestruepp, keine Steine, kein Kleingruen). Jede neue leere Stadt ist frei (ohne Wahl, Nico); in der
    /// Projektdatei steht dafuer "frei": true im Feld "leer". Aeltere Projekte ohne diese Angabe bleiben unveraendert
    /// (mit Kleingruen wie bisher), sie werden nie still umgestellt.
    /// </summary>
    public bool Frei { get; }

    /// <summary>
    /// Kennung einer leeren Stadt: "neu_" und der Projektname in ASCII, klein, jedes andere Zeichen als _, hoechstens 32
    /// Zeichen (Regel der Fassung fuer "stadt.id").
    /// </summary>
    public static string LeerStadtId(string name)
    {
        var id = "neu_" + new string([.. DateiName(name).ToLowerInvariant().Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '_')]);
        return id.Length > 32 ? id[..32] : id;
    }

    /// <summary>Orte mit bearbeitbarem Innenraum in der Basis (leere Stadt: keine).</summary>
    public IReadOnlyList<string> Orte => InnenGrundriss.Orte(Basis.Innenraeume);

    /// <summary>Innenraeume neuer Gebaeude aus Blueprints ("bau:modell", nur im Werkzeug, kommen noch nicht in den Export).</summary>
    public IReadOnlyList<string> Bauten => [.. Aussen.Bauten(AussenStand).Select(b => BauPraefix + b.Key)];

    /// <summary>Alle bearbeitbaren Innenraeume: Orte der Basis, dann neue Gebaeude.</summary>
    public IReadOnlyList<string> Innenorte => [.. Orte, .. Bauten];

    /// <summary>Innenraum zu einem Haus oder Gebaeude draussen (Ort der Basis oder neues Gebaeude), sonst null.</summary>
    public string? InnenortVon(AussenStelle s)
    {
        if (s.Art == AussenArt.Haus)
        {
            return Orte.Contains(s.Ort) ? s.Ort : null;
        }
        if (s.Art == AussenArt.Gebaeude && Aussen.Landschaft(AussenStand).ElementAtOrDefault(s.Nummer) is PyObjekt g && g["modell"] is string m
            && Aussen.Bauten(AussenStand)[m] is PyObjekt)
        {
            return BauPraefix + m;
        }
        return null;
    }

    public static bool IstBau(string ort) => ort.StartsWith(BauPraefix, StringComparison.Ordinal);

    private PyObjekt? Bau(string ort) => IstBau(ort) ? Aussen.Bauten(AussenStand)[ort[BauPraefix.Length..]] as PyObjekt : null;

    public string Name { get; }

    public Spieldaten Daten { get; }

    public Werkzeugordner Werkzeugordner { get; }

    /// <summary>Innenraum, der gerade bearbeitet wird.</summary>
    public string Ort { get; set; }

    /// <summary>Freiwillige Nachricht an CC (steht in der Export-Datei).</summary>
    public string Nachricht { get; set; } = "";

    /// <summary>Datei aus einer neueren Werkzeug-Version: nur lesen, nie speichern.</summary>
    public bool NurLesen { get; private set; }

    /// <summary>Projektdatei: geoeffnete Datei, sonst der Name in ASCII (<see cref="DateiName"/>).</summary>
    public string Datei => _datei ?? Path.Combine(Werkzeugordner.Projekte, DateiName(Name) + Endung);

    /// <summary>
    /// Projektname als Dateiname (Sammelliste 219 Punkt 3): Umlaute und ß als ae, oe, ue, ss, Leerzeichen bleiben; andere
    /// Zeichen ausserhalb von ASCII werden ohne Akzent geschrieben, was dann noch fehlt, wird _.
    /// </summary>
    public static string DateiName(string name)
    {
        var text = new StringBuilder();
        foreach (var c in name)
        {
            var ersatz = c switch
            {
                'ä' => "ae", 'ö' => "oe", 'ü' => "ue", 'Ä' => "Ae", 'Ö' => "Oe", 'Ü' => "Ue", 'ß' => "ss",
                _ when c < 128 => c.ToString(),
                _ => new string([.. c.ToString().Normalize(NormalizationForm.FormD).Where(z => z < 128)]),
            };
            text.Append(ersatz.Length == 0 ? "_" : ersatz);
        }
        return text.ToString();
    }

    public int SchritteZurueck => _zeiger;

    public int SchritteVor => _verlauf.Count - _zeiger;

    public IReadOnlyList<Schritt> Verlauf => _verlauf;

    /// <summary>Orte, deren Grundriss sich gegenueber der Basis geaendert hat.</summary>
    public IReadOnlyList<string> GeaenderteOrte =>
        [.. Orte.Where(o => _grundrisse.TryGetValue(o, out var g) && PythonJson.Standard(g) != PythonJson.Standard(Original(o)))];

    /// <summary>Wird nach jeder Aenderung, jedem Rueckgaengig und Wiederholen gerufen (Oberflaeche zeichnet neu).</summary>
    public event Action? Geaendert;

    /// <summary>Gueltiger Projektname: Buchstaben, Ziffern, Leerzeichen, - und _, 1 bis 40 Zeichen.</summary>
    public static bool NameGueltig(string name) =>
        name.Trim().Length is > 0 and <= 40 && name.Trim() == name && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_');

    public static Projekt Neu(string name, Spieldaten daten, Werkzeugordner ablage, string? ort = null, (double Breite, double Hoehe)? leer = null)
    {
        if (!NameGueltig(name))
        {
            throw new ArgumentException("Projektname ungültig", nameof(name));
        }
        if (leer is { } l && !Aussen.GroesseGueltig(l.Breite, l.Hoehe))
        {
            throw new ArgumentException("Größe ungültig", nameof(leer));
        }
        var p = new Projekt(name, daten, ablage.Anlegen(), leer, frei: true);
        if (ort is not null && p.Orte.Contains(ort))
        {
            p.Ort = ort;
        }
        if (File.Exists(p.Datei))
        {
            throw new IOException("Ein Projekt mit diesem Namen gibt es schon.");
        }
        p.Speichern(vonHand: true);
        return p;
    }

    /// <summary>Der unveraenderte Grundriss eines Orts in der Basis-Fassung (flach, mit allen Zusaetzen).</summary>
    public PyObjekt Original(string ort) => InnenGrundriss.Grundriss(Basis.Innenraeume, ort);

    /// <summary>Der aktuelle Grundriss eines Orts (bearbeitet oder die Basis). Nur ueber <see cref="Aendern"/> veraendern.</summary>
    public PyObjekt Grundriss(string ort)
    {
        if (IstBau(ort))
        {
            return Bau(ort)?["grundriss"] as PyObjekt ?? throw new KeyNotFoundException(ort);
        }
        if (!_grundrisse.TryGetValue(ort, out var g))
        {
            g = Original(ort);
            _grundrisse[ort] = g;
        }
        return g;
    }

    public List<PyObjekt> Personen(string ort) => IstBau(ort)
        ? [.. (Bau(ort)?["personen"] as List<object?> ?? []).OfType<PyObjekt>()]
        : InnenGrundriss.Personen(Basis.Innenraeume, ort);

    // ---- Stadt draussen ---------------------------------------------------------------------------

    /// <summary>Die Stadt draussen (Kopie der Basis mit allen Aenderungen). Nur ueber <see cref="AussenAendern{T}"/> veraendern.</summary>
    public PyObjekt AussenStand => _aussen ??= Aussen.Aus(Basis);

    private Dictionary<string, string> BasisTeile => _basisTeile ??= Aussen.TeileText(Aussen.Aus(Basis));

    /// <summary>Teile der Stadt draussen, die sich gegenueber der Basis geaendert haben (Pfade wie <see cref="Aussen.Teile"/>).</summary>
    public IReadOnlyList<string> GeaenderteTeile
    {
        get
        {
            if (_aussen is null)
            {
                return [];
            }
            var jetzt = Aussen.TeileText(_aussen);
            var basis = BasisTeile;
            return [.. jetzt.Keys.Union(basis.Keys).Where(k => !jetzt.TryGetValue(k, out var a) || !basis.TryGetValue(k, out var b) || a != b).Order(StringComparer.Ordinal)];
        }
    }

    public bool AussenGeaendert => GeaenderteTeile.Count > 0;

    /// <summary>
    /// Eine Aenderung an der Stadt draussen: die geaenderten Teile vorher und nachher kommen in den Verlauf (Ort
    /// <see cref="StadtOrt"/>), danach wird automatisch gespeichert. Aendert <paramref name="aenderung"/> nichts, gibt es keinen Schritt.
    /// </summary>
    public T AussenAendern<T>(string was, Func<PyObjekt, T> aenderung) => AussenAendern(StadtOrt, was, aenderung);

    private T AussenAendern<T>(string schrittOrt, string was, Func<PyObjekt, T> aenderung)
    {
        if (NurLesen)
        {
            throw new InvalidOperationException("Projekt ist nur zum Lesen geöffnet.");
        }
        var a = AussenStand;
        var vorher = Aussen.TeileText(a);
        var ergebnis = aenderung(a);
        var nachher = Aussen.TeileText(a);
        var pfade = vorher.Keys.Union(nachher.Keys).Where(k => !vorher.TryGetValue(k, out var v) || !nachher.TryGetValue(k, out var n) || v != n).Order(StringComparer.Ordinal).ToList();
        if (pfade.Count > 0)
        {
            _verlauf.RemoveRange(_zeiger, _verlauf.Count - _zeiger);
            _verlauf.Add(new Schritt(schrittOrt, was, TeileObjekt(vorher, pfade), TeileObjekt(nachher, pfade)));
            _zeiger = _verlauf.Count;
            Gespeichert();
        }
        return ergebnis;
    }

    public void AussenAendern(string was, Action<PyObjekt> aenderung) => AussenAendern(was, a =>
    {
        aenderung(a);
        return 0;
    });

    /// <summary>{ Pfad: [Wert] } als Text (Werte liegen schon als json.dumps vor; leere Liste = Teil fehlt).</summary>
    private static string TeileObjekt(Dictionary<string, string> teile, List<string> pfade)
    {
        var text = new StringBuilder("{");
        foreach (var pfad in pfade)
        {
            text.Append(text.Length > 1 ? ", " : "").Append(PythonJson.Standard(pfad)).Append(": ").Append(teile.TryGetValue(pfad, out var w) ? "[" + w + "]" : "[]");
        }
        return text.Append('}').ToString();
    }

    private static void TeileAnwenden(PyObjekt aussen, PyObjekt teile)
    {
        foreach (var (pfad, wert) in teile)
        {
            var liste = wert as List<object?>;
            Aussen.TeilSetzen(aussen, pfad, liste is { Count: > 0 } ? liste[0] : null, liste is { Count: > 0 });
        }
    }

    /// <summary>Wurde zuletzt (Rueckgaengig/Wiederholen) ein Schritt der Stadt draussen angewandt?</summary>
    public bool LetzterSchrittDraussen { get; private set; }

    /// <summary>
    /// Eine Aenderung am Grundriss von <paramref name="ort"/>: vorher und nachher kommen in den Verlauf (Schritte nach dem
    /// Zeiger fallen weg), danach wird automatisch gespeichert. Aendert <paramref name="aenderung"/> nichts, gibt es keinen Schritt.
    /// </summary>
    public T Aendern<T>(string ort, string was, Func<PyObjekt, T> aenderung)
    {
        if (NurLesen)
        {
            throw new InvalidOperationException("Projekt ist nur zum Lesen geöffnet.");
        }
        if (IstBau(ort))
        {
            // Innenraum eines neuen Gebaeudes: liegt in der Stadt draussen (Teil "bauten"), der Schritt merkt sich den Innenraum
            return AussenAendern(ort, was, a => aenderung(Grundriss(ort)));
        }
        var g = Grundriss(ort);
        var vorher = PythonJson.Standard(g);
        var ergebnis = aenderung(g);
        var nachher = PythonJson.Standard(g);
        if (vorher != nachher)
        {
            _verlauf.RemoveRange(_zeiger, _verlauf.Count - _zeiger);
            _verlauf.Add(new Schritt(ort, was, vorher, nachher));
            _zeiger = _verlauf.Count;
            Gespeichert();
        }
        return ergebnis;
    }

    public void Aendern(string ort, string was, Action<PyObjekt> aenderung) => Aendern(ort, was, g =>
    {
        aenderung(g);
        return 0;
    });

    public bool Rueckgaengig()
    {
        if (_zeiger == 0 || NurLesen)
        {
            return false;
        }
        Anwenden(_verlauf[--_zeiger], vorher: true);
        Gespeichert();
        return true;
    }

    public bool Wiederholen()
    {
        if (_zeiger >= _verlauf.Count || NurLesen)
        {
            return false;
        }
        Anwenden(_verlauf[_zeiger++], vorher: false);
        Gespeichert();
        return true;
    }

    private void Anwenden(Schritt s, bool vorher)
    {
        var text = vorher ? s.Vorher : s.Nachher;
        LetzterSchrittDraussen = s.Ort == StadtOrt;
        if (LetzterSchrittDraussen || IstBau(s.Ort))
        {
            TeileAnwenden(AussenStand, Lesen(text));
            if (IstBau(s.Ort))
            {
                // Schritt in einem neuen Gebaeude: dorthin wechseln (gibt es das Gebaeude danach nicht mehr: nach draussen)
                LetzterSchrittDraussen = !Bauten.Contains(s.Ort);
                if (!LetzterSchrittDraussen)
                {
                    Ort = s.Ort;
                }
            }
            return;
        }
        _grundrisse[s.Ort] = Lesen(text);
        Ort = s.Ort;
    }

    private static PyObjekt Lesen(string text) => (PyObjekt)Py.Lesen(Encoding.UTF8.GetBytes(text))!;

    private DateTime _letzteSicherung = DateTime.MinValue;

    /// <summary>Spanne, nach der das automatische Speichern zusaetzlich eine Sicherung anlegt.</summary>
    public static readonly TimeSpan SicherungAlle = TimeSpan.FromMinutes(5);

    /// <summary>Uhr fuer Tests.</summary>
    internal Func<DateTime> Uhr { get; set; } = () => DateTime.Now;

    private void Gespeichert()
    {
        _aenderungen++;
        Speichern(vonHand: false);
        Geaendert?.Invoke();
    }

    private long _aenderungen;
    private long _gespeichert;

    /// <summary>
    /// Patch S 0.5.1 (Weisung P12-35): gibt es eine Aenderung, die noch nicht in der Projektdatei steht? Jede Aenderung wird
    /// sofort gespeichert; offen bleibt sie nur, wenn dieses Speichern scheiterte (Datei gesperrt, Platte voll). Dann fragt
    /// "Werkzeug beenden" nach.
    /// </summary>
    public bool Ungespeichert => !NurLesen && _gespeichert != _aenderungen;

    /// <summary>
    /// Projektdatei schreiben (automatisch nach jeder Aenderung, von Hand ueber "Speichern"). Von Hand, beim ersten
    /// Speichern und alle 5 Minuten beim automatischen Speichern kommt eine Sicherung dazu.
    /// </summary>
    public void Speichern(bool vonHand)
    {
        if (NurLesen)
        {
            return;
        }
        var stand = _aenderungen;
        var daten = Schreiben();
        Werkzeugordner.SicherSchreiben(Datei, daten);
        _gespeichert = stand;
        var jetzt = Uhr();
        if (vonHand || jetzt - _letzteSicherung >= SicherungAlle)
        {
            new Sicherungen(Werkzeugordner, Name).Anlegen(daten, jetzt);
            _letzteSicherung = jetzt;
        }
    }

    /// <summary>Inhalt der Projektdatei (JSON, je Feld eine Zeile).</summary>
    public byte[] Schreiben()
    {
        var grundrisse = new PyObjekt();
        foreach (var ort in Orte.Where(_grundrisse.ContainsKey))
        {
            grundrisse.Add(ort, _grundrisse[ort]);
        }
        var aussen = new StringBuilder("{");
        if (_aussen is not null)
        {
            var jetzt = Aussen.TeileText(_aussen);
            foreach (var pfad in GeaenderteTeile)
            {
                aussen.Append(aussen.Length > 1 ? ", " : "").Append(PythonJson.Standard(pfad)).Append(": ").Append(jetzt.TryGetValue(pfad, out var w) ? "[" + w + "]" : "[]");
            }
        }
        object? leer = null;
        if (Leer is { } l)
        {
            var lo = new PyObjekt { { "breite", Bearbeitung.Zahl(l.Breite) }, { "hoehe", Bearbeitung.Zahl(l.Hoehe) } };
            if (Frei)
            {
                lo.Add("frei", true);
            }
            leer = lo;
        }
        var start = Math.Max(0, _verlauf.Count - GespeicherteSchritte);
        // Verlauf: Vorher und Nachher liegen schon als Text in der Form von json.dumps vor (kein neues Lesen beim Speichern)
        var verlauf = new StringBuilder("[");
        foreach (var s in _verlauf.Skip(start))
        {
            verlauf.Append(verlauf.Length > 1 ? ", " : "").Append("{\"ort\": ").Append(PythonJson.Standard(s.Ort)).Append(", \"was\": ").Append(PythonJson.Standard(s.Was))
                .Append(", \"vorher\": ").Append(s.Vorher).Append(", \"nachher\": ").Append(s.Nachher).Append('}');
        }
        var objekt = new List<(string, string)>
        {
            ("format", PythonJson.Standard(Format)), ("formatversion", PythonJson.Standard(FormatVersion)), ("werkzeug", PythonJson.Standard(Werkzeug.Kennzeile)),
            ("name", PythonJson.Standard(Name)), ("stadt", PythonJson.Standard(Basis.StadtId)), ("basis", PythonJson.Standard(Leer is null ? Basis.Nummer : null)),
            ("leer", PythonJson.Standard(leer)), ("ort", PythonJson.Standard(Ort)), ("nachricht", PythonJson.Standard(Nachricht)), ("grundrisse", PythonJson.Standard(grundrisse)),
            ("aussen", aussen.Append('}').ToString()), ("verlauf", verlauf.Append(']').ToString()), ("zeiger", PythonJson.Standard((long)Math.Max(0, _zeiger - start))),
        };
        var text = new StringBuilder("{\n");
        for (var i = 0; i < objekt.Count; i++)
        {
            text.Append(' ').Append(PythonJson.Standard(objekt[i].Item1)).Append(": ").Append(objekt[i].Item2);
            text.Append(i + 1 < objekt.Count ? ",\n" : "\n");
        }
        return PythonJson.Utf8(text.Append("}\n").ToString());
    }

    /// <summary>
    /// Projekt oeffnen. Fehlt ein Feld oder ist die Datei kaputt: <see cref="StadtbauFehler"/> mit Grund. Neuere
    /// Formatnummer: nur lesen. Andere Stadt oder Basis als die eingebaute Fassung: Fehler (nichts wird umgeschrieben).
    /// </summary>
    public static Projekt Oeffnen(string datei, Spieldaten daten, Werkzeugordner ablage)
    {
        object? roh;
        try
        {
            roh = Py.Lesen(File.ReadAllBytes(datei));
        }
        catch (System.Text.Json.JsonException)
        {
            throw new StadtbauFehler(["Die Projektdatei ist kein gültiges JSON."]);
        }
        if (roh is not PyObjekt d || !Py.Gleich(d["format"], Format))
        {
            throw new StadtbauFehler(["Das ist keine Projektdatei des Stadtbau-Werkzeugs."]);
        }
        var neuer = Py.IstNurInt(d["formatversion"]) && Py.Int(d["formatversion"]) > FormatVersion;
        var version1 = Py.IstNurInt(d["formatversion"]) && Py.Int(d["formatversion"]) == 1;
        var fehlend = (version1 ? FelderVersion1 : Felder).Where(f => !Py.Hat(d, f)).ToList();
        if (!neuer && fehlend.Count > 0)
        {
            throw new StadtbauFehler([.. fehlend.Select(f => $"Feld „{f}“ fehlt.")]);
        }
        var name = Path.GetFileNameWithoutExtension(datei);
        name = Py.Str(d["name"]) is { Length: > 0 } n && NameGueltig(n) ? n : name;
        (double, double)? leer = null;
        var frei = false;
        if (Py.Hat(d, "leer") && d["leer"] is PyObjekt lo)
        {
            frei = lo["frei"] is true;
            if (lo["breite"] is not (long or double) || lo["hoehe"] is not (long or double) || !Aussen.GroesseGueltig(Py.F(lo["breite"]), Py.F(lo["hoehe"])) || !Py.Gleich(d["stadt"], LeerStadtId(name)) || d["basis"] is not null
                || (Py.Hat(lo, "frei") && lo["frei"] is not bool))
            {
                throw new StadtbauFehler(["Die Angaben zur leeren Stadt (Feld „leer“) passen nicht."]);
            }
            leer = (Py.F(lo["breite"]), Py.F(lo["hoehe"]));
        }
        else if (!Py.Gleich(d["stadt"], daten.Fassung.StadtId) || !Py.IstNurInt(d["basis"]) || Py.Int(d["basis"]) != daten.Fassung.Nummer)
        {
            throw new StadtbauFehler([string.Create(CultureInfo.InvariantCulture,
                $"Das Projekt baut auf {Py.Str(d["stadt"])} Fassung {Py.Str(d["basis"])} auf; dieses Werkzeug kennt {daten.Fassung.StadtId} Fassung {daten.Fassung.Nummer}.")]);
        }
        var p = new Projekt(name, daten, ablage, leer, frei) { NurLesen = neuer, _datei = Path.GetFullPath(datei) };
        if (d["grundrisse"] is PyObjekt g)
        {
            foreach (var (ort, grundriss) in g)
            {
                if (p.Orte.Contains(ort) && grundriss is PyObjekt go)
                {
                    p._grundrisse[ort] = go;
                }
            }
        }
        if (Py.Hat(d, "aussen") && d["aussen"] is PyObjekt ao && ao.Count > 0)
        {
            var bekannt = p.BasisTeile;
            if (ao.Any(t => (!bekannt.ContainsKey(t.Key) && !GueltigerTeil(t.Key)) || t.Value is not List<object?> { Count: <= 1 }))
            {
                throw new StadtbauFehler(["Die Änderungen an der Stadt draußen (Feld „aussen“) passen nicht."]);
            }
            TeileAnwenden(p.AussenStand, ao);
        }
        if (d["verlauf"] is List<object?> v)
        {
            foreach (var s in v.OfType<PyObjekt>())
            {
                if (s["ort"] is string ort && (p.Orte.Contains(ort) || ((ort == StadtOrt || IstBau(ort)) && s["vorher"] is PyObjekt && s["nachher"] is PyObjekt)))
                {
                    p._verlauf.Add(new Schritt(ort, Py.Str(s["was"]), PythonJson.Standard(s["vorher"]), PythonJson.Standard(s["nachher"])));
                }
            }
        }
        p._zeiger = Py.IstNurInt(d["zeiger"]) ? (int)Math.Clamp(Py.Int(d["zeiger"]), 0, p._verlauf.Count) : p._verlauf.Count;
        p.Nachricht = d["nachricht"] as string ?? "";
        if (d["ort"] is string o && p.Innenorte.Contains(o))
        {
            p.Ort = o;
        }
        return p;
    }

    /// <summary>Ein Teil der Stadt draussen, den es in der Basis nicht gibt, aber geben darf (neuer Schluessel in Karte oder Stadtdaten).</summary>
    private static bool GueltigerTeil(string pfad) =>
        pfad.Split('/', 2) is [var a, var k] && a is "karte" or "stadtdaten" && k.Length > 0;

    /// <summary>Name eines Projekts fuer die Anzeige (Feld "name" der Datei, sonst der Dateiname).</summary>
    public static string AnzeigeName(string datei)
    {
        try
        {
            using var lesen = new StreamReader(new FileStream(datei, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete), Encoding.UTF8);
            for (var i = 0; i < 8 && lesen.ReadLine() is { } zeile; i++)
            {
                if (zeile.StartsWith(" \"name\": ", StringComparison.Ordinal) && Py.Lesen(Encoding.UTF8.GetBytes(zeile[9..].TrimEnd(','))) is string n && NameGueltig(n))
                {
                    return n;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
        }
        return Path.GetFileNameWithoutExtension(datei);
    }

    /// <summary>Alle Projekte im Ordner (neueste zuerst).</summary>
    public static List<string> Liste(Werkzeugordner ablage) =>
        Directory.Exists(ablage.Projekte)
            ? [.. new DirectoryInfo(ablage.Projekte).GetFiles("*" + Endung).OrderByDescending(f => f.LastWriteTimeUtc).Select(f => f.FullName)]
            : [];

    // ---- Pruefung, Grenzen, Export -----------------------------------------------------------------

    /// <summary>Innenraeume der Basis mit allen geaenderten Grundrissen (flach geschrieben).</summary>
    public PyObjekt Innenraeume()
    {
        var innen = Basis.Innenraeume;
        foreach (var ort in GeaenderteOrte)
        {
            innen = InnenGrundriss.MitGrundriss(innen, ort, _grundrisse[ort]);
        }
        return innen;
    }

    public Innenraum Innenraum(string ort) => Lernplattform.Core.Spiel.Probe.Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), Grundriss(ort),
        Personen(ort), Daten.Grafik, Geschaefte.Laeden.Contains(ort) ? Daten.Geschaefte.Daten : null);

    /// <summary>Funde eines Innenraums (Regeln des gemeinsamen Teils plus Katalog-Stuecke, <see cref="KatalogPruefung"/>).</summary>
    public List<StadtbauFund> Pruefen(string ort) => PruefenMitGrenzen(ort).Funde;

    /// <summary>Funde und Grenzen eines Innenraums (Grenzen mit den echten Dreiecken der Katalog-Stuecke).</summary>
    public (List<StadtbauFund> Funde, (long Dreiecke, int Aufrufe) Grenzen) PruefenMitGrenzen(string ort) =>
        KatalogPruefung.Innen(Katalog.Aktiv, ort, Grundriss(ort), Personen(ort), Daten.Grafik, Daten.Geschaefte, Basis.Innenraeume);

    public (long Dreiecke, int Aufrufe) Grenzen(string ort) => PruefenMitGrenzen(ort).Grenzen;

    /// <summary>Die Stadt (draussen und alle Innenraeume) als Fassung, wie sie ein Export schreiben wuerde (Pruefung, 3D).</summary>
    public StadtFassung Stand(string erstellt = "") =>
        Aussen.Fassung(AussenStand, Basis, Innenraeume(), Leer is null ? Basis.Nummer + 1 : 1, Leer is null ? Basis.Nummer : null, Werkzeug.Kennzeile, erstellt,
            Nachricht.Length > 2000 ? Nachricht[..2000] : Nachricht);

    /// <summary>Pruefung der Stadt draussen (blockiert, rund eine halbe Sekunde; in der Oberflaeche im Hintergrund).</summary>
    public List<StadtbauFund> PruefenAussen() => PruefenAussen(Stand(), Daten, Aussen.Bauten(AussenStand));

    /// <summary>
    /// Pruefung draussen zu einem Stand. <paramref name="bauten"/>: Innenraeume neuer Gebaeude (Teil "bauten", steht nicht in
    /// der Fassung; fuer den gelben Hinweis, dass sie noch nicht ins Spiel kommen).
    /// </summary>
    public static List<StadtbauFund> PruefenAussen(StadtFassung stand, Spieldaten daten, PyObjekt? bauten = null)
    {
        var aussen = Aussen.Aus(stand);
        if (bauten is not null)
        {
            Py.Setzen(aussen, "bauten", Py.Kopie(bauten));
        }
        return [.. StadtbauPruefung.Aussen(stand, daten.Inhalt, daten.Grafik).Select(f => KatalogPruefung.Umbenannt(Katalog.Aktiv, f)), .. KatalogPruefung.Aussen(Katalog.Aktiv, aussen), .. Blueprint.Pruefen(aussen)];
    }

    /// <summary>Rote Funde aller geaenderten Orte und der Stadt draussen (Export gesperrt, solange es welche gibt).</summary>
    public List<(string Ort, StadtbauFund Fund)> RoteFunde()
    {
        var rot = GeaenderteOrte.Concat(Bauten).SelectMany(o => Pruefen(o).Where(f => f.Rot).Select(f => (o, f))).ToList();
        if (AussenGeaendert || Leer is not null)
        {
            rot.AddRange(PruefenAussen().Where(f => f.Rot).Select(f => (StadtOrt, f)));
        }
        return rot;
    }

    /// <summary>Name eines Orts fuer die Anzeige (Stadt draussen: "Stadt draußen").</summary>
    public string OrtName(string ort) => ort == StadtOrt ? "Stadt draußen"
        : IstBau(ort) ? Texte.BauName(Bau(ort)?["name"] as string ?? ort[BauPraefix.Length..])
        : Daten.OrtName(ort);

    /// <summary>
    /// Export-Datei (.fistadt) mit allen Aenderungen als neue Fassung (Nummer = Basis + 1, CC vergibt beim Einbau die
    /// naechste freie Nummer nur ueber eine neue Datei). Rot oder nichts geaendert: <see cref="StadtbauFehler"/>.
    /// Die fertige Datei durchlaeuft vor der Rueckgabe dieselbe Format- und Inhaltspruefung wie beim Einbau.
    /// </summary>
    public byte[] Exportieren(DateTimeOffset zeit)
    {
        if (GeaenderteOrte.Count == 0 && !AussenGeaendert)
        {
            throw new StadtbauFehler(["Es gibt noch keine Änderung zum Exportieren."]);
        }
        var rot = RoteFunde();
        if (rot.Count > 0)
        {
            throw new StadtbauFehler([.. rot.Select(r => OrtName(r.Ort) + ": " + r.Fund.Text)]);
        }
        var datei = Stand(zeit.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)).Schreiben();
        var gelesen = StadtFassung.Lesen(datei);
        var funde = StadtbauPruefung.Inhalt(gelesen, Daten.Inhalt);
        return funde.Count > 0 ? throw new StadtbauFehler(funde) : datei;
    }

    /// <summary>Dateiname eines Exports: stadt1_&lt;Projekt&gt;_&lt;JJJJ-MM-TT_HHMM&gt;.fistadt (ASCII, Leerzeichen als _).</summary>
    public string ExportName(DateTimeOffset zeit) =>
        Basis.StadtId + "_" + DateiName(Name).Replace(' ', '_') + "_" + zeit.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture) + StadtFassung.Endung;

    /// <summary>Export in den Ordner exporte/ des Werkzeugs schreiben (nie ueberschreiben: _2, _3, ...). Rueckgabe: Pfad.</summary>
    public string ExportSpeichern(DateTimeOffset zeit, string? ordner = null)
    {
        var daten = Exportieren(zeit);
        var ziel = ordner ?? Werkzeugordner.Exporte;
        Directory.CreateDirectory(ziel);
        var name = ExportName(zeit);
        var pfad = Path.Combine(ziel, name);
        for (var n = 2; File.Exists(pfad); n++)
        {
            pfad = Path.Combine(ziel, Path.GetFileNameWithoutExtension(name) + "_" + n.ToString(CultureInfo.InvariantCulture) + StadtFassung.Endung);
        }
        File.WriteAllBytes(pfad, daten);
        return pfad;
    }

    /// <summary>Ordner im Repo fi-fehlerberichte fuer "An CC senden" (Plan Abschnitt 5).</summary>
    public const string SendeOrdner = "stadtbau";

    /// <summary>Pfad im Repo: stadtbau/&lt;JJJJ-MM-TT&gt;_&lt;HHMMSS&gt;_&lt;PC|Tablet&gt;_S0.1.0[_n].fistadt.</summary>
    public static string SendeName(DateTimeOffset zeit, string geraet, int nummer) =>
        SendeOrdner + "/" + zeit.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + "_" + geraet + "_S" + Werkzeug.Version
        + (nummer > 1 ? "_" + nummer.ToString(CultureInfo.InvariantCulture) : "") + StadtFassung.Endung;

    /// <summary>"An CC senden": Export pruefen und ueber den Weg von "Bericht senden" ablegen (blockiert, im Hintergrund rufen).</summary>
    public VersandErgebnis Senden(DateTimeOffset zeit, string geraet) =>
        Senden(Exportieren(zeit), zeit, geraet, null);

    internal static VersandErgebnis Senden(byte[] datei, DateTimeOffset zeit, string geraet, Func<byte[], Func<int, string>, string, VersandErgebnis>? weg) =>
        (weg ?? BerichtVersand.Ablegen)(datei, n => SendeName(zeit, geraet, n), "Stadtbau " + Werkzeug.Anzeige + " " + geraet);
}

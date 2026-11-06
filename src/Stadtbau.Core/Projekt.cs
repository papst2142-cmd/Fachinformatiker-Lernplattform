using System.Globalization;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>Ein Schritt im Verlauf: Ort, was getan wurde, Grundriss vorher und nachher (JSON-Text).</summary>
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

    public const long FormatVersion = 1;

    public const string Endung = ".fisbau";

    public const int GespeicherteSchritte = 200;

    public static readonly string[] Felder = ["format", "formatversion", "werkzeug", "name", "stadt", "basis", "ort", "nachricht", "grundrisse", "verlauf", "zeiger"];

    private readonly List<Schritt> _verlauf = [];
    private readonly Dictionary<string, PyObjekt> _grundrisse = new(StringComparer.Ordinal);
    private int _zeiger;

    private Projekt(string name, Spieldaten daten, Werkzeugordner ablage)
    {
        Name = name;
        Daten = daten;
        Werkzeugordner = ablage;
        Ort = daten.Orte.FirstOrDefault() ?? "";
    }

    public string Name { get; }

    public Spieldaten Daten { get; }

    public Werkzeugordner Werkzeugordner { get; }

    /// <summary>Innenraum, der gerade bearbeitet wird.</summary>
    public string Ort { get; set; }

    /// <summary>Freiwillige Nachricht an CC (steht in der Export-Datei).</summary>
    public string Nachricht { get; set; } = "";

    /// <summary>Datei aus einer neueren Werkzeug-Version: nur lesen, nie speichern.</summary>
    public bool NurLesen { get; private set; }

    public string Datei => Path.Combine(Werkzeugordner.Projekte, Name + Endung);

    public int SchritteZurueck => _zeiger;

    public int SchritteVor => _verlauf.Count - _zeiger;

    public IReadOnlyList<Schritt> Verlauf => _verlauf;

    /// <summary>Orte, deren Grundriss sich gegenueber der Basis geaendert hat.</summary>
    public IReadOnlyList<string> GeaenderteOrte =>
        [.. Daten.Orte.Where(o => _grundrisse.TryGetValue(o, out var g) && PythonJson.Standard(g) != PythonJson.Standard(Original(o)))];

    /// <summary>Wird nach jeder Aenderung, jedem Rueckgaengig und Wiederholen gerufen (Oberflaeche zeichnet neu).</summary>
    public event Action? Geaendert;

    /// <summary>Gueltiger Projektname: Buchstaben, Ziffern, Leerzeichen, - und _, 1 bis 40 Zeichen.</summary>
    public static bool NameGueltig(string name) =>
        name.Trim().Length is > 0 and <= 40 && name.Trim() == name && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_');

    public static Projekt Neu(string name, Spieldaten daten, Werkzeugordner ablage, string? ort = null)
    {
        if (!NameGueltig(name))
        {
            throw new ArgumentException("Projektname ungültig", nameof(name));
        }
        var p = new Projekt(name, daten, ablage.Anlegen());
        if (ort is not null && daten.Orte.Contains(ort))
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
    public PyObjekt Original(string ort) => InnenGrundriss.Grundriss(Daten.Fassung.Innenraeume, ort);

    /// <summary>Der aktuelle Grundriss eines Orts (bearbeitet oder die Basis). Nur ueber <see cref="Aendern"/> veraendern.</summary>
    public PyObjekt Grundriss(string ort)
    {
        if (!_grundrisse.TryGetValue(ort, out var g))
        {
            g = Original(ort);
            _grundrisse[ort] = g;
        }
        return g;
    }

    public List<PyObjekt> Personen(string ort) => InnenGrundriss.Personen(Daten.Fassung.Innenraeume, ort);

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
        var s = _verlauf[--_zeiger];
        _grundrisse[s.Ort] = Lesen(s.Vorher);
        Ort = s.Ort;
        Gespeichert();
        return true;
    }

    public bool Wiederholen()
    {
        if (_zeiger >= _verlauf.Count || NurLesen)
        {
            return false;
        }
        var s = _verlauf[_zeiger++];
        _grundrisse[s.Ort] = Lesen(s.Nachher);
        Ort = s.Ort;
        Gespeichert();
        return true;
    }

    private static PyObjekt Lesen(string text) => (PyObjekt)Py.Lesen(Encoding.UTF8.GetBytes(text))!;

    private DateTime _letzteSicherung = DateTime.MinValue;

    /// <summary>Spanne, nach der das automatische Speichern zusaetzlich eine Sicherung anlegt.</summary>
    public static readonly TimeSpan SicherungAlle = TimeSpan.FromMinutes(5);

    /// <summary>Uhr fuer Tests.</summary>
    internal Func<DateTime> Uhr { get; set; } = () => DateTime.Now;

    private void Gespeichert()
    {
        Speichern(vonHand: false);
        Geaendert?.Invoke();
    }

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
        var daten = Schreiben();
        Werkzeugordner.SicherSchreiben(Datei, daten);
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
        foreach (var ort in Daten.Orte.Where(_grundrisse.ContainsKey))
        {
            grundrisse.Add(ort, _grundrisse[ort]);
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
            ("name", PythonJson.Standard(Name)), ("stadt", PythonJson.Standard(Daten.Fassung.StadtId)), ("basis", PythonJson.Standard(Daten.Fassung.Nummer)),
            ("ort", PythonJson.Standard(Ort)), ("nachricht", PythonJson.Standard(Nachricht)), ("grundrisse", PythonJson.Standard(grundrisse)),
            ("verlauf", verlauf.Append(']').ToString()), ("zeiger", PythonJson.Standard((long)Math.Max(0, _zeiger - start))),
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
        var fehlend = Felder.Where(f => !Py.Hat(d, f)).ToList();
        if (!neuer && fehlend.Count > 0)
        {
            throw new StadtbauFehler([.. fehlend.Select(f => $"Feld „{f}“ fehlt.")]);
        }
        if (!Py.Gleich(d["stadt"], daten.Fassung.StadtId) || !Py.IstNurInt(d["basis"]) || Py.Int(d["basis"]) != daten.Fassung.Nummer)
        {
            throw new StadtbauFehler([string.Create(CultureInfo.InvariantCulture,
                $"Das Projekt baut auf {Py.Str(d["stadt"])} Fassung {Py.Str(d["basis"])} auf; dieses Werkzeug kennt {daten.Fassung.StadtId} Fassung {daten.Fassung.Nummer}.")]);
        }
        var name = Path.GetFileNameWithoutExtension(datei);
        var p = new Projekt(Py.Str(d["name"]) is { Length: > 0 } n && NameGueltig(n) ? n : name, daten, ablage) { NurLesen = neuer };
        if (d["grundrisse"] is PyObjekt g)
        {
            foreach (var (ort, grundriss) in g)
            {
                if (daten.Orte.Contains(ort) && grundriss is PyObjekt go)
                {
                    p._grundrisse[ort] = go;
                }
            }
        }
        if (d["verlauf"] is List<object?> v)
        {
            foreach (var s in v.OfType<PyObjekt>())
            {
                if (s["ort"] is string ort && daten.Orte.Contains(ort))
                {
                    p._verlauf.Add(new Schritt(ort, Py.Str(s["was"]), PythonJson.Standard(s["vorher"]), PythonJson.Standard(s["nachher"])));
                }
            }
        }
        p._zeiger = Py.IstNurInt(d["zeiger"]) ? (int)Math.Clamp(Py.Int(d["zeiger"]), 0, p._verlauf.Count) : p._verlauf.Count;
        p.Nachricht = d["nachricht"] as string ?? "";
        if (d["ort"] is string o && daten.Orte.Contains(o))
        {
            p.Ort = o;
        }
        return p;
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
        var innen = Daten.Fassung.Innenraeume;
        foreach (var ort in GeaenderteOrte)
        {
            innen = InnenGrundriss.MitGrundriss(innen, ort, _grundrisse[ort]);
        }
        return innen;
    }

    public Innenraum Innenraum(string ort) => Lernplattform.Core.Spiel.Probe.Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), Grundriss(ort),
        Personen(ort), Daten.Grafik, Geschaefte.Laeden.Contains(ort) ? Daten.Geschaefte.Daten : null);

    public List<StadtbauFund> Pruefen(string ort) =>
        StadtbauPruefung.Innen(ort, Grundriss(ort), Personen(ort), Daten.Grafik, Daten.Geschaefte, Daten.Fassung.Innenraeume);

    public (long Dreiecke, int Aufrufe) Grenzen(string ort) => StadtbauGrenzen.Innen(Innenraum(ort));

    /// <summary>Rote Funde aller geaenderten Orte (Export gesperrt, solange es welche gibt).</summary>
    public List<(string Ort, StadtbauFund Fund)> RoteFunde() =>
        [.. GeaenderteOrte.SelectMany(o => Pruefen(o).Where(f => f.Rot).Select(f => (o, f)))];

    /// <summary>
    /// Export-Datei (.fistadt) mit allen Aenderungen als neue Fassung (Nummer = Basis + 1, CC vergibt beim Einbau die
    /// naechste freie Nummer nur ueber eine neue Datei). Rot oder nichts geaendert: <see cref="StadtbauFehler"/>.
    /// Die fertige Datei durchlaeuft vor der Rueckgabe dieselbe Format- und Inhaltspruefung wie beim Einbau.
    /// </summary>
    public byte[] Exportieren(DateTimeOffset zeit)
    {
        if (GeaenderteOrte.Count == 0)
        {
            throw new StadtbauFehler(["Es gibt noch keine Änderung zum Exportieren."]);
        }
        var rot = RoteFunde();
        if (rot.Count > 0)
        {
            throw new StadtbauFehler([.. rot.Select(r => Daten.OrtName(r.Ort) + ": " + r.Fund.Text)]);
        }
        var f = Daten.Fassung;
        var neu = new StadtFassung
        {
            StadtId = f.StadtId,
            Nummer = f.Nummer + 1,
            Basis = f.Nummer,
            Werkzeug = Werkzeug.Kennzeile,
            Erstellt = zeit.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            Nachricht = Nachricht.Length > 2000 ? Nachricht[..2000] : Nachricht,
            Karte = f.Karte,
            Stadtdaten = f.Stadtdaten,
            Innenraeume = Innenraeume(),
            Laternen = f.Laternen,
            Beete = f.Beete,
        };
        var datei = neu.Schreiben();
        var gelesen = StadtFassung.Lesen(datei);
        var funde = StadtbauPruefung.Inhalt(gelesen, Daten.Inhalt);
        return funde.Count > 0 ? throw new StadtbauFehler(funde) : datei;
    }

    /// <summary>Dateiname eines Exports: stadt1_&lt;Projekt&gt;_&lt;JJJJ-MM-TT_HHMM&gt;.fistadt (Leerzeichen als _).</summary>
    public string ExportName(DateTimeOffset zeit) =>
        Daten.Fassung.StadtId + "_" + Name.Replace(' ', '_') + "_" + zeit.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture) + StadtFassung.Endung;

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

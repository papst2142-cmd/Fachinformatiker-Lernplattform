using System.Numerics;
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
public sealed partial class Projekt
{
    public const string Format = "fisbau";

    /// <summary>
    /// 3 seit Welle 4c (Teil "bauten" in "aussen": Innenraeume gesetzter Blueprints, Meldung P12-23 Frage 1 a; ein Werkzeug S 0.4.0
    /// oeffnet die Datei damit nur zum Lesen), 2 seit Welle 3 (Felder "aussen" und "leer"). Dateien mit Formatnummer 1 und 2
    /// lassen sich weiter oeffnen. 4 seit Welle E4 (S 0.53.0, Weisung P12-247): nur eine Datei, in der eine Quest einen
    /// Arbeitsort hat (im Stand oder im Verlauf), bekommt 4 (<see cref="FormatFuer"/>); jede andere bleibt bei 3 und bitgleich.
    /// </summary>
    public const long FormatVersion = 4;

    /// <summary>
    /// Formatnummer beim Schreiben: 4, wenn der Text der Stadt draussen oder des Verlaufs das Feld "arbeitsort" als Schluessel
    /// enthaelt, sonst 3. Ein Schluessel steht im JSON-Text als <c>"arbeitsort": </c>; derselbe Wortlaut in einem Text (Titel,
    /// Frage) steht dort maskiert (<c>\"arbeitsort\": </c>) und zaehlt darum nicht.
    /// </summary>
    internal static long FormatFuer(string aussenText, string verlaufText) =>
        aussenText.Contains(ArbeitsortSchluessel, StringComparison.Ordinal) || verlaufText.Contains(ArbeitsortSchluessel, StringComparison.Ordinal) ? 4 : 3;

    private const string ArbeitsortSchluessel = "\"" + Questebene.FeldArbeitsort + "\": ";

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

    private PyObjekt? Bau(string ort)
    {
        ort = Etagen.Teilen(ort).Ort;
        return IstBau(ort) ? Aussen.Bauten(AussenStand)[ort[BauPraefix.Length..]] as PyObjekt : null;
    }

    /// <summary>
    /// Phase 13 Welle 5: gibt es den Innenraum (auch eine Etage „ort@k“ eines Orts aus <see cref="Innenorte"/>)?
    /// </summary>
    public bool GibtInnenort(string schluessel)
    {
        var (ort, etage) = Etagen.Teilen(schluessel);
        return Innenorte.Contains(ort) && (etage == 0 || Etagen.Gibt(Grundriss(ort), etage));
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
        var (basis, etage) = Etagen.Teilen(ort);
        if (etage > 0)
        {
            // Phase 13 Welle 5: eine Etage steht im Grundriss des Erdgeschosses (Aenderungen gehen dorthin)
            return Etagen.Gebaeude(Grundriss(basis), etage);
        }
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

    public List<PyObjekt> Personen(string ort) => Etagen.Teilen(ort).Etage > 0 ? [] : IstBau(ort)
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

    /// <summary>Ist die Stadt draussen geaendert? Notizen zaehlen nicht (S 0.27.0, Plan P12-79: nur Planung, nie in der Stadt-Datei).</summary>
    public bool AussenGeaendert => GeaenderteTeile.Any(t => t != AussenBearbeitung.NotizTeil);

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
        var strassenVorher = LaternenAutomatik.Strassen(a);
        var ergebnis = aenderung(a);
        // S 0.26.0 (Weisung P12-115, Nico 2a): automatische Laternen im selben Schritt (geloeschte merken, an geaenderten
        // Strassen neu verteilen)
        LaternenNachziehen(a, vorher, strassenVorher);
        // S 0.31.0 (Sammelliste 264): Farben eines geloeschten Gebaeudes fallen im selben Schritt weg
        AussenBearbeitung.FarbenAufraeumen(a);
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

    /// <summary>
    /// S 0.26.0: die Stadt draussen <paramref name="aussen"/> (ohne Laternen) als Szene des Spiels mit dem Probestand, fuer
    /// die Regeln der automatischen Laternen (null = laesst sich nicht bauen, dann keine automatischen).
    /// </summary>
    private readonly StadtSzene.Vorrat _laternenVorrat = new();

    /// <summary>Automatische Laternen nach einer Aenderung von <paramref name="a"/> (vorher: Teile als Text und Strassen).</summary>
    private void LaternenNachziehen(PyObjekt a, Dictionary<string, string> vorher, List<List<Vector2>> strassenVorher)
    {
        if (LaternenAutomatik.AusFuerTests)
        {
            return;
        }
        LaternenAutomatik.Abgleichen(a);
        var zwischen = Aussen.TeileText(a);
        var sonst = zwischen.Keys.Union(vorher.Keys).Any(k => k is not ("laternen" or "bauten" or AussenBearbeitung.NotizTeil or QuestTeil or QuestgeberTeil) && k != LaternenAutomatik.Teil
            && (!vorher.TryGetValue(k, out var v) || !zwischen.TryGetValue(k, out var z) || v != z));
        LaternenAutomatik.NachAenderung(a, strassenVorher, SzeneOhneLaternen, sonst);
    }

    /// <summary>
    /// Fuer die Vorschau (3D-Schatten, Aussehen): merkt sich den Stand jetzt und gibt eine Kopie eines geaenderten Stands mit
    /// denselben automatischen Laternen, die der echte Schritt aufstellen wuerde (darf im Hintergrund laufen).
    /// </summary>
    public Func<PyObjekt, PyObjekt> LaternenFuerVorschau()
    {
        var a = AussenStand;
        var vorher = Aussen.TeileText(a);
        var strassen = LaternenAutomatik.Strassen(a);
        return nachher =>
        {
            var k = (PyObjekt)Py.Kopie(nachher)!;
            LaternenNachziehen(k, vorher, strassen);
            return k;
        };
    }

    public StadtSzene? SzeneOhneLaternen(PyObjekt aussen) => SzeneOhneLaternen(aussen, false);

    /// <summary>Wie <see cref="SzeneOhneLaternen(PyObjekt)"/>; <paramref name="mitGebaeuden"/>: mit den eigenen Gebaeuden (Fund 262, Tuer einrasten).</summary>
    public StadtSzene? SzeneOhneLaternen(PyObjekt aussen, bool mitGebaeuden)
    {
        try
        {
            using var nachsicht = Nachsicht.Werkzeugansicht();
            var fassung = Aussen.Fassung(aussen, Basis, Innenraeume(), 1, null, Werkzeug.Kennzeile, "", "");
            var stadt = fassung.StadtFuer(fassung.MitKarte(Daten.Inhalt));
            var stand = StadtbauSpiel.Probestand(stadt.Inhalt).Stand;
            return StadtSzene.Bauen(stadt, stand, mitGebaeuden ? Ansicht3D.MitGebaeuden(Daten.Grafik, fassung) : Daten.Grafik, null, Daten.Feste, vorrat: _laternenVorrat);
        }
        catch (Exception fehler) when (fehler is InvalidDataException or IOException or KeyNotFoundException or InvalidCastException or FormatException
            or OverflowException or SpielFehler or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Knopf „Laternen neu verteilen“ (ein Schritt): Ergebnis aufgestellt, entfernt.</summary>
    public (int Dazu, int Weg) LaternenVerteilen() => AussenAendern(Texte.LaternenVerteilen, a => LaternenAutomatik.AlleVerteilen(a, SzeneOhneLaternen));

    /// <summary>Sammelliste 296: Zahlen fuer die Rueckfrage vor „Laternen neu verteilen“ (aendert nichts).</summary>
    public (int Verschwinden, int Neu) LaternenVorherSagen() => LaternenAutomatik.VorherSagen(AussenStand, SzeneOhneLaternen);

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
        var etage = Etagen.Teilen(ort).Etage;
        return EtagenAendern(ort, was, (eg, _) => aenderung(Etagen.Gebaeude(eg, etage)));
    }

    /// <summary>
    /// Phase 13 Welle 5: Aenderung am ganzen Haus des Innenraums <paramref name="ort"/> (auch „ort@k“): die Aenderung bekommt
    /// den Grundriss des Erdgeschosses und die Etage; danach gleicht <see cref="EtagenBearbeitung.Aufraeumen"/> Treppen und
    /// Fahrstuehle ab. Ein Schritt im Verlauf mit dem Schluessel der Etage (Rueckgaengig zeigt sie wieder).
    /// </summary>
    public T EtagenAendern<T>(string ort, string was, Func<PyObjekt, int, T> aenderung)
    {
        if (NurLesen)
        {
            throw new InvalidOperationException("Projekt ist nur zum Lesen geöffnet.");
        }
        var (basis, etage) = Etagen.Teilen(ort);
        T Mit(PyObjekt eg)
        {
            var e = aenderung(eg, etage);
            EtagenBearbeitung.Aufraeumen(eg);
            return e;
        }
        if (IstBau(basis))
        {
            // Innenraum eines neuen Gebaeudes: liegt in der Stadt draussen (Teil "bauten"), der Schritt merkt sich den Innenraum
            return AussenAendern(ort, was, a => Mit(Grundriss(basis)));
        }
        var g = Grundriss(basis);
        var vorher = PythonJson.Standard(g);
        var ergebnis = Mit(g);
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

    public void EtagenAendern(string ort, string was, Action<PyObjekt, int> aenderung) => EtagenAendern(ort, was, (eg, k) =>
    {
        aenderung(eg, k);
        return 0;
    });

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
                LetzterSchrittDraussen = !Bauten.Contains(Etagen.Teilen(s.Ort).Ort);
                if (!LetzterSchrittDraussen)
                {
                    Ort = GibtInnenort(s.Ort) ? s.Ort : Etagen.Teilen(s.Ort).Ort;
                }
            }
            return;
        }
        var basis = Etagen.Teilen(s.Ort).Ort;
        _grundrisse[basis] = Lesen(text);
        // Phase 13 Welle 5: Schritt auf einer Etage zeigt die Etage (gibt es sie danach nicht mehr: das Erdgeschoss)
        Ort = GibtInnenort(s.Ort) ? s.Ort : basis;
    }

    private static PyObjekt Lesen(string text) => (PyObjekt)Py.Lesen(Encoding.UTF8.GetBytes(text))!;

    private DateTime _letzteSicherung = DateTime.MinValue;

    /// <summary>Spanne, nach der das automatische Speichern zusaetzlich eine Sicherung anlegt.</summary>
    public static readonly TimeSpan SicherungAlle = TimeSpan.FromMinutes(5);

    /// <summary>Uhr fuer Tests.</summary>
    internal Func<DateTime> Uhr { get; set; } = () => DateTime.Now;

    private void Gespeichert()
    {
        SpielortInnenAbgleichen();
        _aenderungen++;
        SpeicherFehler = null;
        try
        {
            Speichern(vonHand: false);
        }
        catch (Exception e) when (!SpeichernAltFuerTests && e is IOException or UnauthorizedAccessException)
        {
            // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 5): scheitert das automatische Speichern (Datei kurz gesperrt,
            // etwa durch einen Virenscanner oder das Hochladen in eine Wolke), steht der Schritt trotzdem im Verlauf.
            // Vorher lief der Fehler bis zur Oberflaeche, "Geaendert" kam nie an, und "Rueckgaengig" blieb grau, bis die
            // Ansicht wechselte. Jetzt meldet die Oberflaeche den Fehler, der naechste Schritt versucht es wieder, und
            // "Werkzeug beenden" fragt nach (Ungespeichert).
            SpeicherFehler = e.Message;
            Werkzeugordner.Fehler("Automatisch speichern: " + e.GetType().Name + ": " + e.Message);
        }
        Geaendert?.Invoke();
    }

    /// <summary>
    /// Welle S 0.45.0 (Weisung P12-212 Punkt 4): eine leere Stadt mit Spielorten bekommt fuer jeden Spielort den Innenraum der
    /// Stadt 1 ueber die Kennung (<see cref="Spielort.InnenFuer"/>) als Basis; dort laesst er sich bearbeiten wie in Stadt 1.
    /// Nach jedem Schritt, Rueckgaengig und Oeffnen neu (die Basis einer leeren Stadt gehoert nur diesem Projekt).
    /// </summary>
    private void SpielortInnenAbgleichen()
    {
        if (Leer is null)
        {
            return;
        }
        var soll = Spielort.InnenFuer(AussenStand, Daten.Fassung.Innenraeume);
        var innen = Basis.Innenraeume;
        if (PythonJson.Standard(innen) == PythonJson.Standard(soll))
        {
            return;
        }
        innen.Clear();
        innen.AddRange(soll);
    }

    /// <summary>Fehler des letzten automatischen Speicherns (null = gespeichert).</summary>
    public string? SpeicherFehler { get; private set; }

    /// <summary>Gegenprobe (Start-Test, Tests): Speicherfehler laufen wie vor der Nachbesserung bis zur Oberflaeche.</summary>
    public static bool SpeichernAltFuerTests { get; set; }

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
    public void Speichern(bool vonHand) => Speichern(vonHand, ohneSicherung: false);

    /// <summary>
    /// Wie <see cref="Speichern(bool)"/>; <paramref name="ohneSicherung"/> schreibt nur die Projektdatei (S 0.23.0: vor dem
    /// Loeschen, damit keine der 20 Sicherungen ersetzt wird).
    /// </summary>
    public void Speichern(bool vonHand, bool ohneSicherung)
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
        if (!ohneSicherung && (vonHand || jetzt - _letzteSicherung >= SicherungAlle))
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
        var aussenText = aussen.Append('}').ToString();
        var verlaufText = verlauf.Append(']').ToString();
        var objekt = new List<(string, string)>
        {
            ("format", PythonJson.Standard(Format)), ("formatversion", PythonJson.Standard(FormatFuer(aussenText, verlaufText))), ("werkzeug", PythonJson.Standard(Werkzeug.Kennzeile)),
            ("name", PythonJson.Standard(Name)), ("stadt", PythonJson.Standard(Basis.StadtId)), ("basis", PythonJson.Standard(Leer is null ? Basis.Nummer : null)),
            ("leer", PythonJson.Standard(leer)), ("ort", PythonJson.Standard(Ort)), ("nachricht", PythonJson.Standard(Nachricht)), ("grundrisse", PythonJson.Standard(grundrisse)),
            ("aussen", aussenText), ("verlauf", verlaufText), ("zeiger", PythonJson.Standard((long)Math.Max(0, _zeiger - start))),
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
        p.SpielortInnenAbgleichen();
        if (d["ort"] is string o && p.Innenorte.Contains(o))
        {
            p.Ort = o;
        }
        return p;
    }

    /// <summary>
    /// S 0.25.1 (Weisung P12-108 Punkt 2, Sammelliste 251): eine Stadt-Datei (.fistadt, Export oder „An CC senden“) als NEUES
    /// Projekt oeffnen. Stadt draussen = die Datei; Innenraeume, die sich gegenueber der Basis unterscheiden, kommen als
    /// geaenderte Grundrisse dazu. Leere Stadt (Kennung „neu_…“, ohne Basis): leeres Projekt mit der Groesse der Karte
    /// (frei, wenn die Datei kein Kleingruen hat). Nie ueberschrieben: gibt es den Namen schon, ist das ein Fehler
    /// (<see cref="NameAusStadtDatei"/> sucht einen freien). Innenraeume neuer Gebaeude stehen nicht in der Datei.
    /// </summary>
    /// <summary>Nur Gegenprobe: Stadt-Datei ohne die Innenraeume oeffnen (die Aenderung im Innenraum fehlt dann).</summary>
    internal static readonly AsyncLocal<bool> StadtDateiOhneInnenFuerTests = new();

    public static Projekt AusStadtDatei(byte[] datei, Spieldaten daten, Werkzeugordner ablage, string name)
    {
        if (!NameGueltig(name))
        {
            throw new ArgumentException("Projektname ungültig", nameof(name));
        }
        var f = StadtFassung.Lesen(datei);
        (double, double)? leer = null;
        var frei = false;
        if (f.StadtId.StartsWith("neu_", StringComparison.Ordinal) && f.Basis is null)
        {
            if ((Py.Hat(f.Karte, "welt") ? f.Karte["welt"] : null) is not PyObjekt welt || welt["breite"] is not (long or double) || welt["hoehe"] is not (long or double)
                || !Aussen.GroesseGueltig(Py.F(welt["breite"]), Py.F(welt["hoehe"])))
            {
                throw new StadtbauFehler([Texte.StadtDateiGroesse]);
            }
            leer = (Py.F(welt["breite"]), Py.F(welt["hoehe"]));
            frei = (Py.Hat(f.Stadtdaten, "darstellung") ? f.Stadtdaten["darstellung"] : null) is PyObjekt dar && Py.Hat(dar, "kleingruen") && dar["kleingruen"] is false;
        }
        else if (f.StadtId != daten.Fassung.StadtId || f.Basis != daten.Fassung.Nummer)
        {
            throw new StadtbauFehler([Texte.StadtDateiFremd(f.StadtId, f.Basis, daten.Fassung.StadtId, daten.Fassung.Nummer)]);
        }
        var p = new Projekt(name, daten, ablage.Anlegen(), leer, frei);
        if (File.Exists(p.Datei))
        {
            throw new IOException("Ein Projekt mit diesem Namen gibt es schon.");
        }
        p._aussen = Aussen.Aus(f);
        // W4b (Weisung P12-228 Punkt 2): eine Stadt-Datei wird als neue Stadt mit 1 Minute je Schritt geoeffnet
        Aussen.UhrVorgabeSetzen(p._aussen);
        p.SpielortInnenAbgleichen();
        foreach (var ort in StadtDateiOhneInnenFuerTests.Value ? [] : p.Orte)
        {
            var g = InnenGrundriss.Grundriss(f.Innenraeume, ort);
            if (PythonJson.Standard(g) != PythonJson.Standard(p.Original(ort)))
            {
                p._grundrisse[ort] = g;
            }
        }
        p.Nachricht = f.Nachricht;
        p.Speichern(vonHand: true);
        return p;
    }

    /// <summary>
    /// Freier Projektname fuer eine Stadt-Datei: aus dem Dateinamen eines Exports „&lt;stadt&gt;_&lt;Projekt&gt;_&lt;Datum&gt;“
    /// der Projektteil (_ als Leerzeichen), sonst „Stadt aus Datei“; gibt es ihn schon: „… 2“, „… 3“ ...
    /// </summary>
    public static string NameAusStadtDatei(string dateiname, string stadtId, Werkzeugordner ablage)
    {
        var stamm = Path.GetFileNameWithoutExtension(dateiname);
        var m = System.Text.RegularExpressions.Regex.Match(stamm, @"^" + System.Text.RegularExpressions.Regex.Escape(stadtId) + @"_(.+)_\d{4}-\d{2}-\d{2}_\d{4}(_\d+)?$");
        var name = m.Success ? m.Groups[1].Value.Replace('_', ' ').Trim() : "";
        if (!NameGueltig(name) || name.Length > 36)
        {
            name = Texte.StadtDateiName;
        }
        var kandidat = name;
        for (var n = 2; File.Exists(Path.Combine(ablage.Projekte, DateiName(kandidat) + Endung)); n++)
        {
            kandidat = name + " " + n.ToString(CultureInfo.InvariantCulture);
        }
        return kandidat;
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

    /// <summary>Name der Kopie, die vor dem Loeschen in die Sicherungen des Projekts kommt (eigener Name, kein Platz der 20).</summary>
    public static string LoeschKopieName(DateTime zeit) =>
        "geloescht_" + zeit.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + Endung;

    /// <summary>
    /// Welle S 0.23.0 (Weisung P12-96, Sammelliste 242, Nico: angefangene Projekte loeschen): die Projektdatei
    /// <paramref name="datei"/> loeschen. Vorher kommt sie unveraendert als <see cref="LoeschKopieName"/> in den
    /// Sicherungsordner des Projekts (<c>sicherungen/&lt;Name&gt;/</c>, gleicher Ordner wie die 20 Sicherungen, die bleiben
    /// unangetastet; gibt es den Namen schon, folgt _2, _3, ...). Die Kopie wird zurueckgelesen; scheitert sie oder weicht
    /// sie ab, bleibt die Datei (IOException). Die Datei wird roh gelesen: kaputte und neuere Projekte lassen sich auch
    /// loeschen. Nichts ausserhalb von projekte/ und sicherungen/&lt;Name&gt;/ wird beruehrt (Blueprints, Exporte bleiben).
    /// Rueckgabe: Pfad der Kopie.
    /// </summary>
    public static string Loeschen(Werkzeugordner ablage, string datei, DateTime zeit)
    {
        var voll = Path.GetFullPath(datei);
        if (!string.Equals(Path.GetDirectoryName(voll), Path.GetFullPath(ablage.Projekte), StringComparison.Ordinal)
            || !voll.EndsWith(Endung, StringComparison.Ordinal))
        {
            throw new IOException("Nur Projektdateien im Ordner „projekte“ lassen sich löschen.");
        }
        var daten = File.ReadAllBytes(voll);
        var ordner = new Sicherungen(ablage, AnzeigeName(voll)).Ordner;
        Directory.CreateDirectory(ordner);
        var kopie = Path.Combine(ordner, LoeschKopieName(zeit));
        for (var n = 2; File.Exists(kopie); n++)
        {
            kopie = Path.Combine(ordner, Path.GetFileNameWithoutExtension(LoeschKopieName(zeit)) + "_" + n.ToString(CultureInfo.InvariantCulture) + Endung);
        }
        Werkzeugordner.SicherSchreiben(kopie, daten);
        if (!File.ReadAllBytes(kopie).AsSpan().SequenceEqual(daten))
        {
            throw new IOException("Die Kopie in den Sicherungen weicht ab, das Projekt bleibt.");
        }
        File.Delete(voll);
        return kopie;
    }

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

    public Innenraum Innenraum(string ort) => Lernplattform.Core.Spiel.Probe.Innenraum.AusGrundrissMitPersonen(
        StadtbauPruefung.Ansicht(Etagen.Teilen(ort).Ort, Etagen.Teilen(ort).Etage), Grundriss(ort),
        Personen(ort), Daten.Grafik, Geschaefte.Laeden.Contains(Etagen.Teilen(ort).Ort) ? Daten.Geschaefte.Daten : null);

    /// <summary>Funde eines Innenraums (Regeln des gemeinsamen Teils plus Katalog-Stuecke, <see cref="KatalogPruefung"/>).</summary>
    public List<StadtbauFund> Pruefen(string ort) => PruefenMitGrenzen(ort).Funde;

    /// <summary>Funde und Grenzen eines Innenraums (Grenzen mit den echten Dreiecken der Katalog-Stuecke).</summary>
    /// <remarks>
    /// Phase 13 Welle 5: <paramref name="ort"/> darf eine Etage „ort@k“ sein; dazu kommen die Funde der Etagen-Pruefung
    /// (<see cref="StadtbauPruefung.Etagen"/>) fuer diese Etage (Funde am ganzen Haus stehen beim Erdgeschoss).
    /// </remarks>
    public (List<StadtbauFund> Funde, (long Dreiecke, int Aufrufe) Grenzen) PruefenMitGrenzen(string ort)
    {
        // S 0.49.0 (Sammelliste 328): dieselbe Rechnung wie die Liste aller Innenraeume (InnenFunde)
        var a = InnenFunde.Vorbereiten(this, [ort]);
        return InnenFunde.PruefenMitGrenzen(a, a.Raeume[0]);
    }

    /// <summary>
    /// S 0.19.0: Funde zwischen dem Entwurf (Teile in <c>stadtdaten.bauteile</c>) und den Etagen eines Gebaeudes ohne Ort
    /// (<see cref="EtagenAusEntwurf.Pruefen"/>); leer fuer Orte und Gebaeude ohne Entwurf.
    /// </summary>
    public List<EtagenFund> EtagenEntwurfFunde(string basis) =>
        IstBau(basis) && Bau(basis) is not null && EtagenAusEntwurf.AusStadt(AussenStand, basis[BauPraefix.Length..]) is { } e
            ? EtagenAusEntwurf.Pruefen(Grundriss(basis), e.Teile, e.Eingaenge)
            : [];

    /// <summary>Hat das Gebaeude <paramref name="basis"/> einen Entwurf (dann gibt es „Vorschlag neu“)?</summary>
    public bool HatEntwurf(string basis) => IstBau(basis) && Bau(basis) is not null && EtagenAusEntwurf.AusStadt(AussenStand, basis[BauPraefix.Length..]) is not null;

    /// <summary>
    /// „Vorschlag neu“ (Nico 3a): Etagen und Treppenhaus neu aus dem Entwurf, das Erdgeschoss bleibt. Ein Schritt im Verlauf.
    /// Rueckgabe: Zahl der Etagen (-1 = kein Entwurf).
    /// </summary>
    public int EtagenVorschlagNeu(string basis, string was)
    {
        if (!HatEntwurf(basis))
        {
            return -1;
        }
        var e = EtagenAusEntwurf.AusStadt(AussenStand, basis[BauPraefix.Length..])!.Value;
        return EtagenAendern(basis, was, (eg, _) => EtagenAusEntwurf.Neu(eg, e.Teile, e.Eingaenge));
    }

    /// <summary>Schluessel aller Etagen eines Orts (Erdgeschoss zuerst).</summary>
    public List<string> EtagenVon(string ort)
    {
        var eg = Grundriss(ort);
        return [.. Enumerable.Range(0, Etagen.Anzahl(eg) + 1).Where(k => Etagen.Gibt(eg, k)).Select(k => Etagen.Schluessel(ort, k))];
    }

    public (long Dreiecke, int Aufrufe) Grenzen(string ort) => PruefenMitGrenzen(ort).Grenzen;

    /// <summary>Die Stadt (draussen und alle Innenraeume) als Fassung, wie sie ein Export schreiben wuerde (Pruefung, 3D).</summary>
    public StadtFassung Stand(string erstellt = "") =>
        Aussen.Fassung(AussenStand, Basis, Innenraeume(), Leer is null ? Basis.Nummer + 1 : 1, Leer is null ? Basis.Nummer : null, Werkzeug.Kennzeile, erstellt,
            Nachricht.Length > 2000 ? Nachricht[..2000] : Nachricht);

    /// <summary>Pruefung der Stadt draussen (blockiert, rund eine halbe Sekunde; in der Oberflaeche im Hintergrund).</summary>
    /// <remarks>Welle Q1 (S 0.40.0): dazu die Funde der Quests und Questgeber (<see cref="QuestFunde"/>).</remarks>
    public List<StadtbauFund> PruefenAussen() => [.. PruefenAussen(Stand(), Daten, Aussen.Bauten(AussenStand)), .. QuestFunde().Select(f => f.FuerStadt)];

    /// <summary>
    /// Pruefung draussen zu einem Stand. <paramref name="bauten"/>: Innenraeume neuer Gebaeude (Teil "bauten", steht nicht in
    /// der Fassung; fuer den gelben Hinweis, dass sie noch nicht ins Spiel kommen). <paramref name="wege"/>: schon gerechnete
    /// Laufzeiten (P12-221: die Oberflaeche rechnet sie einmal fuer Karte und Hinweis).
    /// </summary>
    public static List<StadtbauFund> PruefenAussen(StadtFassung stand, Spieldaten daten, PyObjekt? bauten = null, List<Laufzeiten.Weg>? wege = null)
    {
        var aussen = Aussen.Aus(stand);
        if (bauten is not null)
        {
            Py.Setzen(aussen, "bauten", Py.Kopie(bauten));
        }
        // Welle Q (Sammelliste 229): Gebaeude ohne Ort mit dem Namen des Blueprints statt „bau:…“
        // Fund 262: Szene mit den eigenen Gebaeuden (wie die 3D-Ansicht), dazu die Regel „Tuer buendig“ (nur Werkzeug)
        var funde = StadtbauPruefung.Aussen(stand, daten.Inhalt, Ansicht3D.MitGebaeuden(daten.Grafik, stand), null, sz => TuerBuendig.Pruefen(sz, stand, daten));
        // Fund 320 (Weisung P12-226): Orte mit dem Namen der Karte statt dem Namen im Probestand („Probe“, „filiale“)
        if (funde.Count > 0 && ProbeNamen.Ersatz(stand, daten.Inhalt) is { Count: > 0 } ersatz)
        {
            funde = [.. funde.Select(f => ProbeNamen.Umbenennen(ersatz, f))];
        }
        return [.. funde.Where(f => !GebaeudeTuer.NurGelb(aussen, f)).Select(f => Blueprint.MitNamen(aussen, KatalogPruefung.Umbenannt(Katalog.Aktiv, f))), .. KatalogPruefung.Aussen(Katalog.Aktiv, aussen).Select(f => Blueprint.MitNamen(aussen, f)), .. Blueprint.Pruefen(aussen), .. GebaeudeTuer.Pruefen(aussen), .. TuerFrei.Pruefen(aussen),
            .. (stand.StadtId == daten.Fassung.StadtId ? [] : Spielort.Pruefen(aussen, daten.Fassung)), .. Laufzeiten.Pruefen(wege ?? Laufzeiten.Rechnen(stand, daten.Inhalt, daten.Grafik), aussen)];
    }

    /// <summary>Rote Funde aller geaenderten Orte und der Stadt draussen (Export gesperrt, solange es welche gibt).</summary>
    public List<(string Ort, StadtbauFund Fund)> RoteFunde()
    {
        // S 0.49.0 (Sammelliste 328): derselbe Weg wie die Liste „Innenräume – m rot“ rechts in jeder Ansicht
        var rot = InnenFunde.Rechnen(InnenFunde.Vorbereiten(this)).SelectMany(o => o.Funde.Where(f => f.Rot).Select(f => (o.Schluessel, f))).ToList();
        if (AussenGeaendert || Leer is not null)
        {
            // Welle D2 (Weisung P12-157, Nico 3a): ueber der Grenze ist die Stadt zu teuer; Sammelliste 279: dieser Fund zuerst
            var teuer = GrenzenLive.Fuer(Stand(), Daten).Fund();
            rot.AddRange(GrenzenLive.Zuerst(PruefenAussen().Where(f => f.Rot), teuer).Select(f => (StadtOrt, f)));
        }
        return rot;
    }

    /// <summary>Name eines Orts fuer die Anzeige (Stadt draussen: "Stadt draußen").</summary>
    public string OrtName(string ort) => Etagen.Teilen(ort) is (var basis, > 0 and var k) && GibtInnenort(ort)
        ? OrtName(basis) + ", " + Etagen.Name(Grundriss(basis), k)
        : ort == StadtOrt ? "Stadt draußen"
        : IstBau(ort) ? Texte.BauName(Bau(ort)?["name"] as string ?? ort[BauPraefix.Length..])
        : AussenBearbeitung.Ort(AussenStand, ort)?["name"] is string n && n.Length > 0 ? n
        : Daten.OrtName(ort);

    /// <summary>Gegenprobe S 0.22.1: Export mit eigenem Gebaeude gesperrt wie bis S 0.22.0 (nur in Tests, je Ablauf).</summary>
    internal static readonly AsyncLocal<bool> EigeneSperreAltFuerTests = new();

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
        // S 0.9.0 (Weisung P12-48) sperrte ein selbst gebautes Gebaeude den Export (gelb). Seit S 0.22.1 (Weisung P12-94,
        // Nico 1a) geht der Export mit ihnen, der gelbe Hinweis bleibt; die alte Sperre nur noch als Gegenprobe.
        if (EigeneSperreAltFuerTests.Value && Blueprint.EigeneGebaeude(AussenStand) is { Count: > 0 } eigene)
        {
            throw new StadtbauFehler([.. eigene.Select(e => OrtName(StadtOrt) + ": " + Texte.EigenesGebaeudeNochNicht(e.Name))]);
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

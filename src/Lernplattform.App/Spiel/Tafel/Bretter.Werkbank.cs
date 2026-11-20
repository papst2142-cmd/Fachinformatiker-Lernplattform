using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Core.Daten;
using Lernplattform.Design;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>Ansicht der Aufgabe „PC zusammenbauen“: Werkbank (Draufsicht), Liste (wie bisher) oder Regal (Lager).</summary>
public enum BauAnsicht
{
    Werkbank,
    Liste,
    Regal,
}

/// <summary>
/// Welle A 1.0.0 (Sammelliste 357, Werkstatt Teil 2, Nico F3 a, Entwurf P12-194): Werkbank in Draufsicht (ESD-Matte mit
/// Mainboard, Steckplaetze als Ziele; Teil antippen, dann Ziel antippen oder „Einsetzen“; „Pruefen“ zeigt, was belegt ist
/// und was fehlt) und Regal (was im Lager liegt, Antippen legt ein Teil auf die Werkbank). Die Liste bleibt als zweiter Weg
/// (Screenreader, kleine Geraete). Alle drei fuellen dieselbe Antwort; die Bewertung bleibt unveraendert.
/// </summary>
public sealed partial class BauteileBrett
{
    private const string Matte = "#1D4ED8";
    private const string Platine = "#14532D";
    private const string Weiss = "#FFFFFF";
    private const string Gelb = "#FACC15";

    /// <summary>Gewaehlte Ansicht (Start: Werkbank).</summary>
    public BauAnsicht Ansicht { get; private set; } = BauAnsicht.Werkbank;

    /// <summary>Auf der Werkbank angetipptes Teil (Kennung als Text), sonst null.</summary>
    public string? GewaehltesTeil { get; private set; }

    private string? _meldung;

    /// <summary>Bereitgelegte Teile (Kennungen, fuer Start-Test und Tests).</summary>
    public IReadOnlyList<object?> Bereit => _verfuegbar;

    /// <summary>Steckplaetze der Aufgabe.</summary>
    public IReadOnlyList<string> Plaetze => _plaetze;

    /// <summary>Belegung (Steckplatz -> Kennung), dieselbe Antwort wie in der Liste.</summary>
    public IReadOnlyDictionary<string, object?> Belegung => _antwort;

    /// <summary>Steckplatz, in den das Teil gehoert (null = keiner).</summary>
    public string? PlatzVon(object? kennung) => Werkbank.PlatzFuer(kennung, _plaetze, Inhalt);

    /// <summary>Letzte Meldung auf der Werkbank (passt nicht, Pruefen), sonst null.</summary>
    public string? Meldung => _meldung;

    /// <summary>Lager des Spielstands (fuer das Regal).</summary>
    private PyDict _lager = new();

    /// <summary>Fuer Start-Test und Tests: Ansicht wechseln.</summary>
    public void AnsichtSetzen(BauAnsicht ansicht)
    {
        Ansicht = ansicht;
        _meldung = null;
        Malen();
    }

    /// <summary>Fuer Start-Test und Tests: Teil antippen (wie der Finger auf der Werkbank).</summary>
    public void TeilAntippen(object? kennung)
    {
        if (Gesperrt)
        {
            return;
        }
        var k = Py.Str(kennung);
        GewaehltesTeil = GewaehltesTeil == k ? null : k;
        _meldung = null;
        Malen();
    }

    /// <summary>Fuer Start-Test und Tests: Ziel (Steckplatz) antippen. true = eingesetzt.</summary>
    public bool ZielAntippen(string platz)
    {
        if (Gesperrt)
        {
            return false;
        }
        if (GewaehltesTeil is not { } teil)
        {
            // ohne gewaehltes Teil: das eingesetzte Teil waehlen (dann „Ablegen“)
            GewaehltesTeil = _antwort.TryGetValue(platz, out var drin) ? Py.Str(drin) : null;
            _meldung = GewaehltesTeil is null ? "Erst ein Teil antippen, dann den Steckplatz." : null;
            Malen();
            return false;
        }
        var kennung = Kennung(teil);
        if (Werkbank.PlatzFuer(kennung, [platz], Inhalt) is null)
        {
            _meldung = Py.Str(SpielDaten.Teil(kennung, Inhalt)?["name"]) + " passt nicht in: " + Bewertung.PlatzName(platz, Inhalt);
            Malen();
            return false;
        }
        _antwort[platz] = kennung;
        _aktuell = _plaetze.FirstOrDefault(p => !_antwort.ContainsKey(p));
        GewaehltesTeil = null;
        _meldung = null;
        Malen();
        return true;
    }

    /// <summary>„Einsetzen“: gewaehltes Teil in seinen Steckplatz.</summary>
    public bool Einsetzen() =>
        GewaehltesTeil is { } t && Werkbank.PlatzFuer(Kennung(t), _plaetze, Inhalt) is { } platz ? ZielAntippen(platz) : Melden("Erst ein Teil antippen.");

    /// <summary>„Ablegen“: gewaehltes Teil aus seinem Steckplatz nehmen.</summary>
    public bool Ablegen()
    {
        if (GewaehltesTeil is not { } t || _antwort.FirstOrDefault(a => Py.Str(a.Value) == t) is not { Key: { } platz })
        {
            return Melden("Erst ein eingesetztes Teil antippen.");
        }
        _antwort.Remove(platz);
        GewaehltesTeil = null;
        _meldung = null;
        Malen();
        return true;
    }

    private bool Melden(string text)
    {
        _meldung = text;
        Malen();
        return false;
    }

    private object? Kennung(string text) => _verfuegbar.FirstOrDefault(v => Py.Str(v) == text) ?? text;

    private void NaechstesTeil()
    {
        var frei = _verfuegbar.Where(v => !_antwort.Values.Any(a => Py.Gleich(a, v))).Select(Py.Str).ToList();
        if (frei.Count == 0)
        {
            Melden("Alle bereitgelegten Teile sind eingesetzt.");
            return;
        }
        var i = GewaehltesTeil is null ? -1 : frei.IndexOf(GewaehltesTeil);
        GewaehltesTeil = frei[(i + 1) % frei.Count];
        _meldung = null;
        Malen();
    }

    /// <summary>Text von „Pruefen“: belegt, fehlt, RAM (ohne zu sagen, ob die Wahl richtig ist; das sagt erst die Abgabe).</summary>
    public string PruefText()
    {
        var optional = ListeOderLeer(Auftrag, "optional");
        var fehlt = Werkbank.Fehlt(_plaetze, _antwort, optional);
        var zeilen = new List<string> { PyFormat.Prozent("Belegt: %d von %d Steckplätzen", _antwort.Count, _plaetze.Count) };
        zeilen.Add(fehlt.Count == 0 ? "Es fehlt kein Pflicht-Steckplatz." : "Fehlt: " + string.Join(", ", fehlt.Select(p => Bewertung.PlatzName(p, Inhalt))));
        if (_plaetze.Contains("ram"))
        {
            zeilen.Add(Werkbank.RamText(Eingesetzt("mainboard"), Eingesetzt("ram")));
        }
        return string.Join("\n", zeilen);
    }

    private PyObjekt? Eingesetzt(string platz) => _antwort.TryGetValue(platz, out var k) ? SpielDaten.Teil(k, Inhalt) : null;

    private Control AnsichtWahl()
    {
        var wahl = Pillen([("werkbank", "Werkbank"), ("liste", "Liste"), ("regal", "Regal")], Ansicht switch
        {
            BauAnsicht.Liste => "liste",
            BauAnsicht.Regal => "regal",
            _ => "werkbank",
        }, w => AnsichtSetzen(w switch { "liste" => BauAnsicht.Liste, "regal" => BauAnsicht.Regal, _ => BauAnsicht.Werkbank }));
        return wahl;
    }

    private IEnumerable<Control?> BauenWerkbank()
    {
        yield return Label("ESD-Matte (Draufsicht)");
        yield return Flaeche(Stapel(10, MatteInhalt()), Matte, Matte, null, 12, new Thickness(12));
        yield return Label("Bereitgelegte Teile");
        if (_verfuegbar.Count == 0)
        {
            yield return T("Auf der Werkbank liegt nichts bereit. Schau ins Regal.", 13, "muted");
        }
        yield return Reihe(_verfuegbar.Select(BereitTeil));
        var info = GewaehltesTeil is { } g
            ? "Gewählt: " + Py.Str(SpielDaten.Teil(Kennung(g), Inhalt)?["name"])
                + (Werkbank.PlatzFuer(Kennung(g), _plaetze, Inhalt) is { } p ? "\nPasst in: " + Bewertung.PlatzName(p, Inhalt) + " (gelb markiert)" : "\nFür dieses Teil gibt es hier keinen Steckplatz.")
            : "Ablauf: 1. Teil antippen 2. Steckplatz antippen oder „Einsetzen“ 3. „Prüfen“ zeigt, was belegt ist und was fehlt";
        yield return Hinweis([T(_meldung is null ? info : _meldung + "\n" + info, 13, "text")], _meldung is null ? "purple" : "yellow");
        if (!Gesperrt)
        {
            var knoepfe = Reihe(
                Knopf("Einsetzen", () => Einsetzen(), "primary", 38),
                Knopf("Ablegen", () => Ablegen(), "ghost", 38),
                Knopf("Nächstes Teil", NaechstesTeil, "ghost", 38),
                Knopf("Prüfen", () => Melden(PruefText()), "ghost", 38));
            foreach (var k in knoepfe.Children)
            {
                k.Margin = new Thickness(0, 0, 8, 8);
            }
            yield return knoepfe;
        }
    }

    /// <summary>Die Matte: Mainboard mit CPU-Sockel, RAM-Baenken, M.2 und PCIe; daneben Netzteil und Gehaeuse.</summary>
    private IEnumerable<Control?> MatteInhalt()
    {
        var platine = new List<Control?>();
        var aufPlatine = new[] { "cpu", "ram", "ssd", "gpu" }.Where(_plaetze.Contains).ToList();
        if (_plaetze.Contains("mainboard"))
        {
            platine.Add(Ziel("mainboard", "#166534"));
        }
        foreach (var p in aufPlatine)
        {
            platine.Add(Ziel(p, Platine));
        }
        yield return Flaeche(Stapel(8, platine), Platine, "#0B3B1E", null, 8, new Thickness(10));
        foreach (var p in _plaetze.Where(p => p != "mainboard" && !aufPlatine.Contains(p)))
        {
            yield return Ziel(p, "#1E3A8A");
        }
    }

    /// <summary>Ein Steckplatz als Ziel: Name, eingesetztes Teil; gelb, wenn das gewaehlte Teil hineinpasst.</summary>
    private Border Ziel(string platz, string hinten)
    {
        var teil = _antwort.GetValueOrDefault(platz);
        var passt = GewaehltesTeil is { } g && Werkbank.PlatzFuer(Kennung(g), [platz], Inhalt) is not null;
        var name = platz switch
        {
            "cpu" => "CPU-Sockel",
            "ram" => "RAM-Bänke",
            "ssd" => "SSD (M.2 oder SATA)",
            "gpu" => "PCIe x16 (Grafikkarte)",
            _ => Bewertung.PlatzName(platz, Inhalt),
        };
        var optional = ListeOderLeer(Auftrag, "optional").Any(o => Py.Gleich(o, platz)) ? " (optional)" : "";
        var zeilen = new List<Control?>
        {
            T(name + optional, 13, Weiss, true),
            T(teil is not null ? Py.Str(SpielDaten.Teil(teil, Inhalt)?["name"]) : "leer", 12, Weiss),
        };
        if (platz == "ram")
        {
            zeilen.Add(RamBaenke());
        }
        string rand;
        double breite;
        if (Gesperrt)
        {
            (rand, breite) = (_erfolg ? "green" : "red", 2);
        }
        else
        {
            (rand, breite) = passt ? (Gelb, 3) : (teil is not null ? "#4ADE80" : "#94A3B8", teil is not null ? 2 : 1);
        }
        var ziel = Flaeche(Stapel(4, zeilen), hinten, rand, Gesperrt ? null : () => ZielAntippen(platz), 8, new Thickness(10, 8), breite);
        Avalonia.Automation.AutomationProperties.SetName(ziel, name + optional + ": " + (teil is not null ? Py.Str(SpielDaten.Teil(teil, Inhalt)?["name"]) : "leer")
            + (platz == "ram" ? ", " + Werkbank.RamText(Eingesetzt("mainboard"), Eingesetzt("ram")) : "") + (passt ? ", passt" : ""));
        return ziel;
    }

    /// <summary>RAM-Baenke des eingesetzten Mainboards: belegte gruen, freie dunkel; darunter „RAM frei: n von m“.</summary>
    private Control RamBaenke()
    {
        var r = Werkbank.Ram(Eingesetzt("mainboard"), Eingesetzt("ram"));
        var baenke = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var zahl = r?.Baenke ?? 0;
        for (var i = 0; i < zahl; i++)
        {
            var belegt = i < r!.Value.Belegt;
            baenke.Children.Add(new Border { Width = 10, Height = 40, CornerRadius = new CornerRadius(2) }
                .Farbe(Border.BackgroundProperty, Ton(belegt ? "#22C55E" : "#1F2937")));
        }
        return Stapel(4, [zahl > 0 ? baenke : null, T(Werkbank.RamText(Eingesetzt("mainboard"), Eingesetzt("ram")), 12, Weiss)]);
    }

    /// <summary>Ein bereitgelegtes Teil (Kachel): Name, Daten, „eingesetzt“ bzw. „aus dem Lager“.</summary>
    private Control BereitTeil(object? kennung)
    {
        var teil = SpielDaten.Teil(kennung, Inhalt)!;
        var gewaehlt = GewaehltesTeil == Py.Str(kennung);
        var drin = _antwort.FirstOrDefault(a => Py.Gleich(a.Value, kennung)).Key;
        var unter = drin is not null ? "eingesetzt: " + Bewertung.PlatzName(drin, Inhalt)
            : _ausLager.Contains(Py.Str(kennung)) ? "aus dem Regal" : Euro(teil["preis"]);
        var kachel = Flaeche(Stapel(2, [T(Py.Str(teil["name"]), 13, "text", true), T(SpielTexte.TeilDaten(teil, Inhalt), 11, "muted"), T(unter, 11, drin is not null ? "accent" : "muted", true)]),
            gewaehlt ? "card_hi" : "card", gewaehlt ? Gelb : "border", Gesperrt ? null : () => TeilAntippen(kennung), 10, new Thickness(10, 8), gewaehlt ? 3 : 1);
        kachel.Width = 180;
        kachel.Margin = new Thickness(0, 0, 8, 8);
        return kachel;
    }

    private IEnumerable<Control?> BauenRegal()
    {
        yield return Label("Regal: Lager der Werkstatt");
        var faecher = Werkbank.Regal(_lager, Inhalt);
        if (faecher.Count == 0)
        {
            yield return T("Das Regal ist leer. Gekaufte Teile kommen hier an.", 13, "muted");
        }
        foreach (var fach in faecher)
        {
            var zeilen = new List<Control?> { T(fach.Name, 14, "text", true) };
            foreach (var (kennung, name, anzahl) in fach.Teile)
            {
                var bereit = _verfuegbar.Any(v => Py.Gleich(v, kennung));
                var zeile = T(name + " · " + anzahl + (bereit ? "" : " (für diesen Auftrag nicht nötig)"), 13, bereit ? "text" : "muted");
                zeilen.Add(bereit && !Gesperrt
                    ? Flaeche(zeile, "card", "border", () => { GewaehltesTeil = Py.Str(kennung); Ansicht = BauAnsicht.Werkbank; _meldung = null; Malen(); }, 8, new Thickness(10, 6))
                    : zeile);
            }
            yield return Flaeche(Stapel(4, zeilen), "card_alt", "border", null, 12, new Thickness(12, 10));
        }
        yield return T("Antippen: Teil auf die Werkbank legen. Gekaufte Teile kommen hier an.", 12, "muted");
        yield return Reihe(Knopf("Zur Werkbank", () => AnsichtSetzen(BauAnsicht.Werkbank), "primary", 38));
    }
}

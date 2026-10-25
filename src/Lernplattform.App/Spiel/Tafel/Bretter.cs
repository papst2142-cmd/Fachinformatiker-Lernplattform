using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Phase 7, W6: die Bretter der Ticket-Aufgaben wie in mobile/src/spiel.py (OptionList, MatchBoard,
/// SlotBoard, OrderBoard, RackBoard, FormBoard, TerminalBoard, DiagnoseBoard mit ExchangePanel,
/// MaintenanceBoard). Jedes Brett baut seinen Inhalt bei jeder Eingabe neu (wie _paint); die
/// Antwort hat den Aufbau, den Python an Game.solve gibt.
/// </summary>
public abstract class Brett : StackPanel
{
    protected Brett(PyObjekt auftrag, PyObjekt inhalt, double abstand = 8)
    {
        Auftrag = auftrag;
        Inhalt = inhalt;
        Spacing = abstand;
    }

    protected PyObjekt Auftrag { get; }

    protected PyObjekt Inhalt { get; }

    protected bool Gesperrt { get; set; }

    /// <summary>complete()</summary>
    public abstract bool Fertig { get; }

    /// <summary>answer (dict(self.options.answer) bzw. der gewaehlte Text bei "auswahl").</summary>
    public abstract object? Antwort { get; }

    /// <summary>hint_key: Schluessel fuer EMPTY_HINT.</summary>
    public virtual string HinweisSchluessel => Py.Str(Auftrag["typ"]);

    /// <summary>reveal(richtig)</summary>
    public virtual void Aufdecken(bool richtig)
    {
        Gesperrt = true;
        Malen();
    }

    /// <summary>_paint: Inhalt neu bauen.</summary>
    protected void Malen()
    {
        Children.Clear();
        foreach (var teil in Bauen())
        {
            if (teil is not null)
            {
                Children.Add(teil);
            }
        }
    }

    protected abstract IEnumerable<Control?> Bauen();

    /// <summary>Feld der Ausgangsdaten (card_alt mit Rahmen).</summary>
    protected static Control Kasten(IEnumerable<Control?> zeilen) => Flaeche(Stapel(4, zeilen), innen: new Thickness(14, 10));

    /// <summary>Pille zum Antippen (Verlauf, wenn aktiv), wie _pill/_paint_pill.</summary>
    protected static Control Pille(string text, bool aktiv, Action? klick, double hoehe = 34)
    {
        var t = T(text, 13, aktiv ? "on_accent" : "text_dim", true);
        t.VerticalAlignment = VerticalAlignment.Center;
        var pille = Flaeche(t, aktiv ? "card" : "card", aktiv ? "card" : "border", klick, hoehe / 2, new Thickness(14, 0), aktiv ? 0 : 1);
        pille.MinHeight = hoehe;
        if (aktiv)
        {
            pille.Bind(Border.BackgroundProperty, pille.GetResourceObservable("VerlaufW.primary"));
        }
        return pille;
    }

    protected static readonly Random Zufall = new();

    protected static List<T> Gemischt<T>(IEnumerable<T> werte)
    {
        var liste = werte.ToList();
        for (var i = liste.Count - 1; i > 0; i--)
        {
            var j = Zufall.Next(i + 1);
            (liste[i], liste[j]) = (liste[j], liste[i]);
        }
        return liste;
    }
}

/// <summary>ui.OptionList: Antwortzeilen zum Antippen, nach dem Pruefen gruen/rot.</summary>
public sealed class AuswahlBrett : Brett
{
    private readonly List<string> _optionen;
    private string? _richtig;

    public AuswahlBrett(PyObjekt auftrag, PyObjekt inhalt, IEnumerable<string>? optionen = null) : base(auftrag, inhalt)
    {
        _optionen = Gemischt(optionen ?? Liste(auftrag["optionen"]).Select(Py.Str));
        Malen();
    }

    public string? Gewaehlt { get; private set; }

    public override bool Fertig => Gewaehlt is not null;

    public override object? Antwort => Gewaehlt;

    public void Aufdecken(string? richtig)
    {
        _richtig = richtig;
        base.Aufdecken(true);
    }

    public override void Aufdecken(bool richtig) => Aufdecken(Py.Str(Auftrag["antwort"]));

    protected override IEnumerable<Control?> Bauen()
    {
        foreach (var option in _optionen)
        {
            var zustand = !Gesperrt ? (option == Gewaehlt ? "selected" : "idle")
                : option == _richtig ? "correct" : option == Gewaehlt ? "wrong" : "muted";
            var (hinten, rand, schrift, punkt) = zustand switch
            {
                "selected" => ("card_hi", "purple", "text", "purple"),
                "correct" => (Mix("card", "green", 0.18), "green", "text", "green"),
                "wrong" => (Mix("card", "red", 0.18), "red", "text", "red"),
                "muted" => ("card_alt", "border", "muted", (string?)null),
                _ => ("card_alt", "border", "text", (string?)null),
            };
            var stark = zustand is "selected" or "correct" or "wrong";
            var kreis = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2),
                Child = punkt is null ? null : new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Pinsel(punkt) },
            }.Farbe(Border.BorderBrushProperty, Ton(zustand == "idle" ? "field_border" : rand));
            var wahl = option;
            var zeile = Flaeche(Zeile(T(option, 15, schrift), null, 12, kreis), hinten, rand, Gesperrt ? null : () => { Gewaehlt = wahl; Malen(); },
                12, new Thickness(14, 12), stark ? 2 : 1);
            Avalonia.Automation.AutomationProperties.SetName(zeile, option);
            yield return zeile;
        }
    }
}

/// <summary>MatchBoard: erst den Begriff, dann das Gegenstueck antippen.</summary>
public sealed class ZuordnungBrett : Brett
{
    private readonly List<string> _links;
    private readonly List<string> _rechts;
    private readonly Dictionary<string, string> _loesung;
    private readonly Dictionary<string, string> _antwort = new(StringComparer.Ordinal);
    private string? _aktuell;

    public ZuordnungBrett(PyObjekt auftrag, PyObjekt inhalt) : base(auftrag, inhalt)
    {
        var paare = Liste(auftrag["paare"]).Select(p => (List<object?>)p!).Select(p => (Py.Str(p[0]), Py.Str(p[1]))).ToList();
        _links = [.. paare.Select(p => p.Item1)];
        _rechts = Gemischt(paare.Select(p => p.Item2));
        _loesung = paare.ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);
        _aktuell = _links.FirstOrDefault();
        Malen();
    }

    public override bool Fertig => _antwort.Count == _links.Count;

    public override object? Antwort
    {
        get
        {
            var antwort = new PyObjekt();
            foreach (var (l, r) in _antwort)
            {
                antwort.Add(l, r);
            }
            return antwort;
        }
    }

    private void Rechts(string rechts)
    {
        if (Gesperrt || _aktuell is null)
        {
            return;
        }
        foreach (var (l, r) in _antwort.ToList())
        {
            if (r == rechts)
            {
                _antwort.Remove(l);
            }
        }
        _antwort[_aktuell] = rechts;
        _aktuell = _links.FirstOrDefault(l => !_antwort.ContainsKey(l));
        Malen();
    }

    protected override IEnumerable<Control?> Bauen()
    {
        yield return Label("1. Begriff antippen");
        foreach (var links in _links)
        {
            var wert = _antwort.GetValueOrDefault(links);
            string hinten, rand, ziel, farbe;
            double breite;
            if (Gesperrt)
            {
                var richtig = wert == _loesung[links];
                var f = richtig ? "green" : "red";
                (hinten, rand, breite) = (Mix("card", f, 0.16), f, 2);
                ziel = (wert ?? "nicht zugeordnet") + (richtig ? "" : PyFormat.Prozent("  (richtig: %s)", _loesung[links]));
                farbe = "text_soft";
            }
            else
            {
                var aktiv = links == _aktuell;
                (hinten, rand, breite) = (aktiv ? "card_hi" : "card_alt", aktiv ? "purple" : "border", aktiv ? 2 : 1);
                ziel = wert is not null ? "passt zu: " + wert : "noch nicht zugeordnet";
                farbe = wert is not null ? "accent" : "muted";
            }
            var l = links;
            yield return Flaeche(Stapel(2, [T(links, 15, "text", true), T(ziel, 12, farbe)]), hinten, rand,
                Gesperrt ? null : () => { _aktuell = l; Malen(); }, 12, new Thickness(14, 10), breite);
        }
        yield return Label("2. Passendes Gegenstück antippen");
        var benutzt = _antwort.Values.ToHashSet(StringComparer.Ordinal);
        yield return Reihe(_rechts.Select(r => (Control?)Pille(r, benutzt.Contains(r), () => Rechts(r), 36)).Select(p => { p!.Margin = new Thickness(0, 0, 8, 8); return p; }));
    }
}

/// <summary>SlotBoard: Steckplatz antippen, darunter klappen die passenden Bauteile auf.</summary>
public sealed class BauteileBrett : Brett
{
    private readonly List<object?> _verfuegbar;
    private readonly HashSet<string> _ausLager;
    private readonly List<string> _plaetze;
    private readonly Dictionary<string, object?> _antwort = new(StringComparer.Ordinal);
    private string? _aktuell;
    private bool _erfolg;

    public BauteileBrett(PyObjekt auftrag, PyObjekt inhalt, IReadOnlyList<object?> verfuegbar, PyDict lager) : base(auftrag, inhalt)
    {
        _verfuegbar = [.. verfuegbar];
        var teile = ListeOderLeer(auftrag, "teile");
        _ausLager = [.. _verfuegbar.Where(t => !teile.Any(x => Py.Gleich(x, t)) && lager.Get(t) is not null).Select(Py.Str)];
        _plaetze = [.. Liste(auftrag["slots"]).Select(Py.Str)];
        _aktuell = _plaetze.FirstOrDefault();
        Malen();
    }

    public override bool Fertig => _antwort.Count > 0;

    public override object? Antwort
    {
        get
        {
            var antwort = new PyObjekt();
            foreach (var platz in _antwort)
            {
                antwort.Add(platz.Key, platz.Value);
            }
            return antwort;
        }
    }

    public override void Aufdecken(bool richtig)
    {
        _erfolg = richtig;
        _aktuell = null;
        base.Aufdecken(richtig);
    }

    private void Teil(object? teil)
    {
        if (Gesperrt || _aktuell is null)
        {
            return;
        }
        _antwort[_aktuell] = teil;
        _aktuell = _plaetze.FirstOrDefault(p => !_antwort.ContainsKey(p));
        Malen();
    }

    protected override IEnumerable<Control?> Bauen()
    {
        var vorgaben = SpielTexte.BauVorgaben(Auftrag);
        if (vorgaben.Length > 0)
        {
            yield return T("Vorgaben: " + vorgaben, 13, "accent", true);
        }
        yield return Label("Steckplatz antippen, dann das Bauteil");
        var optional = ListeOderLeer(Auftrag, "optional");
        foreach (var platz in _plaetze)
        {
            var teil = _antwort.GetValueOrDefault(platz);
            var aktiv = platz == _aktuell && !Gesperrt;
            var name = Bewertung.PlatzName(platz, Inhalt) + (optional.Any(o => Py.Gleich(o, platz)) ? " (optional)" : "");
            var zeilen = new List<Control?>
            {
                T(name, 15, "text", true),
                T(teil is not null ? "eingesetzt: " + Py.Str(SpielDaten.Teil(teil, Inhalt)?["name"]) : "leer", 12, teil is not null ? "accent" : "muted"),
            };
            if (aktiv)
            {
                var wahl = _verfuegbar.Where(t => Py.Gleich(SpielDaten.Teil(t, Inhalt)?["typ"], platz)).ToList();
                if (wahl.Count == 0)
                {
                    zeilen.Add(T("Für diesen Steckplatz liegt nichts bereit.", 12, "muted"));
                }
                foreach (var t in wahl)
                {
                    zeilen.Add(TeilZeile(t, Py.Gleich(t, teil)));
                }
                if (teil is not null)
                {
                    zeilen.Add(Reihe(Knopf("Steckplatz leeren", () => { _antwort.Remove(platz); Malen(); }, "ghost", 36)));
                }
            }
            string hinten, rand;
            double breite;
            if (Gesperrt)
            {
                var f = _erfolg ? "green" : "red";
                (hinten, rand, breite) = (Mix("card", f, 0.12), f, 2);
            }
            else
            {
                (hinten, rand, breite) = (aktiv ? "card_hi" : "card_alt", aktiv ? "purple" : "border", aktiv ? 2 : 1);
            }
            var p = platz;
            yield return Flaeche(Stapel(6, zeilen), hinten, rand, Gesperrt ? null : () => { _aktuell = p == _aktuell ? null : p; Malen(); },
                12, new Thickness(14, 10), breite);
        }
    }

    private Control TeilZeile(object? kennung, bool gewaehlt)
    {
        var teil = SpielDaten.Teil(kennung, Inhalt)!;
        var kopf = Zeile(T(Py.Str(teil["name"]), 14, "text", true),
            _ausLager.Contains(Py.Str(kennung)) ? T("aus dem Lager", 11, "accent", true) : null, 8);
        var zeile = Flaeche(Stapel(2, [kopf, T(PyFormat.Prozent("%s · %s", SpielTexte.TeilDaten(teil, Inhalt), Euro(teil["preis"])), 12, "muted")]),
            gewaehlt ? "card_hi" : "card", gewaehlt ? "purple" : "border", () => Teil(kennung), 10, new Thickness(12, 9), gewaehlt ? 2 : 1);
        return zeile;
    }
}

/// <summary>OrderBoard: Stueckzahl je Angebot mit Minus und Plus, darunter Summe und Lieferzeit.</summary>
public sealed class BestellungBrett : Brett
{
    private readonly Dictionary<string, long> _korb = new(StringComparer.Ordinal);
    private bool? _richtig;

    public BestellungBrett(PyObjekt auftrag, PyObjekt inhalt) : base(auftrag, inhalt) => Malen();

    public override bool Fertig => _korb.Count > 0;

    public override object? Antwort
    {
        get
        {
            var korb = new PyObjekt();
            foreach (var (k, v) in _korb)
            {
                korb.Add(k, v);
            }
            return korb;
        }
    }

    public override void Aufdecken(bool richtig)
    {
        _richtig = richtig;
        base.Aufdecken(richtig);
    }

    private void Aendern(string angebot, int schritt)
    {
        if (Gesperrt)
        {
            return;
        }
        var wert = Math.Max(0, Math.Min(9, _korb.GetValueOrDefault(angebot) + schritt));
        if (wert != 0)
        {
            _korb[angebot] = wert;
        }
        else
        {
            _korb.Remove(angebot);
        }
        Malen();
    }

    private Control Schritt(string text, string angebot, int schritt)
    {
        var zeichen = T(schritt < 0 ? "−" : "+", 18, "text", true);
        zeichen.HorizontalAlignment = HorizontalAlignment.Center;
        zeichen.VerticalAlignment = VerticalAlignment.Center;
        var knopf = Flaeche(zeichen, "card", "border", () => Aendern(angebot, schritt), 18, new Thickness(0));
        knopf.Width = knopf.Height = 36;
        Avalonia.Automation.AutomationProperties.SetName(knopf, text);
        return knopf;
    }

    protected override IEnumerable<Control?> Bauen()
    {
        var kopf = SpielTexte.BestellKopf(Auftrag);
        var bedarf = new List<Control?> { Label("Bedarf") };
        bedarf.AddRange(kopf.Take(kopf.Count - 1).Select(z => (Control?)T(z, 14, "text", true)));
        if (kopf.Count > 0)
        {
            bedarf.Add(T(kopf[^1], 13, "accent", true));
        }
        yield return Kasten(bedarf);
        yield return Label("Angebote");
        foreach (var angebot in Liste(Auftrag["angebote"]).Select(Objekt))
        {
            var id = Py.Str(angebot["id"]);
            var teil = SpielDaten.Teil(angebot["teil"], Inhalt)!;
            var wert = _korb.GetValueOrDefault(id);
            var tage = Py.Int(angebot["lieferzeit"]);
            var zahl = T(Py.Str(wert), 16, wert != 0 ? "accent" : "muted", true);
            zahl.Width = 26;
            zahl.TextAlignment = TextAlignment.Center;
            zahl.VerticalAlignment = VerticalAlignment.Center;
            var rand = wert != 0 ? "purple" : "border";
            if (_richtig is { } r && wert != 0)
            {
                rand = r ? "green" : "red";
            }
            var unten = Zeile(T(PyFormat.Prozent("Lieferung %d Tag%s", tage, tage == 1 ? "" : "e"), 12, "text_dim"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Schritt("Weniger", id, -1), zahl, Schritt("Mehr", id, 1) } }, 8);
            yield return Flaeche(Stapel(4, [
                Zeile(T(Py.Str(teil["name"]), 14, "text", true), T(Euro(angebot["preis"]), 14, "text", true), 8),
                T(PyFormat.Prozent("%s · %s", SpielTexte.TeilDaten(teil, Inhalt), SpielAnsicht.Haendler(angebot["haendler"], Inhalt)?["name"]), 12, "muted"),
                unten,
            ]), wert != 0 ? "card_hi" : "card_alt", rand, null, 12, new Thickness(14, 10), wert != 0 ? 2 : 1);
        }
        var (text, zuTeuer, zuSpaet) = SpielTexte.KorbZusammenfassung(Auftrag, (PyObjekt)Antwort!);
        yield return T("Warenkorb: " + text, 14, zuTeuer || zuSpaet ? "yellow" : "text_soft", true);
    }
}

/// <summary>Fold: aufklappbarer Abschnitt (Kopfzeile antippen klappt auf/zu).</summary>
internal sealed class Klappe
{
    public Klappe(string titel, bool offen = true)
    {
        Titel = titel;
        Offen = offen;
    }

    public string Titel { get; }

    public bool Offen { get; set; }

    public Control Bauen(IEnumerable<Control?> inhalt, string zusammenfassung, string farbe, Action neu)
    {
        var zeichen = T(Offen ? "▴" : "▾", 14, "muted");
        var kopf = Flaeche(Zeile(Label(Titel), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { T(zusammenfassung, 12, farbe), zeichen } }, 8),
            "card_alt", "border", () => { Offen = !Offen; neu(); }, 10, new Thickness(12, 8));
        Avalonia.Automation.AutomationProperties.SetName(kopf, Titel);
        var teile = new List<Control?> { kopf };
        if (Offen)
        {
            teile.Add(Stapel(6, inhalt));
        }
        return Stapel(6, teile);
    }
}

/// <summary>RackBoard: Geraet waehlen, HE antippen; aufklappbar Geraete, Werte und Vorgaben.</summary>
public sealed class RackBrett : Brett
{
    private const string Hinten = "#1E1A30";
    private const string Leer = "#2A2442";
    private const string Rahmen = "#5B5480";
    private const string HeHell = "#A79FC4";

    private readonly List<object?> _geraete;
    private readonly long _einheit;
    private PyObjekt _antwort = [];
    private long? _aktuell = 0;
    private bool _erfolg;
    private readonly Klappe _geraeteKlappe = new("Geräte", false);
    private readonly Klappe _werteKlappe = new("Werte");
    private readonly Klappe _vorgabenKlappe = new("Vorgaben");

    public RackBrett(PyObjekt auftrag, PyObjekt inhalt) : base(auftrag, inhalt)
    {
        _geraete = Liste(auftrag["geraete"]);
        var groesse = Py.Int(Objekt(auftrag["schrank"])["he"]);
        _einheit = groesse <= 14 ? 30 : groesse <= 24 ? 24 : 20;
        Malen();
    }

    public override bool Fertig => _antwort.Count > 0;

    public override object? Antwort => _antwort;

    public override void Aufdecken(bool richtig)
    {
        _erfolg = richtig;
        _aktuell = null;
        base.Aufdecken(richtig);
    }

    private static string HeText(long unten, long hoehe)
    {
        var oben = unten + hoehe - 1;
        return oben == unten ? PyFormat.Prozent("HE %d", unten) : PyFormat.Prozent("HE %d–%d", unten, oben);
    }

    private PyObjekt Geraet(int index) => RackGeraet(_geraete[index], Inhalt)!;

    private string Wo(int index)
    {
        var unten = Get(_antwort, Py.Str((long)index));
        return unten is not null ? HeText(Py.Int(unten), Py.Int(Geraet(index)["he"])) : SpielTexte.RackHerkunft(Auftrag, index);
    }

    private void Tippen(long einheit)
    {
        if (Gesperrt)
        {
            return;
        }
        if (_aktuell is not { } aktuell)
        {
            if (SpielTexte.RackBelegtVon(Auftrag, _antwort, einheit, Inhalt) is { } belegt)
            {
                _aktuell = belegt;
                Malen();
            }
            return;
        }
        _antwort = SpielTexte.RackSetzen(Auftrag, _antwort, aktuell, einheit, Inhalt);
        _aktuell = null;
        Malen();
    }

    protected override IEnumerable<Control?> Bauen()
    {
        var kopf = SpielTexte.RackKopf(Auftrag, Inhalt);
        yield return Kasten([T(kopf[0], 14, "text", true), .. kopf.Skip(1).Select(z => (Control?)T(z, 13, "accent", true))]);
        yield return Label("Gerät wählen, dann HE antippen (HE 1 ist unten)");
        var liste = new ComboBox
        {
            PlaceholderText = "Gerät wählen",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = !Gesperrt,
            ItemsSource = _geraete.Select((g, i) => PyFormat.Prozent("%s · %s", Geraet(i)["name"], Wo(i))).ToList(),
            SelectedIndex = _aktuell is { } a ? (int)a : -1,
        }.Groesse();
        liste.SelectionChanged += (_, _) =>
        {
            if (!Gesperrt && liste.SelectedIndex != (_aktuell ?? -1))
            {
                _aktuell = liste.SelectedIndex >= 0 ? liste.SelectedIndex : null;
                Malen();
            }
        };
        yield return liste;
        yield return new Border
        {
            Child = Stapel(0, Schrank()),
            Background = Pinsel(Hinten),
            BorderBrush = Pinsel(Rahmen),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6),
        };
        if (_aktuell is { } gewaehlt && Py.Hat(_antwort, Py.Str(gewaehlt)) && !Gesperrt)
        {
            yield return Reihe(Knopf("Gerät ausbauen", () =>
            {
                _antwort = [.. _antwort.Where(p => p.Key != Py.Str(gewaehlt))];
                Malen();
            }, "ghost", 36));
        }
        var zeilen = new List<Control?>();
        for (var i = 0; i < _geraete.Count; i++)
        {
            var geraet = Geraet(i);
            var unten = Get(_antwort, Py.Str((long)i));
            var rand = Gesperrt && unten is not null ? (_erfolg ? "green" : "red") : "border";
            var index = i;
            zeilen.Add(Flaeche(Zeile(Stapel(2, [
                    Zeile(T(Py.Str(geraet["name"]), 13, "text", true), T(Wo(i), 12, unten is not null ? "accent" : "muted", true), 8),
                    T(SpielTexte.RackDaten(geraet), 11, "muted"),
                ]), null, 10, Strich(SpielAnsicht.RackFarben.GetValueOrDefault(Py.Str(geraet["typ"]), "cyan"), 4, 30)),
                rand: rand, klick: Gesperrt ? null : () => { _aktuell = index; Malen(); }, ecken: 10, innen: new Thickness(10, 7)));
        }
        yield return _geraeteKlappe.Bauen(zeilen, PyFormat.Prozent("%d von %d eingebaut", _antwort.Count, _geraete.Count), "muted", Malen);
        var balken = SpielAnsicht.RackBalken(Auftrag, _antwort, Inhalt);
        var zuViel = balken.Any(b => Py.Wahr(b["zu_viel"]));
        yield return _werteKlappe.Bauen(WerteZeilen(balken), zuViel ? "zu viel" : "", zuViel ? "red" : "muted", Malen);
        var pruefungen = SpielAnsicht.RackPruefungen(Auftrag, _antwort, Inhalt);
        var (text, farbe) = PruefZusammenfassung(pruefungen);
        yield return _vorgabenKlappe.Bauen(PruefZeilen(pruefungen), text, farbe, Malen);
    }

    /// <summary>cabinet_rows: Schrank als Zeilen von oben bis HE 1.</summary>
    private List<Control?> Schrank()
    {
        var zeilen = new List<Control?>();
        var einheit = Py.Int(Objekt(Auftrag["schrank"])["he"]);
        var heFarbe = Design.FarbenAnwenden.Aktuell.Hell ? HeHell : "muted";
        Control Reihe(string beschriftung, double hoehe, Control? inhalt, string fuellung, string? rand, double randBreite, long tippen)
        {
            var nummer = T(beschriftung, 10, heFarbe);
            nummer.TextAlignment = TextAlignment.Right;
            nummer.VerticalAlignment = VerticalAlignment.Center;
            var feld = new Border
            {
                Child = inhalt,
                Height = hoehe - 3,
                Background = Pinsel(fuellung),
                BorderBrush = rand is null ? null : Pinsel(rand),
                BorderThickness = new Thickness(randBreite),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 0),
                Margin = new Thickness(0, 1.5),
            };
            if (!Gesperrt)
            {
                feld.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
                feld.PointerReleased += (_, e) =>
                {
                    e.Handled = true;
                    Tippen(tippen);
                };
            }
            var raster = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*"), ColumnSpacing = 6, Height = hoehe };
            raster.Children.Add(nummer);
            Grid.SetColumn(feld, 1);
            raster.Children.Add(feld);
            return raster;
        }
        while (einheit >= 1)
        {
            var belegt = SpielTexte.RackBelegtVon(Auftrag, _antwort, einheit, Inhalt);
            if (belegt is null)
            {
                zeilen.Add(Reihe(Py.Str(einheit), _einheit, null, Leer, null, 0, einheit));
                einheit--;
                continue;
            }
            var geraet = Geraet((int)belegt);
            var farbe = SpielAnsicht.RackFarben.GetValueOrDefault(Py.Str(geraet["typ"]), "cyan");
            var gewaehlt = belegt == _aktuell && !Gesperrt;
            var unten = Py.Int(_antwort[Py.Str(belegt)]);
            var he = Py.Int(geraet["he"]);
            var name = T(Py.Str(geraet["name"]), _einheit < 24 ? 12 : 13, "text", true);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            name.VerticalAlignment = VerticalAlignment.Center;
            zeilen.Add(Reihe(he == 1 ? Py.Str(einheit) : PyFormat.Prozent("%d–%d", unten, einheit), _einheit * he, name,
                Mix("card", farbe, gewaehlt ? 0.55 : 0.35), farbe, gewaehlt ? 2 : 1, einheit));
            einheit = unten - 1;
        }
        return zeilen;
    }

    /// <summary>value_rows</summary>
    internal static List<Control?> WerteZeilen(IEnumerable<PyObjekt> balken)
    {
        var zeilen = new List<Control?>();
        foreach (var b in balken)
        {
            var zuViel = Py.Wahr(b["zu_viel"]);
            var anteil = Py.F(b["anteil"]);
            var farbe = zuViel ? "red" : anteil >= 0.9 ? "yellow" : "green";
            var teil = (int)Math.Round(Math.Max(0, Math.Min(1, anteil)) * 1000, MidpointRounding.ToEven);
            var spur = new Grid { Height = 7, ColumnDefinitions = new ColumnDefinitions($"{Math.Max(1, teil)}*,{Math.Max(1, 1000 - teil)}*") };
            var hinten = new Border { CornerRadius = new CornerRadius(3.5) }.Farbe(Border.BackgroundProperty, "Pinsel.ring_bg");
            Grid.SetColumnSpan(hinten, 2);
            spur.Children.Add(hinten);
            if (teil > 0)
            {
                spur.Children.Add(new Border { CornerRadius = new CornerRadius(3.5), Background = Pinsel(farbe) });
            }
            zeilen.Add(Stapel(3, [Zeile(T(Py.Str(b["name"]), 12, "text_dim"), T(Py.Str(b["text"]), 12, zuViel ? "red" : "text", true), 6), spur]));
        }
        return zeilen;
    }

    /// <summary>check_rows: Vorgaben mit Haken.</summary>
    internal static List<Control?> PruefZeilen(IEnumerable<PyObjekt> pruefungen) =>
        [.. pruefungen.Select(p =>
        {
            var ok = Py.Wahr(p["ok"]);
            var zeichen = T(ok ? "✓" : "✗", 14, ok ? "green" : "red", true);
            zeichen.VerticalAlignment = VerticalAlignment.Top;
            var text = T(Py.Str(p["text"]), 13, ok ? "text_soft" : "text");
            text.VerticalAlignment = VerticalAlignment.Top;
            return (Control?)Zeile(text, null, 6, zeichen);
        })];

    /// <summary>checks_summary</summary>
    internal static (string, string) PruefZusammenfassung(IEnumerable<PyObjekt> pruefungen)
    {
        var offen = pruefungen.Count(p => !Py.Wahr(p["ok"]));
        return offen == 0 ? ("alles erfüllt", "green") : (offen == 1 ? "1 offen" : PyFormat.Prozent("%d offen", offen), "red");
    }
}

/// <summary>FormBoard: Eingabefelder untereinander, bei IP-Plaenen ein Kasten je Teilnetz.</summary>
public sealed class FormularBrett : Brett
{
    private readonly List<Bewertung.FormFeld> _felder;
    private readonly Dictionary<string, TextBox> _eingaben = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _wahl = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WrapPanel> _pillen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _marken = new(StringComparer.Ordinal);

    public FormularBrett(PyObjekt auftrag, PyObjekt inhalt) : base(auftrag, inhalt)
    {
        _felder = Bewertung.FormFelder(auftrag);
        Children.Add(Kasten([Label("Ausgangsdaten"), .. SpielTexte.FormAusgang(auftrag).Select(z => (Control?)T(z, 14, "text", true))]));
        var gruppen = new List<string>();
        foreach (var feld in _felder)
        {
            if (!Py.Wahr(feld.Gruppe))
            {
                Children.Add(Feld(feld));
            }
            else if (!gruppen.Contains(Py.Str(feld.Gruppe)))
            {
                gruppen.Add(Py.Str(feld.Gruppe));
            }
        }
        foreach (var gruppe in gruppen)
        {
            Children.Add(Flaeche(Stapel(8, [T(gruppe, 15, "text", true), .. _felder.Where(f => Py.Str(f.Gruppe) == gruppe && Py.Wahr(f.Gruppe)).Select(f => (Control?)Feld(f))]),
                innen: new Thickness(12, 10)));
        }
    }

    public override bool Fertig => ((PyObjekt)Antwort!).Any(p => Py.Str(p.Value).Trim().Length > 0);

    public override object? Antwort
    {
        get
        {
            var antwort = new PyObjekt();
            foreach (var (id, feld) in _eingaben)
            {
                antwort.Add(id, feld.Text ?? "");
            }
            foreach (var (id, wert) in _wahl)
            {
                Py.Setzen(antwort, id, wert);
            }
            return antwort;
        }
    }

    /// <summary>Eingaben aus einem frueheren Brett uebernehmen (_offer_calc: inputs[key].value = value).</summary>
    public void Vorbelegen(PyObjekt alt)
    {
        foreach (var (id, wert) in alt)
        {
            if (_eingaben.TryGetValue(id, out var feld))
            {
                feld.Text = Py.Str(wert);
            }
        }
    }

    protected override IEnumerable<Control?> Bauen() => [];

    private Control Feld(Bewertung.FormFeld feld)
    {
        var marke = T("", 12, "text", true);
        marke.IsVisible = false;
        _marken[feld.Id] = marke;
        if (feld.Art == "wahl")
        {
            _wahl[feld.Id] = "";
            var pillen = new WrapPanel();
            _pillen[feld.Id] = pillen;
            PillenMalen(feld);
            return Stapel(6, [T(feld.Label, 13, "text_dim"), pillen, marke]);
        }
        var eingabe = Eingabe("", feld.Einheit.Length > 0 ? null : null);
        if (feld.Einheit.Length > 0)
        {
            eingabe.InnerRightContent = T(feld.Einheit, 14, "muted");
        }
        _eingaben[feld.Id] = eingabe;
        return Stapel(4, [T(feld.Label, 13, "text_dim"), eingabe, marke]);
    }

    private void PillenMalen(Bewertung.FormFeld feld)
    {
        var pillen = _pillen[feld.Id];
        pillen.Children.Clear();
        foreach (var option in feld.Optionen ?? [])
        {
            var text = Py.Str(option);
            var pille = Pille(text, _wahl[feld.Id] == text, Gesperrt ? null : () => { _wahl[feld.Id] = text; PillenMalen(feld); }, 36);
            pille.Margin = new Thickness(0, 0, 8, 8);
            pillen.Children.Add(pille);
        }
    }

    public override void Aufdecken(bool richtig)
    {
        Gesperrt = true;
        var pruefung = SpielAnsicht.FormPruefung(Auftrag, (PyObjekt)Antwort!);
        foreach (var feld in _felder)
        {
            var ok = Py.Wahr(pruefung[feld.Id]);
            var farbe = ok ? "green" : "red";
            if (_eingaben.TryGetValue(feld.Id, out var eingabe))
            {
                eingabe.Farbe(TextBox.BorderBrushProperty, Ton(farbe));
                eingabe.BorderThickness = new Thickness(2);
                eingabe.IsReadOnly = true;
            }
            if (_pillen.ContainsKey(feld.Id))
            {
                PillenMalen(feld);
            }
            var marke = _marken[feld.Id];
            marke.Text = ok ? "richtig" : "richtig wäre: " + feld.Anzeige;
            marke.Farbe(TextBlock.ForegroundProperty, Ton(farbe));
            marke.IsVisible = true;
        }
    }
}

/// <summary>TerminalBoard: je Schritt einen Befehl antippen, die Ausgabe erscheint im Terminal.</summary>
public sealed class TerminalBrett : Brett
{
    private readonly List<List<int>> _versuche;
    private bool? _erfolg;

    public TerminalBrett(PyObjekt auftrag, PyObjekt inhalt) : base(auftrag, inhalt, 10)
    {
        _versuche = [.. Liste(auftrag["schritte"]).Select(_ => new List<int>())];
        Malen();
    }

    public override bool Fertig => SpielTexte.TerminalAktuell(Auftrag, _versuche) is null;

    public override object? Antwort => new PyObjekt { { "schritte", _versuche.Select(v => (object?)v.Select(i => (object?)(long)i).ToList()).ToList() } };

    public override void Aufdecken(bool richtig)
    {
        _erfolg = richtig;
        base.Aufdecken(richtig);
    }

    private static string Rollenfarbe(string rolle) => rolle switch
    {
        "start" or "kommentar" => "muted",
        "ausgabe" => "text_soft",
        "fehler" => "red",
        "gefahr" => "yellow",
        _ => "text",
    };

    private TextBlock ZeileBauen(string rolle, string wert)
    {
        var zeile = new TextBlock { FontFamily = Festbreite.Schrift, TextWrapping = TextWrapping.NoWrap }.Groesse("Klein");
        if (rolle is "eingabe" or "cursor")
        {
            zeile.Inlines = [
                new Run(Py.Str(Auftrag["prompt"]) + " ") { Foreground = Pinsel("green") },
                new Run(wert) { Foreground = Pinsel(rolle == "cursor" ? "accent" : "text") },
            ];
        }
        else
        {
            zeile.Text = wert;
            zeile.Foreground = Pinsel(Rollenfarbe(rolle));
        }
        return zeile;
    }

    protected override IEnumerable<Control?> Bauen()
    {
        var schritte = Liste(Auftrag["schritte"]).Select(Objekt).ToList();
        var schritt = SpielTexte.TerminalAktuell(Auftrag, _versuche);
        var zeilen = SpielTexte.TerminalProtokoll(Auftrag, _versuche).Select(z => (Control?)ZeileBauen(z.Rolle, z.Text)).ToList();
        if (schritt is not null && !Gesperrt)
        {
            zeilen.Add(ZeileBauen("cursor", "_"));
        }
        var rand = Gesperrt ? (_erfolg == true ? "green" : "red") : "border";
        string stufe, ziel;
        if (schritt is null)
        {
            (stufe, ziel) = ("ALLE SCHRITTE ERLEDIGT", Py.Str(schritte[^1]["ziel"]));
        }
        else
        {
            (stufe, ziel) = (Core.Texte.Beschriftungen.Gross(SpielTexte.TerminalSchritt(Auftrag, schritt.Value)), Py.Str(schritte[schritt.Value]["ziel"]));
        }
        yield return Stapel(2, [Label(stufe), T(ziel, 15, "text", true)]);
        yield return Flaeche(new ScrollViewer
        {
            Content = Stapel(2, zeilen),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        }, "sidebar", rand, innen: new Thickness(12, 10), randBreite: Gesperrt ? 2 : 1);
        if (Gesperrt)
        {
            yield break;
        }
        if (schritt is null)
        {
            yield return T("Das System ist eingerichtet. Reiche jetzt die Lösung ein.", 13, "green");
            yield break;
        }
        yield return Label("Befehl wählen");
        var befehle = Liste(schritte[schritt.Value]["befehle"]).Select(Objekt).ToList();
        for (var i = 0; i < befehle.Count; i++)
        {
            var versucht = _versuche[schritt.Value].Contains(i);
            var text = new TextBlock { Text = Py.Str(befehle[i]["befehl"]), FontFamily = Festbreite.Schrift, TextWrapping = TextWrapping.Wrap }.Groesse();
            text.Farbe(TextBlock.ForegroundProperty, versucht ? "Pinsel.muted" : "Pinsel.text_soft");
            var index = i;
            var s = schritt.Value;
            yield return Flaeche(text, versucht ? "card" : "card_alt", "border",
                versucht ? null : () => { if (!_versuche[s].Contains(index)) { _versuche[s].Add(index); Malen(); } }, 12, new Thickness(14, 12));
        }
    }
}

/// <summary>DiagnoseBoard: Pruefungen antippen, Ergebnisse im Notizblock, dann Ursache und Massnahme.</summary>
public sealed class DiagnoseBrett : Brett
{
    private readonly List<object?> _erledigt = [];
    private readonly AuswahlBrett _ursache;
    private readonly AuswahlBrett _massnahme;
    private readonly AustauschTeil? _austausch;
    private readonly string _lager;

    public DiagnoseBrett(PyObjekt auftrag, PyObjekt inhalt, IReadOnlyList<object?> verfuegbar, PyDict lager, Action<object?>? nachbestellen)
        : base(auftrag, inhalt, 10)
    {
        _lager = SpielTexte.ErsatzImLager(auftrag, verfuegbar, inhalt);
        _ursache = new AuswahlBrett(auftrag, inhalt, Liste(auftrag["ursachen"]).Select(Py.Str));
        _massnahme = new AuswahlBrett(auftrag, inhalt, Liste(auftrag["massnahmen"]).Select(Py.Str));
        if (Py.Wahr(Get(auftrag, "austausch")))
        {
            _austausch = new AustauschTeil(auftrag, inhalt, lager, nachbestellen);
        }
        Malen();
    }

    public override string HinweisSchluessel => _austausch is not null ? "austausch" : "diagnose";

    public override bool Fertig => _ursache.Gewaehlt is not null && _massnahme.Gewaehlt is not null && (_austausch is null || _austausch.Fertig);

    public override object? Antwort
    {
        get
        {
            var antwort = new PyObjekt
            {
                { "pruefungen", _erledigt.ToList() }, { "ursache", _ursache.Gewaehlt ?? "" }, { "massnahme", _massnahme.Gewaehlt ?? "" },
            };
            if (_austausch is not null)
            {
                antwort.Add("teil", _austausch.Teil);
                antwort.Add("reihenfolge", _austausch.Reihenfolge.Select(r => (object?)r).ToList());
            }
            return antwort;
        }
    }

    public override void Aufdecken(bool richtig)
    {
        _ursache.Aufdecken(Py.Str(Auftrag["ursache"]));
        _massnahme.Aufdecken(Py.Str(Auftrag["massnahme"]));
        _austausch?.Aufdecken();
        base.Aufdecken(richtig);
    }

    protected override IEnumerable<Control?> Bauen()
    {
        if (_lager.Length > 0)
        {
            yield return T(_lager, 13, "accent", true);
        }
        yield return Label("Prüfungen");
        var wichtig = SpielAnsicht.DiagnoseWichtig(Auftrag);
        foreach (var pruefung in Liste(Auftrag["pruefungen"]).Select(Objekt))
        {
            var id = pruefung["id"];
            var fertig = _erledigt.Any(e => Py.Gleich(e, id));
            var schluessel = wichtig.Any(w => Py.Gleich(w, id));
            var marke = Gesperrt && schluessel ? (fertig ? "geprüft · wichtig" : "wichtig") : fertig ? "geprüft" : "";
            yield return Flaeche(Zeile(T(Py.Str(pruefung["text"]), 14, fertig ? "muted" : "text_soft"), T(marke, 12, "green", true), 10),
                fertig ? "card" : "card_alt", Gesperrt && schluessel ? "green" : "border",
                Gesperrt || fertig ? null : () => { _erledigt.Add(id); Malen(); }, 12, new Thickness(14, 11), Gesperrt && schluessel ? 2 : 1);
        }
        yield return Label("Notizblock");
        var notizen = new List<Control?>();
        if (_erledigt.Count == 0)
        {
            notizen.Add(T("Noch nichts geprüft. Die Ergebnisse erscheinen hier.", 13, "muted"));
        }
        for (var i = 0; i < _erledigt.Count; i++)
        {
            var eintrag = SpielAnsicht.DiagnosePruefung(Auftrag, _erledigt[i])!;
            notizen.Add(T(PyFormat.Prozent("%d. %s", i + 1, eintrag["text"]), 13, "text", true));
            notizen.Add(T(Py.Str(eintrag["ergebnis"]), 13, "accent"));
        }
        yield return Flaeche(Stapel(4, notizen), innen: new Thickness(14, 10));
        yield return T(SpielTexte.DiagnoseZaehler(Auftrag, _erledigt.Count), 12, "muted");
        yield return Label("Ursache");
        yield return Abloesen(_ursache);
        yield return Label("Maßnahme");
        yield return Abloesen(_massnahme);
        if (_austausch is not null)
        {
            yield return Abloesen(_austausch);
        }
    }

    private static Control Abloesen(Control teil)
    {
        (teil.Parent as Panel)?.Children.Remove(teil);
        return teil;
    }
}

/// <summary>ExchangePanel: Ersatzteil waehlen (aus dem Lager, sonst nachbestellen) und den Ablauf antippen.</summary>
public sealed class AustauschTeil : StackPanel
{
    private readonly PyObjekt _auftrag;
    private readonly PyObjekt _inhalt;
    private readonly PyDict _lager;
    private readonly Action<object?>? _nachbestellen;
    private bool _gesperrt;
    private bool _loesung;

    public AustauschTeil(PyObjekt auftrag, PyObjekt inhalt, PyDict lager, Action<object?>? nachbestellen)
    {
        _auftrag = auftrag;
        _inhalt = inhalt;
        _lager = lager;
        _nachbestellen = nachbestellen;
        Spacing = 8;
        Malen();
    }

    public object? Teil { get; private set; }

    public List<string> Reihenfolge { get; } = [];

    public bool Fertig => Teil is not null && Reihenfolge.Count > 0;

    public void Aufdecken()
    {
        _gesperrt = true;
        _loesung = true;
        Malen();
    }

    private void Malen()
    {
        Children.Clear();
        var austausch = Objekt(_auftrag["austausch"]);
        var art = Py.Str(Get(SpielAnsicht.ErsatzTypen(_inhalt), Py.Str(austausch["typ"]), "Ersatzteil"));
        Children.Add(Label("Austausch: " + art));
        Children.Add(T("Wähle ein Teil aus dem Lager. Fehlt das Passende, bestelle es nach: Es kommt am nächsten Arbeitstag, bis dahin wartet das Ticket.", 12, "text_dim"));
        foreach (var teil in SpielAnsicht.ErsatzKatalog(_auftrag, _inhalt))
        {
            var id = teil["id"];
            var anzahl = Py.Int(_lager.Get(id) ?? 0L);
            var zeilen = new List<Control?>
            {
                Zeile(T(Py.Str(teil["name"]), 14, "text", true),
                    T(anzahl != 0 ? PyFormat.Prozent("im Lager: %d", anzahl) : "nicht im Lager", 11, anzahl != 0 ? "green" : "muted", true), 8),
                T(PyFormat.Prozent("%s · %s", SpielTexte.ErsatzDaten(teil, _inhalt), Euro(teil["preis"])), 12, "muted"),
            };
            if (anzahl == 0 && _nachbestellen is not null)
            {
                zeilen.Add(Reihe(Knopf(PyFormat.Prozent("Nachbestellen (%s)", Euro(teil["preis"])), () => _nachbestellen(id), "ghost", 34)));
            }
            var gewaehlt = Py.Gleich(id, Teil);
            var richtig = _loesung && Bewertung.ErsatzPasst(_auftrag, id, _inhalt).Count == 0;
            Children.Add(Flaeche(Stapel(4, zeilen), gewaehlt ? "card_hi" : "card_alt", richtig ? "green" : gewaehlt ? "purple" : "border",
                anzahl != 0 && !_gesperrt ? () => { Teil = id; Malen(); } : null, 10, new Thickness(12, 9), gewaehlt || richtig ? 2 : 1));
        }
        Children.Add(Label("Ablauf"));
        Children.Add(T("Tippe die Schritte in der richtigen Reihenfolge an. Nicht jeder Schritt gehört dazu.", 12, "text_dim"));
        var schritte = ListeOderLeer(austausch, "schritte").Select(Py.Str).ToList();
        foreach (var text in SpielTexte.AustauschSchritte(_auftrag))
        {
            int? nummer = Reihenfolge.Contains(text) ? Reihenfolge.IndexOf(text) + 1 : null;
            if (_loesung)
            {
                nummer = schritte.Contains(text) ? schritte.IndexOf(text) + 1 : null;
            }
            var gut = _loesung && schritte.Contains(text);
            var zahl = T(nummer?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", 14, "accent", true);
            zahl.Width = 22;
            var t = text;
            Children.Add(Flaeche(Zeile(T(text, 14, "text_soft"), null, 6, zahl), nummer is not null ? "card" : "card_alt", gut ? "green" : "border",
                _gesperrt ? null : () => { if (!Reihenfolge.Contains(t)) { Reihenfolge.Add(t); Malen(); } }, 12, new Thickness(12, 11), gut ? 2 : 1));
        }
        Children.Add(Reihe(Knopf("Ablauf zurücksetzen", () => { if (!_gesperrt) { Reihenfolge.Clear(); Malen(); } }, "ghost", 38)));
    }
}

/// <summary>MaintenanceBoard: Pruefpunkt antippen, Messwert lesen, bewerten, Abschluss waehlen.</summary>
public sealed class WartungBrett : Brett
{
    private readonly List<object?> _geprueft = [];
    private readonly Dictionary<string, string> _bewertung = new(StringComparer.Ordinal);
    private readonly AuswahlBrett _abschluss;
    private bool _loesung;

    public WartungBrett(PyObjekt auftrag, PyObjekt inhalt) : base(auftrag, inhalt, 10)
    {
        _abschluss = new AuswahlBrett(auftrag, inhalt, Liste(auftrag["abschluss"]).Select(Py.Str));
        Malen();
    }

    public override bool Fertig => _bewertung.Count == Liste(Auftrag["pruefpunkte"]).Count && _abschluss.Gewaehlt is not null;

    private PyObjekt Bewertungen()
    {
        var bewertung = new PyObjekt();
        foreach (var (k, v) in _bewertung)
        {
            bewertung.Add(k, v);
        }
        return bewertung;
    }

    public override object? Antwort => new PyObjekt { { "bewertung", Bewertungen() }, { "abschluss", _abschluss.Gewaehlt ?? "" } };

    public override void Aufdecken(bool richtig)
    {
        _loesung = true;
        _abschluss.Aufdecken(Py.Str(Auftrag["abschluss_antwort"]));
        foreach (var punkt in Liste(Auftrag["pruefpunkte"]).Select(Objekt))
        {
            if (!_geprueft.Any(g => Py.Gleich(g, punkt["id"])))
            {
                _geprueft.Add(punkt["id"]);
            }
        }
        base.Aufdecken(richtig);
    }

    private void Bewerten(object? punkt, string schluessel)
    {
        if (Gesperrt)
        {
            return;
        }
        if (!_geprueft.Any(g => Py.Gleich(g, punkt)))
        {
            _geprueft.Add(punkt);
        }
        _bewertung[Py.Str(punkt)] = schluessel;
        Malen();
    }

    protected override IEnumerable<Control?> Bauen()
    {
        yield return Label("Prüfpunkte");
        foreach (var punkt in Liste(Auftrag["pruefpunkte"]).Select(Objekt))
        {
            var id = punkt["id"];
            var fertig = _geprueft.Any(g => Py.Gleich(g, id));
            var gewertet = _bewertung.GetValueOrDefault(Py.Str(id));
            var ergebnis = fertig ? Py.Str(punkt["ergebnis"]) : "Zum Prüfen antippen.";
            var rand = "border";
            if (_loesung)
            {
                var soll = Py.Wahr(Get(punkt, "auffaellig")) ? SpielAnsicht.BewertungAuffaellig : SpielAnsicht.BewertungOk;
                rand = gewertet == soll ? "green" : "red";
                ergebnis = PyFormat.Prozent("%s · richtig: %s", punkt["ergebnis"], SpielAnsicht.BewertungText[soll]);
            }
            var zeilen = new List<Control?> { T(Py.Str(punkt["text"]), 14, "text_soft"), T(ergebnis, 13, fertig ? "accent" : "muted") };
            if (fertig)
            {
                zeilen.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8,
                    Children =
                    {
                        Pille(SpielAnsicht.BewertungText[SpielAnsicht.BewertungOk], gewertet == SpielAnsicht.BewertungOk, () => Bewerten(id, SpielAnsicht.BewertungOk)),
                        Pille(SpielAnsicht.BewertungText[SpielAnsicht.BewertungAuffaellig], gewertet == SpielAnsicht.BewertungAuffaellig, () => Bewerten(id, SpielAnsicht.BewertungAuffaellig)),
                    },
                });
            }
            yield return Flaeche(Stapel(6, zeilen), fertig ? "card" : "card_alt", rand,
                Gesperrt || fertig ? null : () => { _geprueft.Add(id); Malen(); }, 12, new Thickness(14, 11), _loesung ? 2 : 1);
        }
        yield return T(SpielTexte.WartungZaehler(Auftrag, Bewertungen()), 12, "muted");
        yield return Label("Abschluss für das Protokoll");
        (_abschluss.Parent as Panel)?.Children.Remove(_abschluss);
        yield return _abschluss;
    }
}

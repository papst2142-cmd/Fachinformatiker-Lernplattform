using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3, W2 (Paket R6): Karte "Subnetting" wie app_gui.SubnetCalc / main.py _build_subnet:
/// Schalter IPv4/IPv6, darunter die Ansichten des Modus (Netzdaten, Netz aufteilen, Nach
/// Hostbedarf nur IPv4). Jede Ansicht hat eigene Eingaben, eigenes Ergebnis, eigene Seite und
/// eigenen Rechenweg; beim Wechsel wird nichts neu gerechnet, der Rechenweg klappt zu. Eine
/// Ansicht entsteht erst beim ersten Zeigen (wie SubnetViews in Python 0.63.1).
/// </summary>
internal sealed class SubnetKarte
{
    private readonly RechnerSeite _seite;
    private readonly TextBlock _unter = RechnerTeile.Unterschrift(SUBNET_SUBTITLE["ipv4"]);
    private readonly Pillen _modusPillen;
    private readonly Dictionary<string, Pillen> _ansichtPillen = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), Ansicht> _ansichten = [];
    private readonly Dictionary<string, string> _ansichtVon = new(StringComparer.Ordinal) { ["ipv4"] = "netzdaten", ["ipv6"] = "netzdaten" };
    private readonly StackPanel _pillenPlatz = new();
    private readonly ContentControl _ansichtPlatz = new();

    public SubnetKarte(RechnerSeite seite)
    {
        _seite = seite;
        _modusPillen = new Pillen(SUBNET_MODES, "ipv4", ModusSetzen);
        var inhalt = new StackPanel { Spacing = 4, Children = { _modusPillen, _pillenPlatz, _ansichtPlatz } };
        Karte = RechnerTeile.Karte(SUBNET_TITLE, "Pinsel.accent", _unter, inhalt);
        Zeigen();
    }

    public Border Karte { get; }

    public string Modus { get; private set; } = "ipv4";

    /// <summary>Gezeigte Ansicht.</summary>
    public Ansicht Aktuell => Holen(Modus, _ansichtVon[Modus]);

    private bool Schmal => _seite.Schmal;

    private Ansicht Holen(string modus, string schluessel)
    {
        if (!_ansichten.TryGetValue((modus, schluessel), out var ansicht))
        {
            ansicht = new Ansicht(this, modus, schluessel);
            ansicht.Kasten.Einheitlich = Schmal;
            ansicht.Weg.Kasten.Einheitlich = Schmal;
            _ansichten[(modus, schluessel)] = ansicht;
        }
        return ansicht;
    }

    private Pillen AnsichtPillen(string modus)
    {
        if (!_ansichtPillen.TryGetValue(modus, out var pillen))
        {
            pillen = new Pillen(SUBNET_VIEWS[modus], _ansichtVon[modus], s => AnsichtSetzen(modus, s)) { Margin = new Thickness(0, 4, 0, 6) };
            _ansichtPillen[modus] = pillen;
        }
        return pillen;
    }

    private void Zeigen()
    {
        _pillenPlatz.Children.Clear();
        _pillenPlatz.Children.Add(AnsichtPillen(Modus));
        _ansichtPlatz.Content = Aktuell.Panel;
    }

    // -- Wechsel (Plan 0.63 4.6) ----------------------------------------------------------------

    /// <summary>set_mode: Rechenweg zu, Modus zeigen, Unterschrift IPv4/IPv6, Fokus ins Netzfeld.</summary>
    public void ModusSetzen(string modus)
    {
        if (modus == Modus)
        {
            return;
        }
        Aktuell.Weg.Zeigen(false);
        Modus = modus;
        _modusPillen.Waehle(modus);
        _unter.Text = SUBNET_SUBTITLE[modus];
        Zeigen();
        Aktuell.Netz.Focus();
    }

    /// <summary>set_view: Rechenweg der alten Ansicht zu, neue Ansicht zeigen.</summary>
    public void AnsichtSetzen(string modus, string schluessel)
    {
        var alt = Holen(modus, _ansichtVon[modus]);
        if (alt.Schluessel == schluessel)
        {
            return;
        }
        alt.Weg.Zeigen(false);
        _ansichtVon[modus] = schluessel;
        AnsichtPillen(modus).Waehle(schluessel);
        if (modus == Modus)
        {
            Zeigen();
            Aktuell.Netz.Focus();
        }
    }

    /// <summary>Aufbau breit/schmal gewechselt: gebaute Ansichten in der passenden Fassung zeigen.</summary>
    public void AufbauGeaendert()
    {
        foreach (var ansicht in _ansichten.Values)
        {
            ansicht.Kasten.Einheitlich = Schmal;
            ansicht.Weg.Kasten.Einheitlich = Schmal;
            ansicht.Anzeigen();
            ansicht.Weg.Fassung(Schmal);
        }
    }

    public void RechenwegZeigen(bool offen) => Aktuell.Weg.Zeigen(offen);

    /// <summary>Eine Ansicht der Karte (Eingaben, Berechnen, Hinweis, Ergebnis, Blaettern, Rechenweg).</summary>
    internal sealed class Ansicht
    {
        private readonly SubnetKarte _karte;
        private readonly StackPanel _blaettern = new() { Spacing = 6, IsVisible = false };
        private readonly Dictionary<string, Button> _seitenKnoepfe = new(StringComparer.Ordinal);
        private readonly TextBlock _seiteText = RechnerTeile.Beschriftung("");
        private readonly TextBox? _sprung;
        private Aufteilung? _aufteilung;
        private Hostbedarf? _bedarf;
        private string? _netzdaten;
        private bool _gueltig;

        public Ansicht(SubnetKarte karte, string modus, string schluessel)
        {
            _karte = karte;
            Modus = modus;
            Schluessel = schluessel;
            var vorgabe = SUBNET_DEFAULTS.TryGetValue((modus, schluessel), out var v) ? v : default;
            Netz = RechnerTeile.Feld(schluessel == "netzdaten" ? (modus == "ipv4" ? "192.168.1.50/24" : "2001:db8:abcd:12::1/64") : vorgabe.Item1,
                SUBNET_FIELD_NET[modus]);
            Panel = new StackPanel { Spacing = 10 };
            Panel.Children.Add(RechnerTeile.Beschriftung(SUBNET_FIELD_NET[modus]));
            Panel.Children.Add(new LinksBuendig { Child = Netz.HoechstBreite(560) });
            RechnerSeite.EnterRechnet(Rechnen, Netz);
            if (schluessel == "aufteilen")
            {
                Zweites = RechnerTeile.Feld(vorgabe.Item2, SUBNET_FIELD_COUNT, zahl: true, breite: 110);
                RechnerSeite.EnterRechnet(Rechnen, Zweites);
                var zeile = RechnerTeile.Zeile(SUBNET_FIELD_COUNT, Zweites).HoechstBreite(420);
                zeile.HorizontalAlignment = HorizontalAlignment.Left;
                Panel.Children.Add(zeile);
            }
            else if (schluessel == "bedarf")
            {
                // Ein Subnetz je Zeile; Eingabetaste = neue Zeile, gerechnet wird nur mit dem Knopf
                Zweites = new TextBox { Text = vorgabe.Item2, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 110 }
                    .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
                Avalonia.Automation.AutomationProperties.SetName(Zweites, SUBNET_FIELD_NEEDS);
                Panel.Children.Add(RechnerTeile.Beschriftung(SUBNET_FIELD_NEEDS));
                Panel.Children.Add(new LinksBuendig { Child = Zweites.HoechstBreite(560) });
            }
            Panel.Children.Add(RechnerSeite.Knopf(RechnerTexte.Berechnen, Rechnen));
            Panel.Children.Add(Fehler);
            Panel.Children.Add(Kasten);
            if (schluessel == "aufteilen")
            {
                var knoepfe = new WrapPanel();
                foreach (var (wo, text) in new[] { ("first", SUBNET_PAGE_FIRST), ("back", SUBNET_PAGE_BACK), ("next", SUBNET_PAGE_NEXT), ("last", SUBNET_PAGE_LAST) })
                {
                    var knopf = LernTeile.Knopf(text);
                    knopf.Click += (_, _) => Blaettern(wo);
                    _seitenKnoepfe[wo] = knopf;
                    knoepfe.Children.Add(knopf);
                }
                _sprung = RechnerTeile.Feld("", SUBNET_PAGE_JUMP_FIELD, zahl: true, breite: 90);
                _sprung.PlaceholderText = SUBNET_PAGE_JUMP_FIELD;
                RechnerSeite.EnterRechnet(() => Springen(_sprung.Text), _sprung);
                var springen = LernTeile.Knopf(SUBNET_PAGE_JUMP);
                springen.Click += (_, _) => Springen(_sprung.Text);
                _seiteText.VerticalAlignment = VerticalAlignment.Center;
                _seiteText.Margin = new Thickness(0, 0, 16, 8);
                _sprung.Margin = new Thickness(0, 0, 8, 8);
                _blaettern.Children.Add(knoepfe);
                _blaettern.Children.Add(new WrapPanel { Children = { _seiteText, _sprung, springen } });
                Panel.Children.Add(_blaettern);
            }
            Weg = new Rechenwegteil(Erklaerung);
            Panel.Children.Add(Weg.Knopf);
            Panel.Children.Add(Weg.Kasten);
        }

        public string Modus { get; }

        public string Schluessel { get; }

        public StackPanel Panel { get; }

        public TextBox Netz { get; }

        /// <summary>Anzahl Subnetze bzw. Hostbedarf (Netzdaten: null).</summary>
        public TextBox? Zweites { get; }

        public Ergebniskasten Kasten { get; } = new();

        public Fehlerhinweis Fehler { get; } = new();

        public Rechenwegteil Weg { get; }

        public BigInteger SeiteNr { get; private set; } = 1;

        /// <summary>Text "Seite x von y" (leer, solange nicht geblaettert werden kann).</summary>
        public string SeiteText => _blaettern.IsVisible ? _seiteText.Text ?? "" : "";

        private int Version => Modus == "ipv4" ? 4 : 6;

        // -- Rechnen ---------------------------------------------------------------------------

        public void Rechnen()
        {
            try
            {
                switch (Schluessel)
                {
                    case "netzdaten":
                        _netzdaten = Subnetting.Netzdaten(Netz.Text, Modus);
                        break;
                    case "aufteilen":
                        _aufteilung = Subnetting.Aufteilen(Netz.Text, Zweites!.Text, Version);
                        SeiteNr = 1;
                        break;
                    default:
                        _bedarf = Subnetting.NachBedarf(Netz.Text, Zweites!.Text, 4);
                        break;
                }
            }
            catch (RechnerFehler f)
            {
                // B-D-04 (D-4): Kasten leer, Blaettern weg, Rechenweg zu (sein Text bleibt bei der
                // letzten gueltigen Rechnung), dann die Meldung als roter Hinweis
                _gueltig = false;
                Kasten.Setzen("");
                _blaettern.IsVisible = false;
                Weg.Zeigen(false);
                Fehler.Zeigen(f.Message);
                return;
            }
            _gueltig = true;
            Fehler.Zeigen(null);
            Anzeigen();
            if (Weg.Offen)
            {
                Weg.Zeigen(true);
            }
        }

        /// <summary>show: Ergebnis der Ansicht in den Kasten, ohne neu zu rechnen.</summary>
        public void Anzeigen()
        {
            if (!_gueltig)
            {
                return;
            }
            var schmal = _karte.Schmal;
            switch (Schluessel)
            {
                case "netzdaten":
                    Kasten.Setzen(_netzdaten!);
                    break;
                case "aufteilen":
                    Kasten.Setzen(Subnetting.AufteilenBericht(_aufteilung!, SeiteNr, schmal));
                    BlaetternZeichnen();
                    break;
                default:
                    Kasten.Setzen(Subnetting.BedarfBericht(_bedarf!, schmal));
                    break;
            }
        }

        /// <summary>Rechenweg zur letzten gueltigen Rechnung, vorher zur Vorgabe (explanation).</summary>
        private string Erklaerung()
        {
            var schmal = _karte.Schmal;
            if (Schluessel == "netzdaten")
            {
                return Modus == "ipv4"
                    ? (schmal ? CALC_EXPLAIN_SUBNET_HANDY : CALC_EXPLAIN_SUBNET)
                    : (schmal ? CALC_EXPLAIN_SUBNET_V6_HANDY : CALC_EXPLAIN_SUBNET_V6);
            }
            var (netz, zweites) = SUBNET_DEFAULTS[(Modus, Schluessel)];
            if (Schluessel == "aufteilen")
            {
                return Core.Rechner.Rechenweg.Aufteilen(_aufteilung ?? Subnetting.Aufteilen(netz, zweites, Version), schmal);
            }
            return Core.Rechner.Rechenweg.Bedarf(_bedarf ?? Subnetting.NachBedarf(netz, zweites, 4), schmal);
        }

        // -- Blaettern ---------------------------------------------------------------------------

        private void BlaetternZeichnen()
        {
            var seiten = _aufteilung!.Seiten;
            if (seiten <= 1)
            {
                _blaettern.IsVisible = false;
                return;
            }
            _seiteText.Text = Beschriftungen.Einsetzen(SUBNET_PAGE_OF, Zahlen.DeGanz(SeiteNr), Zahlen.DeGanz(seiten));
            _seitenKnoepfe["first"].IsEnabled = SeiteNr > 1;
            _seitenKnoepfe["back"].IsEnabled = SeiteNr > 1;
            _seitenKnoepfe["next"].IsEnabled = SeiteNr < seiten;
            _seitenKnoepfe["last"].IsEnabled = SeiteNr < seiten;
            _blaettern.IsVisible = true;
        }

        /// <summary>turn: erste, zurueck, weiter, letzte Seite (ohne neue Rechnung).</summary>
        public void Blaettern(string wo)
        {
            if (!_gueltig || _aufteilung is null)
            {
                return;
            }
            Gehe(wo switch
            {
                "first" => 1,
                "back" => SeiteNr - 1,
                "next" => SeiteNr + 1,
                _ => _aufteilung.Seiten,
            });
        }

        private void Gehe(BigInteger seite)
        {
            SeiteNr = BigInteger.Min(BigInteger.Max(seite, 1), _aufteilung!.Seiten);
            Anzeigen();
        }

        /// <summary>jump: Seitennummer lesen; Fehler als Hinweis, die Seite bleibt stehen.</summary>
        public void Springen(string? text)
        {
            if (!_gueltig || _aufteilung is null)
            {
                return;
            }
            BigInteger seite;
            try
            {
                seite = Zahlen.LesenGanz(text, SUBNET_PAGE_JUMP_FIELD);
            }
            catch (RechnerFehler f)
            {
                Fehler.Zeigen(f.Message);
                return;
            }
            Fehler.Zeigen(null);
            Gehe(seite);
        }
    }
}

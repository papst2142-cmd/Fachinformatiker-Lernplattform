using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.73.0 (Weisung P12-239, Sammelliste 335, Nico 13.11.2026 15:59 und 16:07 MEZ): selbst mit dem Bus fahren. Die Figur
/// geht wie zu jedem Punkt der Stadt zu einer Haltestelle und wartet dort. Haelt ein Bus der Linie dort, steht unten der Knopf
/// „Einsteigen“; danach sitzt sie im Bus, die Kamera zeigt den Bus von aussen und folgt ihm, unten steht „Aussteigen“. Nach dem
/// Druecken haelt der Bus an der naechsten Haltestelle und die Figur steht neben ihr; ohne Druecken faehrt sie weiter, Haltestelle
/// fuer Haltestelle. Der Bus ist der Bus der Darstellung (<see cref="Verkehr"/>), die Oberflaeche ruft je Bild
/// <see cref="MitfahrtTakt"/>. Die Fahrt kostet die Minuten der gefahrenen Strecke wie die Fahrt der Ortsliste
/// (<see cref="Busfahrt.MinutenJeZelleBus"/> je Zelle) und ist ein einziges Ereignis beim Aussteigen
/// (<see cref="Stadt.Mitfahrt"/>, vorhandene Art <see cref="Stadt.Gelaufen"/> mit <c>"mit": "bus"</c>), kostenlos. Ohne
/// Haltestellen (Fassung 0) und ohne 3D (kein Verkehr) aendert sich nichts.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>Welle A 0.73.0: Stufe der selbst gefahrenen Busfahrt.</summary>
    public enum MitfahrtStufe
    {
        /// <summary>Keine Fahrt (auch: die Figur wartet an einer Haltestelle).</summary>
        Keine,

        /// <summary>Die Figur geht vom Wartplatz zur Tuer des Busses (der Bus wartet).</summary>
        SteigtEin,

        /// <summary>Die Figur sitzt im Bus; die Kamera folgt dem Bus.</summary>
        ImBus,

        /// <summary>Die Figur geht von der Tuer zum Platz neben der Haltestelle (der Bus wartet).</summary>
        SteigtAus,
    }

    /// <summary>Aktion des Knopfs „Einsteigen“.</summary>
    public const string AktionEinsteigen = "bus_einsteigen";

    /// <summary>Aktion des Knopfs „Aussteigen“.</summary>
    public const string AktionAussteigen = "bus_aussteigen";

    /// <summary>Aktion des Knopfs „Hält an der nächsten Haltestelle“ (Tippen aendert nichts).</summary>
    public const string AktionHaeltGleich = "bus_haelt";

    /// <summary>
    /// So lange (Fahrzeit) wartet ein Bus hoechstens, wenn die Figur an seiner Haltestelle steht (sonst
    /// <see cref="Buslinie.HaltSekunden"/>): Zeit zum Tippen auch am Handy (E1).
    /// </summary>
    public const double WartetMitFigur = 15;

    /// <summary>Waehrend die Figur ein- oder aussteigt, steht der Bus mindestens noch so lange.</summary>
    private const double HaltenBeimSteigen = 0.25;

    /// <summary>Erst nach so viel Weg ab der Einstiegs-Haltestelle zaehlt sie wieder als Halt zum Aussteigen.</summary>
    private const float WiederAb = 0.5f;

    /// <summary>
    /// Wartplatz laengs zur Mitte der Haltestelle (quer: Mitte des Gehwegs). Weisung P12-240: 0,4 hinter der Mitte (0,76 vom Mast),
    /// neben der mittleren Tuer des haltenden Busses. Der Mast mit dem Schild (0,35 vor der Mitte) steht so deutlich ausserhalb
    /// des Kreises um die Figur und auch im Blick laengs des Gehwegs nicht hinter ihr (vorher 0,1 vor der Mitte: 0,275 vom Mast,
    /// der Kreis reicht 0,28 weit, im Bild stand der Mast scheinbar in der Figur; 0,15 hinter der Mitte reichte im Bild nicht).
    /// </summary>
    private const float PlatzLaengs = -0.4f;

    /// <summary>Tuer laengs zur Mitte der Haltestelle: neben der vorderen Tuer des haltenden Busses (Front am Schild).</summary>
    private const float TuerLaengs = 0.1f;

    /// <summary>Tuer: an der Bordsteinkante neben der vorderen Tuer des haltenden Busses.</summary>
    private const float TuerQuer = (StadtSzene.StrassenBreite / 2) + 0.02f;

    public MitfahrtStufe Mitfahrt { get; private set; }

    /// <summary>Haltestelle, an der die Figur gerade wartet (null = an keiner).</summary>
    public int? WartetAn { get; private set; }

    /// <summary>Nummer des Busses (im Verkehr), der an der Haltestelle der Figur haelt (Knopf „Einsteigen“), sonst null.</summary>
    public int? BusZumEinsteigen { get; private set; }

    /// <summary>„Aussteigen“ gedrueckt: der Bus haelt an der naechsten Haltestelle.</summary>
    public bool AussteigenGewuenscht { get; private set; }

    /// <summary>Die Figur ist mit dem Bus unterwegs (ein- oder aussteigend oder im Bus).</summary>
    public bool MitfahrtAktiv => Mitfahrt != MitfahrtStufe.Keine;

    /// <summary>Die Figur sitzt im Bus (sie selbst wird nicht gezeichnet, die Autos halten nicht fuer sie).</summary>
    public bool SitztImBus => Mitfahrt == MitfahrtStufe.ImBus;

    /// <summary>Zuletzt gefahrene Strecke (Feldmass) und ihre Minuten (Start-Test, Fotos).</summary>
    public (float Strecke, long Minuten, int Ein, int Aus)? LetzteMitfahrt { get; private set; }

    private int _mitBus;
    private int _einHalt;
    private float _gefahren;
    private float _letzterWeg;
    private int? _letzterHalt;
    private (int Bus, int Nr)? _verlaengert;
    private Vector2 _steigZiel;
    private int _ausHalt;
    private (int Bus, int Nr)? _ausgestiegen;

    /// <summary>Wo die Figur an der Haltestelle wartet und nach dem Aussteigen steht (Mitte des Gehwegs neben dem Schild).</summary>
    public static Vector2 Wartplatz(Haltestelle h) =>
        h.Mitte + (h.Fahrt * PlatzLaengs) + (h.Aussen * ((StadtSzene.StrassenBreite / 2 + h.Halb) / 2));

    /// <summary>Tuer des haltenden Busses (die Figur geht vom Wartplatz hierher und verschwindet im Bus).</summary>
    public static Vector2 Tuer(Haltestelle h) => h.Mitte + (h.Fahrt * TuerLaengs) + (h.Aussen * TuerQuer);

    /// <summary>
    /// Steht der Punkt an einer Haltestelle (auf ihrer Seite der Strasse, nicht auf der Fahrbahn, auf Hoehe des Haeuschens
    /// und des Schilds)? Dann ihre Nummer.
    /// </summary>
    public int? HaltestelleBei(double x, double z)
    {
        var p = new Vector2((float)x, (float)z);
        foreach (var h in Szene.Haltestellen)
        {
            var rel = p - h.Mitte;
            var quer = Vector2.Dot(rel, h.Aussen);
            var laengs = Vector2.Dot(rel, h.Fahrt);
            if (quer >= (StadtSzene.StrassenBreite / 2) - 0.02f && quer <= h.Halb + Haltestellen.HaeuschenAbstand + Haltestellen.HaeuschenTief + 0.3f
                && laengs >= -(Haltestellen.HaeuschenLang / 2) - 0.6f && laengs <= Haltestellen.SchildVor + 0.6f)
            {
                return h.Nr;
            }
        }
        return null;
    }

    /// <summary>
    /// Auf eine Haltestelle getippt (Haeuschen, Schild oder der Gehweg davor): Ziel ist ihr Wartplatz (ins Haeuschen selbst
    /// fuehrt kein Weg). Sonst der Punkt selbst.
    /// </summary>
    private (double X, double Z) ZielAnHaltestelle(double x, double z)
    {
        if (HaltestelleBei(x, z) is { } nr && Szene.Haltestellen.FirstOrDefault(h => h.Nr == nr) is { } h)
        {
            var w = Wartplatz(h);
            return (w.X, w.Y);
        }
        return (x, z);
    }

    /// <summary>Knoepfe der Fahrt (Aktion, Name); leer, wenn keine Fahrt und kein Bus zum Einsteigen.</summary>
    private List<(string Aktion, string Name)> MitfahrtKnoepfe() => Mitfahrt switch
    {
        MitfahrtStufe.Keine when BusZumEinsteigen is not null => [(AktionEinsteigen, SpielProbeTexte.Einsteigen)],
        MitfahrtStufe.ImBus when AussteigenGewuenscht => [(AktionHaeltGleich, SpielProbeTexte.HaeltGleich)],
        MitfahrtStufe.ImBus => [(AktionAussteigen, SpielProbeTexte.Aussteigen)],
        _ => [],
    };

    private bool MitfahrtAktion(string aktion)
    {
        switch (aktion)
        {
            case AktionEinsteigen:
                Einsteigen();
                return true;
            case AktionAussteigen:
                Aussteigen();
                return true;
            case AktionHaeltGleich:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Knopf „Einsteigen“: geht nur, solange ein Bus an der Haltestelle der Figur haelt.</summary>
    public bool Einsteigen() => Gemessen(() =>
    {
        if (Mitfahrt != MitfahrtStufe.Keine || BusZumEinsteigen is not { } bus || WartetAn is not { } nr
            || Szene.Haltestellen.FirstOrDefault(h => h.Nr == nr) is not { } h)
        {
            return false;
        }
        _mitBus = bus;
        _einHalt = nr;
        _letzterHalt = nr;
        _gefahren = 0;
        _letzterWeg = float.NaN;
        AussteigenGewuenscht = false;
        BusZumEinsteigen = null;
        _steigZiel = Tuer(h);
        Mitfahrt = MitfahrtStufe.SteigtEin;
        Knoepfe = [];
        Geaendert?.Invoke();
        return true;
    });

    /// <summary>Knopf „Aussteigen“: der Bus haelt an der naechsten Haltestelle (steht er gerade an einer, gleich dort).</summary>
    public bool Aussteigen() => Gemessen(() =>
    {
        if (Mitfahrt != MitfahrtStufe.ImBus || AussteigenGewuenscht)
        {
            return false;
        }
        AussteigenGewuenscht = true;
        Zeile = SpielProbeTexte.BusHaeltGleich;
        Knoepfe = MitfahrtKnoepfe();
        Geaendert?.Invoke();
        return true;
    });

    /// <summary>
    /// Je Bild nach dem Verkehr (<paramref name="verkehr"/> = der gezeichnete, null = keiner, z. B. innen oder ohne 3D):
    /// Bus an der Haltestelle der Figur erkennen, Ein- und Aussteigen gehen, im Bus der Lage des Busses folgen.
    /// </summary>
    public void MitfahrtTakt(Verkehr? verkehr, double sekunden)
    {
        _imBild = true;
        try
        {
            TaktOhneMessung(verkehr, sekunden);
        }
        finally
        {
            _imBild = false;
        }
    }

    private void TaktOhneMessung(Verkehr? verkehr, double sekunden)
    {
        var bus = verkehr?.Autos.FirstOrDefault(a => a.Nummer == _mitBus && a.Nummer >= Buslinie.ErsteNummer);
        switch (Mitfahrt)
        {
            case MitfahrtStufe.Keine:
                HaltestellePruefen(verkehr);
                return;
            case MitfahrtStufe.SteigtEin:
                if (bus is null)
                {
                    // der Bus ist weg (anderer Verkehr): die Figur bleibt an der Haltestelle
                    Mitfahrt = MitfahrtStufe.Keine;
                    HaltestellePruefen(verkehr);
                    return;
                }
                bus.HaltRest = Math.Max(bus.HaltRest, HaltenBeimSteigen);
                bus.Haelt = true;
                if (Gehen(sekunden))
                {
                    Mitfahrt = MitfahrtStufe.ImBus;
                    WartetAn = null;
                    _letzterWeg = bus.Weg;
                    ImBusFolgen(bus);
                    Zeile = SpielProbeTexte.ImBusUnterwegs;
                    Knoepfe = MitfahrtKnoepfe();
                    Geaendert?.Invoke();
                }
                return;
            case MitfahrtStufe.ImBus:
                if (bus is null)
                {
                    // der Bus faehrt nicht mehr (z. B. Verkehr der Festzeit): an der naechsten Haltestelle aussteigen
                    var naechste = Szene.Haltestellen.OrderBy(h => Vector2.Distance(h.Mitte, new Vector2(FigurX, FigurZ))).FirstOrDefault();
                    if (naechste is null)
                    {
                        Mitfahrt = MitfahrtStufe.Keine;
                        return;
                    }
                    AussteigenAn(naechste, null);
                    return;
                }
                if (!float.IsNaN(_letzterWeg))
                {
                    // Weg seit dem letzten Bild ueber den Rundlauf hinweg; beim Halt setzt der Verkehr die Mitte auf das Schild
                    // zurueck (wenige Hundertstel), das ist keine ganze Runde
                    var d = ((bus.Weg - _letzterWeg) % bus.Laenge + bus.Laenge) % bus.Laenge;
                    _gefahren += d > bus.Laenge / 2 ? d - bus.Laenge : d;
                }
                _letzterWeg = bus.Weg;
                ImBusFolgen(bus);
                if (bus.HaeltAn is { } nr)
                {
                    var neu = nr != _letzterHalt || _gefahren >= WiederAb;
                    _letzterHalt = nr;
                    if (AussteigenGewuenscht && neu && (nr != _einHalt || _gefahren >= WiederAb)
                        && Szene.Haltestellen.FirstOrDefault(h => h.Nr == nr) is { } halt)
                    {
                        AussteigenAn(halt, bus);
                    }
                }
                else
                {
                    _letzterHalt = null;
                }
                return;
            case MitfahrtStufe.SteigtAus:
                if (bus is not null)
                {
                    bus.HaltRest = Math.Max(bus.HaltRest, HaltenBeimSteigen);
                    bus.Haelt = true;
                }
                if (Gehen(sekunden))
                {
                    Mitfahrt = MitfahrtStufe.Keine;
                    // derselbe Bus bietet an dieser Haltestelle nicht gleich wieder „Einsteigen“ an (und wartet nicht laenger);
                    // die Zeile „Ausgestiegen …“ bleibt stehen
                    _ausgestiegen = (_mitBus, _ausHalt);
                    _verlaengert = (_mitBus, _ausHalt);
                    WartetAn = _ausHalt;
                    _mitBus = 0;
                    ImFreien = true;
                    HaltestellePruefen(verkehr);
                }
                return;
        }
    }

    /// <summary>Wartet die Figur an einer Haltestelle? Haelt dort ein Bus, Knopf „Einsteigen“ (und der Bus wartet laenger).</summary>
    private void HaltestellePruefen(Verkehr? verkehr)
    {
        int? an = null;
        int? bus = null;
        if (verkehr is not null && !ImInnenraum && ImFreien && !Geht && Etage == 0 && !MitBusUnterwegs && Szene.Haltestellen.Count > 0)
        {
            an = HaltestelleBei(FigurX, FigurZ);
            if (an is { } nr && verkehr.Autos.FirstOrDefault(a => a.Nummer >= Buslinie.ErsteNummer && a.HaeltAn == nr && a.HaltRest > 0
                && _ausgestiegen != (a.Nummer, nr)) is { } b)
            {
                bus = b.Nummer;
                if (_verlaengert != (b.Nummer, nr))
                {
                    // E1: steht die Figur an der Haltestelle, wartet der Bus hoechstens WartetMitFigur statt HaltSekunden
                    _verlaengert = (b.Nummer, nr);
                    b.HaltRest += WartetMitFigur - Buslinie.HaltSekunden;
                }
            }
        }
        if (verkehr?.Autos.Any(a => _verlaengert is { } v && a.Nummer == v.Bus && a.HaeltAn == v.Nr) != true)
        {
            _verlaengert = null;
        }
        if (verkehr?.Autos.Any(a => _ausgestiegen is { } v && a.Nummer == v.Bus && a.HaeltAn == v.Nr) != true)
        {
            _ausgestiegen = null;
        }
        if (an == WartetAn && bus == BusZumEinsteigen)
        {
            return;
        }
        var vorher = WartetAn;
        WartetAn = an;
        BusZumEinsteigen = bus;
        if (an is null)
        {
            if (vorher is not null && Knoepfe.Any(k => k.Aktion == AktionEinsteigen))
            {
                // weggegangen: der Knopf faellt weg (E1: sonst nichts)
                Knoepfe = [];
            }
            Geaendert?.Invoke();
            return;
        }
        Zeile = bus is null ? SpielProbeTexte.AnDerHaltestelle : SpielProbeTexte.BusIstDa;
        Knoepfe = MitfahrtKnoepfe();
        Geaendert?.Invoke();
    }

    /// <summary>Die Figur geht ein Stueck zum Ziel des Ein- oder Aussteigens; true = angekommen.</summary>
    private bool Gehen(double sekunden)
    {
        var rest = sekunden * Tempo;
        var dx = _steigZiel.X - FigurX;
        var dz = _steigZiel.Y - FigurZ;
        var weite = Math.Sqrt((dx * dx) + (dz * dz));
        if (weite > 1e-6)
        {
            FigurRichtung = (float)(Math.Atan2(dx, dz) * 180 / Math.PI);
        }
        if (weite <= rest)
        {
            (FigurX, FigurZ) = (_steigZiel.X, _steigZiel.Y);
            Schrittweg += weite;
            return true;
        }
        FigurX += (float)(dx / weite * rest);
        FigurZ += (float)(dz / weite * rest);
        Schrittweg += rest;
        return false;
    }

    private void ImBusFolgen(Verkehr.Auto bus)
    {
        var (ort, richtung) = bus.Lage(bus.Weg);
        (FigurX, FigurZ) = (ort.X, ort.Y);
        FigurRichtung = MathF.Atan2(richtung.X, richtung.Y) * 180f / MathF.PI;
    }

    /// <summary>An Haltestelle <paramref name="h"/> aussteigen: Fahrt schreiben, an der Tuer erscheinen, zum Wartplatz gehen.</summary>
    private void AussteigenAn(Haltestelle h, Verkehr.Auto? bus)
    {
        var minuten = Math.Max(1L, (long)Math.Ceiling((_gefahren / Stadt.Zelle * Busfahrt.MinutenJeZelleBus) - 1e-9));
        _ausHalt = h.Nr;
        var ergebnis = Schreiben(() => Stadt.Mitfahrt(Ablauf, minuten, _einHalt, h.Nr));
        LetzteMitfahrt = (_gefahren, minuten, _einHalt, h.Nr);
        var tuer = Tuer(h);
        (FigurX, FigurZ) = (tuer.X, tuer.Y);
        _steigZiel = Wartplatz(h);
        Mitfahrt = bus is null ? MitfahrtStufe.Keine : MitfahrtStufe.SteigtAus;
        if (bus is null)
        {
            (FigurX, FigurZ) = (_steigZiel.X, _steigZiel.Y);
            _mitBus = 0;
            ImFreien = true;
        }
        AussteigenGewuenscht = false;
        Knoepfe = [];
        Zeile = ergebnis is null ? Zeile
            : ergebnis.Ok ? MitUhrHinweis(string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.Ausgestiegen, ergebnis.Daten!["minuten"]))
            : StadtTexte.Grund(ergebnis.Grund);
        Geaendert?.Invoke();
    }

    /// <summary>
    /// Nur Start-Test (Layout je Schriftstufe und Groesse): Zeile und Knopf eines Zustands der Fahrt zeigen, ohne die Fahrt zu
    /// aendern (<paramref name="aktion"/> = <see cref="AktionEinsteigen"/>, <see cref="AktionAussteigen"/> oder
    /// <see cref="AktionHaeltGleich"/>). Echtes Ein- und Aussteigen prueft der Start-Test einmal mit echtem Tippen.
    /// </summary>
    public void KnopfZeigenFuerTests(string aktion)
    {
        (Zeile, Knoepfe) = aktion switch
        {
            AktionEinsteigen => (SpielProbeTexte.BusIstDa, new List<(string, string)> { (AktionEinsteigen, SpielProbeTexte.Einsteigen) }),
            AktionAussteigen => (SpielProbeTexte.ImBusUnterwegs, [(AktionAussteigen, SpielProbeTexte.Aussteigen)]),
            _ => (SpielProbeTexte.BusHaeltGleich, [(AktionHaeltGleich, SpielProbeTexte.HaeltGleich)]),
        };
        Geaendert?.Invoke();
    }

    private static readonly AsyncLocal<bool> SperreAus = new();

    /// <summary>Nur Gegenprobe der Belastungsprobe: waehrend der Fahrt gehen Tippen und Ortsliste wie ohne Fahrt (Fehler mit Absicht).</summary>
    public static bool MitfahrtSperreAusFuerTests
    {
        get => SperreAus.Value;
        set => SperreAus.Value = value;
    }

    /// <summary>Waehrend der Fahrt: andere Aktionen (Tippen, Ortsliste) gehen nicht; Zeile mit dem Hinweis.</summary>
    private bool ErstAussteigen()
    {
        if (!MitfahrtAktiv || SperreAus.Value)
        {
            return false;
        }
        Zeile = SpielProbeTexte.ErstAussteigen;
        Geaendert?.Invoke();
        return true;
    }
}

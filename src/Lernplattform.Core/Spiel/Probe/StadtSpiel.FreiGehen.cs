using System.Globalization;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.95.0 (Sammelliste 385 „frei gehen“, Weisung P12-306, Nico 1c 2a 3a 4a 5a): die Figur geht mit gehaltenen Tasten
/// (W/A/S/D am PC, spaeter der Stick am Handy) frei durch die Stadt und die Innenraeume. Die Rechnung der Richtung und des
/// Schritts mit Gleiten macht der Engine-Baustein <see cref="Steuerung"/>; hier steht nur das Spielwissen:
/// <list type="bullet">
/// <item>Sperre draussen wie die Wegsuche (Laufraster mit feiner Sperre: Haeuser, Wasser, Fahrbahn und Gleis nur an Uebergaengen,
/// Regel 342), dazu Questgeber (stehen bleiben auf <see cref="VorDemQuestgeber"/>) und noch nicht geladene Kacheln;
/// drinnen das Raster der Wege (Waende, Moebel, Tresen) und Personen (nie naeher als <see cref="AnsprechAbstand"/>).</item>
/// <item>Spielzeit (Antwort 3a): draussen wird die gegangene Strecke gezaehlt; steht die Figur (keine Taste, <see cref="HaltNach"/>
/// still) oder passiert etwas anderes, schreibt das Spiel EIN Ereignis „gelaufen“ (<see cref="Stadt.Laufen"/>) mit den
/// Schritten wie beim Antippen (Strecke in Rasterfeldern, aufgerundet). Steht sie dabei vor einer Tuer (<see cref="VorDerTuer"/>),
/// ist es stattdessen der Weg zu diesem Ort (<see cref="Stadt.Gehen"/> mit denselben Schritten): die Tafel zeigt den Ort wie nach
/// dem Antippen, „Hineingehen“ geht. Drinnen kostet Gehen keine Zeit. Keine neue Ereignisart, der Spiel-Kern bleibt unberuehrt.</item>
/// <item>Ein angetippter Weg endet bei einer Gehtaste an der Stelle der Figur (geschriebene Schritte zaehlen); sitzend oder
/// liegend steht sie auf, Ducken und Kriechen enden; im Bus und auf dem Weg zum Bus geht nichts.</item>
/// </list>
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>So lange (Sekunden) ohne Gehtaste, bis der Halt das Ereignis schreibt (kurzer Wechsel der Taste ist kein Halt).</summary>
    public const double HaltNach = 0.3;

    /// <summary>Draussen rastet die Figur an einem Uebergang hoechstens so weit seitlich ein (Kurzplan P12-187 Abschnitt 8).</summary>
    public const double EinrastenDraussen = 0.1;

    /// <summary>Steht die Figur beim Halt hoechstens so weit vom Stehplatz eines Orts, steht sie vor dessen Tuer.</summary>
    public const double VorDerTuer = 0.5;

    /// <summary>Bis zu dieser Weite nennt die Ansage beim Halt den naechsten Ort („Vor dir: …“).</summary>
    public const double AnsageWeite = 3.0;

    private readonly Steuerung.Zustand _steuerung = new();

    /// <summary>Draussen frei gegangene Strecke seit dem letzten Halt (noch nicht als Ereignis geschrieben).</summary>
    private double _freiStrecke;

    private double _freiStill;

    private bool _freiBewegt;

    private bool _angestossenGesagt;

    /// <summary>Die Figur geht gerade frei (Taste gehalten und sie kommt voran): Bewegungsbilder und Kamera wie beim Gehen.</summary>
    public bool FreiUnterwegs { get; private set; }

    /// <summary>Frei gegangen, der Halt (Ereignis) steht noch aus.</summary>
    public bool FreiOffen => _freiBewegt;

    /// <summary>Ansagen fuer den Screenreader beim freien Gehen (Anstossen einmal je Halt, beim Halt der Ort davor).</summary>
    public event Action<string>? GehAnsage;

    /// <summary>
    /// Nachladen (grosse Karte): ist die Stelle auf einer geladenen Kachel? null = alles geladen (setzt die Darstellung).
    /// Eine nicht geladene Kachel zaehlt beim freien Gehen als gesperrt.
    /// </summary>
    public Func<double, double, bool>? GeladenAn { get; set; }

    private static readonly AsyncLocal<bool> FreiAlt = new();

    /// <summary>Nur Gegenprobe: freies Gehen ohne jede Sperre (Figur geht durch Waende, ueber die Fahrbahn, in Personen).</summary>
    public static bool FreiGehenAltFuerTests { get => FreiAlt.Value; set => FreiAlt.Value = value; }

    private static readonly AsyncLocal<bool> HaltAlt = new();

    /// <summary>Nur Gegenprobe: jedes Bild mit Weg schreibt sofort sein eigenes Ereignis (statt eines je Halt).</summary>
    public static bool FreiHaltAltFuerTests { get => HaltAlt.Value; set => HaltAlt.Value = value; }

    /// <summary>
    /// Je Bild: frei gehen in Richtung (<paramref name="rx"/>, <paramref name="rz"/>) am Boden (Laenge hoechstens 1, aus
    /// <see cref="Steuerung.Richtung"/>; 0 = keine Gehtaste). <paramref name="blick"/> (Grad wie <see cref="FigurRichtung"/>)
    /// dreht die Figur in der Ich-Sicht in den Blick statt in die Gehrichtung. true = es ist noch etwas zu tun (Bild noetig).
    /// </summary>
    public bool FreiGehen(double rx, double rz, double sekunden, float? blick = null)
    {
        _imBild = true;
        try
        {
            return FreiGehenOhneMessung(rx, rz, sekunden, blick);
        }
        finally
        {
            _imBild = false;
        }
    }

    private bool FreiGehenOhneMessung(double rx, double rz, double sekunden, float? blick)
    {
        if (Math.Abs(rx) + Math.Abs(rz) < 1e-9)
        {
            FreiUnterwegs = false;
            if (!_freiBewegt)
            {
                _angestossenGesagt = false;
                return false;
            }
            _freiStill += Math.Max(0, sekunden);
            if (_freiStill >= HaltNach)
            {
                FreiHalt();
                return false;
            }
            return true;
        }
        _freiStill = 0;
        if (MitfahrtAktiv)
        {
            // im Bus (selbst gefahren) geht kein freies Gehen; der Hinweis steht einmal in der Zeile
            if (Zeile != SpielProbeTexte.ErstAussteigen)
            {
                ErstAussteigen();
            }
            return false;
        }
        if (MitBusUnterwegs || Einrichten)
        {
            return false;
        }
        Aufstehen();
        _koerper = Koerperhaltung.Stehen;
        if (Geht)
        {
            // ein angetippter Weg endet hier (die geschriebenen Schritte zaehlen); draussen steht die Figur dann im Freien
            WegLeeren();
            _danach = null;
            _danachZu = null;
            _setzenNach = null;
            if (!ImInnenraum && _zielOrt is not null)
            {
                _zielOrt = null;
                ImFreien = true;
                Knoepfe = [];
            }
        }
        var draussen = !ImInnenraum;
        _steuerung.Neu((rx, rz), draussen ? EinrastenDraussen : 0);
        var (vonX, vonZ) = ((double)FigurX, (double)FigurZ);
        var schritt = Steuerung.Schritt(vonX, vonZ, rx, rz, Math.Max(0, sekunden) * Tempo, (x, z) => Frei(x, z, vonX, vonZ), _steuerung,
            draussen ? Szene.AnUebergang : null);
        FigurRichtung = blick ?? (float)(Math.Atan2(rx, rz) * 180 / Math.PI);
        if (schritt.Weg <= 1e-9)
        {
            FreiUnterwegs = false;
            if (schritt.Angestossen && !_angestossenGesagt)
            {
                _angestossenGesagt = true;
                GehAnsage?.Invoke(SpielProbeTexte.NichtWeiter);
            }
            return _freiBewegt;
        }
        var halbVorher = Math.Floor(Schrittweg / (Zyklus / 2));
        FigurX = (float)schritt.X;
        FigurZ = (float)schritt.Z;
        Schrittweg += schritt.Weg;
        if (Ton is not null && Math.Floor(Schrittweg / (Zyklus / 2)) != halbVorher)
        {
            Ton.Invoke(new Tonanlass(Anlass.Schritt, Boden));
        }
        FreiUnterwegs = true;
        if (!_freiBewegt)
        {
            _freiBewegt = true;
            if (draussen && !ImFreien)
            {
                // vom Stehplatz eines Orts losgegangen: jetzt im Freien (die Tafel des Orts faellt weg)
                ImFreien = true;
                Knoepfe = [];
                Zeile = SpielProbeTexte.ImFreien;
                Geaendert?.Invoke();
            }
        }
        if (draussen)
        {
            _freiStrecke += schritt.Weg;
            if (HaltAlt.Value || Math.Ceiling(_freiStrecke / Stadt.Zelle) * Stadt.MinutenJeSchritt >= Stadt.TagMinuten / 2)
            {
                // sehr lange ohne Halt (oder Gegenprobe): zwischendurch schreiben, die Figur geht weiter
                FreiHalt(ohneTuer: true);
                _freiBewegt = true;
                FreiUnterwegs = true;
            }
        }
        return true;
    }

    /// <summary>Darf die Figur von (<paramref name="vonX"/>, <paramref name="vonZ"/>) aus auf (x, z) stehen?</summary>
    private bool Frei(double x, double z, double vonX, double vonZ)
    {
        if (FreiAlt.Value)
        {
            return true;
        }
        if (Innen is { } innen)
        {
            return FreiDrinnen(innen, x, z, vonX, vonZ);
        }
        if (GeladenAn is { } geladen && !geladen(x, z))
        {
            return false;
        }
        if (!Szene.Laufraster.KannStehen(x, z) || Szene.Laufraster.FeinGesperrt(x, z))
        {
            return false;
        }
        foreach (var q in Szene.Questgeber)
        {
            var neu = Abstand(x, z, q.X, q.Z);
            if (neu < VorDemQuestgeber && neu < Abstand(vonX, vonZ, q.X, q.Z))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Drinnen frei: das Raster der Wege (Waende, Moebel, Tresen, Personen) und keiner Person naeher als
    /// <see cref="AnsprechAbstand"/> (wer schon naeher steht, darf nur weg von ihr).
    /// </summary>
    internal static bool FreiDrinnen(Innenraum innen, double x, double z, double vonX, double vonZ)
    {
        if (!Wege.KannStehen(x, z, innen.Lauf))
        {
            return false;
        }
        foreach (var p in innen.Personen)
        {
            var neu = Abstand(x, z, p.X, p.Z);
            if (neu < AnsprechAbstand && neu < Abstand(vonX, vonZ, p.X, p.Z))
            {
                return false;
            }
        }
        return true;
    }

    private static double Abstand(double ax, double az, double bx, double bz) => Math.Sqrt(((ax - bx) * (ax - bx)) + ((az - bz) * (az - bz)));

    /// <summary>
    /// Halt: das frei Gegangene abschliessen (vor jeder anderen Aktion und wenn die Figur steht). Draussen genau ein Ereignis
    /// (gelaufen, oder vor einer Tuer der Weg zu diesem Ort), drinnen nur Text und Knoepfe zum Platz. Nichts frei gegangen: nichts.
    /// </summary>
    public void FreiHalt() => FreiHalt(ohneTuer: false);

    private void FreiHalt(bool ohneTuer)
    {
        if (!_freiBewegt)
        {
            return;
        }
        _freiBewegt = false;
        FreiUnterwegs = false;
        _freiStill = 0;
        _angestossenGesagt = false;
        if (ImInnenraum)
        {
            _freiStrecke = 0;
            InnenText();
            Geaendert?.Invoke();
            return;
        }
        var strecke = _freiStrecke;
        _freiStrecke = 0;
        if (!ohneTuer && TuerNahe() is { } tuer)
        {
            // vor einer Tuer stehen geblieben: der Weg zu diesem Ort mit allen Schritten (ein Ereignis), dann wie nach dem Antippen
            if (GeheZuOrt(tuer.Id, strecke) == StadtGrund.Ok)
            {
                GehAnsage?.Invoke(string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.VorDir, tuer.Name));
                return;
            }
        }
        if (strecke <= 1e-9)
        {
            return;
        }
        var schritte = Math.Max(1, (long)Math.Ceiling((strecke / Stadt.Zelle) - 1e-9));
        var ergebnis = Schreiben(() => Stadt.Laufen(Ablauf, schritte));
        if (ergebnis is null)
        {
            return;
        }
        if (!ergebnis.Ok)
        {
            Zeile = StadtTexte.Grund(ergebnis.Grund);
            Geaendert?.Invoke();
            return;
        }
        if (ohneTuer)
        {
            return;
        }
        if (!QuestgeberImFreien())
        {
            Zeile = MitUhrHinweis(SpielProbeTexte.ImFreien);
            Knoepfe = [];
        }
        if (Szene.Orte.Select(o => (o, a: Abstand(FigurX, FigurZ, o.StehX, o.StehZ))).Where(t => t.a <= AnsageWeite).OrderBy(t => t.a)
                .Select(t => t.o).FirstOrDefault() is { } nah)
        {
            GehAnsage?.Invoke(string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.VorDir, nah.Name));
        }
        Geaendert?.Invoke();
    }

    /// <summary>Der Ort, vor dessen Tuer die Figur steht (Stehplatz hoechstens <see cref="VorDerTuer"/> weit, der naechste), sonst null.</summary>
    private StadtOrt? TuerNahe() =>
        Szene.Orte.Select(o => (o, a: Abstand(FigurX, FigurZ, o.StehX, o.StehZ))).Where(t => t.a <= VorDerTuer).OrderBy(t => t.a)
            .Select(t => t.o).FirstOrDefault();

    /// <summary>
    /// Steht die Figur hoechstens <see cref="VorDerTuer"/> vor dem Stehplatz des Orts und ist die gerade Linie dorthin frei
    /// (Laufraster mit feiner Sperre, je <see cref="Steuerung.Teilschritt"/>)?
    /// </summary>
    private bool GeradeVorDieTuer(StadtOrt ort)
    {
        var a = Abstand(FigurX, FigurZ, ort.StehX, ort.StehZ);
        if (a > VorDerTuer)
        {
            return false;
        }
        var teile = Math.Max(1, (int)Math.Ceiling(a / Steuerung.Teilschritt));
        for (var i = 1; i <= teile; i++)
        {
            var x = FigurX + ((ort.StehX - FigurX) * i / teile);
            var z = FigurZ + ((ort.StehZ - FigurZ) * i / teile);
            if (!Szene.Laufraster.KannStehen(x, z) || Szene.Laufraster.FeinGesperrt(x, z))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Vor dem Hineingehen nach freiem Gehen: den restlichen kurzen Weg vor die Tuer (hoechstens <see cref="VorDerTuer"/>) gleich
    /// zu Ende gehen, damit „Hineingehen“ sofort geht.
    /// </summary>
    private void TuerWegBeenden()
    {
        FreiHalt();
        if (Geht && !ImInnenraum && _zielOrt is not null && !MitBusUnterwegs
            && Wegsuche.Laenge((FigurX, FigurZ), _weg) <= VorDerTuer + 0.2)
        {
            SofortAnkommen();
        }
    }

    /// <summary>Nur Tests: die Figur draussen ohne Weg und ohne Ereignis an die Stelle setzen (im Freien).</summary>
    internal void StelleFuerTests(double x, double z)
    {
        WegLeeren();
        _zielOrt = null;
        FigurX = (float)x;
        FigurZ = (float)z;
        ImFreien = !ImInnenraum;
    }

    /// <summary>Alle laufenden Spiele: vor dem Programmende und im Hintergrund (Android) den Halt schreiben.</summary>
    private static readonly List<WeakReference<StadtSpiel>> Alle = [];

    private void Anmelden()
    {
        lock (Alle)
        {
            Alle.RemoveAll(w => !w.TryGetTarget(out _));
            Alle.Add(new WeakReference<StadtSpiel>(this));
        }
    }

    /// <summary>Den Halt aller laufenden Spiele schreiben (nur im Faden der Oberflaeche aufrufen).</summary>
    public static void AlleFreiHalten()
    {
        List<StadtSpiel> lebend;
        lock (Alle)
        {
            lebend = [.. Alle.Select(w => w.TryGetTarget(out var s) ? s : null).OfType<StadtSpiel>()];
        }
        foreach (var spiel in lebend)
        {
            spiel.FreiHalt();
        }
    }
}

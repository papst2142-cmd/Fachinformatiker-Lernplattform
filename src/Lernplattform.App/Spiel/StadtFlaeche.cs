using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W1 (Stichprobe, Plan Abschnitt 3 Technik A), ab W4 die ganze Stadt: 3D-Flaeche mit
/// Avalonias eingebautem <see cref="OpenGlControlBase"/> und eigener kleiner Darstellung (OpenGL ES 3.0
/// bzw. OpenGL 3.x, Matrizen aus System.Numerics, keine Fremdbibliothek). Zeichnet die Stadt oder das
/// Buero aus <see cref="StadtSpiel"/>, die Figur und die Zielmarke.
/// Bedienung (Weisung P7-9 Punkt 3a): Tippen/Klick auf ein Haus geht hin (im Buero: zum Bodenpunkt),
/// Ziehen verschiebt die Ansicht, zwei Finger zoomen und drehen, Mausrad zoomt, Pfeiltasten
/// verschieben, Q/E drehen, +/- zoomen, Eingabe geht hinein. Welle A 0.80.0 (Weisung P12-268): im Spiel draussen
/// (<see cref="FesteKamera"/>) feste Kamera nach <see cref="SpielKamera"/>, Regeln in <see cref="Kamerabedienung"/>. Die Flaeche faengt jeden Zeiger selbst
/// (Capture) und liegt nie in einer ScrollView, sonst nimmt die Seite die Geste (Fehler S24, W1).
/// Treffer zaehlt die ganze Flaeche (<see cref="ICustomHitTest"/>): das GL-Bild selbst ist fuer
/// Avalonia kein gezeichneter Inhalt, sonst ginge ein Finger an der Flaeche vorbei an den Rahmen.
/// W5: Innenraeume (<see cref="Innenraum"/>), Figuren aus dem CC0-Modell mit Aussehen, Personen mit
/// "!" bei einem Auftrag, Vorschau beim Einrichten; steht die Figur hinter einem Haus, wird es halb
/// durchsichtig gezeichnet (Rasterung im Pixel-Shader, ohne Mischen: Sammelliste 102).
/// </summary>
public sealed partial class StadtFlaeche : OpenGlControlBase, ICustomHitTest
{
    private const int Lagen = Netz.Zahlen * sizeof(float);
    // in GlConsts nicht enthalten (Werte aus der OpenGL-Spezifikation)
    private const int GlLequal = 0x0203;
    private const int GlUnsignedInt = 0x1405;
    private const double ZiehenAb = 8;

    private StadtSpiel? _spiel;
    private readonly Dictionary<string, Kamera> _kameras = [];
    private bool _szeneNeu;
    private readonly Dictionary<string, GlNetz> _flaechen = [];
    private readonly Dictionary<StadtHaus, GlNetz> _haeuser = [];

    // Welle D1 (Weisung P12-143): Fernform je Haus (Teil B) und Kugel je Haus fuer den Sichtkegel (Teil A)
    private readonly Dictionary<StadtHaus, GlNetz> _haeuserFern = [];
    private readonly Dictionary<StadtHaus, (System.Numerics.Vector3 Mitte, float Halb)> _hausKugel = [];

    /// <summary>Welle D1: Schluessel der Fernform des Stadtnetzes (Fest-Deko vereinfacht, Teil C) in den Flaechen.</summary>
    private const string StadtFern = ProbeWelt.Stadt + "|fern";

    /// <summary>
    /// Welle D1 (Teil A): nur fuer die Gegenprobe der Tests und Messungen: Haeuser ausserhalb des Bilds werden wieder
    /// gezeichnet und die Fernformen nie genommen (Verhalten vor D1, ohne die Unterseiten am Boden).
    /// </summary>
    public static bool OhneSparenFuerTests { get; set; }
    private readonly Dictionary<string, GlNetz> _figuren = [];
    private GlNetz? _innen;
    private Dictionary<InnenWand, GlNetz> _innenWaende = [];
    // Weisung P8-55 (W12): jeder hochgeladene Innenraum bleibt in der Grafikkarte, solange er im Vorrat des Spiels ist
    private readonly Dictionary<Innenraum, (GlNetz Netz, Dictionary<InnenWand, GlNetz> Waende)> _innenGl = [];
    // Weisung P8-55 (W12): gebaute Netze der Figuren (Prozessor), bleiben ueber den Verlust des GL-Zusammenhangs
    private readonly ConcurrentDictionary<string, Netz> _figurenCpu = new(StringComparer.Ordinal);
    private readonly HashSet<InnenWand> _durchsichtig = [];
    private readonly HashSet<StadtHaus> _verdeckt = [];
    private readonly Dictionary<string, (PyObjekt? Aussehen, IReadOnlyList<Figur.Stueck>? Kleidung, GlNetz Netz)> _figurZuletzt = [];
    private PyObjekt? _ringAussehen;
    private Innenraum? _innenGeladen;

    /// <summary>Welle A 0.92.0 (Weisung P12-301): Regeln der Ich-Sicht (Auge, Gehrichtung, Pause), je Bild aufgerufen.</summary>
    private readonly IchSicht _ich = new();

    /// <summary>Welle A 0.92.0: Decke des Innenraums in der Ich-Sicht (einmal je Innenraum geladen).</summary>
    private (Innenraum Raum, GlNetz Netz)? _decke;

    /// <summary>Welle A 0.92.0: war die Ich-Sicht im letzten Bild ganz an (fuer die Ansage an/aus)?</summary>
    private bool _ichWarAn;

    /// <summary>Welle A 0.92.0: Regeln der Ich-Sicht dieser Flaeche (Start-Test).</summary>
    public IchSicht IchRegeln => _ich;

    /// <summary>
    /// Welle A 0.92.0: Ich-Sicht ganz an oder aus (true/false) und der Text fuer den Screenreader (an/aus und Blickrichtung);
    /// auch bei jedem Wechsel der Blickrichtung in der Ich-Sicht (acht Richtungen).
    /// </summary>
    public event Action<bool, string>? IchAnsage;

    /// <summary>Welle A 0.92.0: Ich-Sicht ein- oder ausschalten (Menue, Taste); false = geht hier nicht (kein Spiel).</summary>
    public bool IchUmschalten()
    {
        if (AktuelleKamera is not { } kamera || !SpielKamera.IchUmschalten(kamera))
        {
            return false;
        }
        AnfordernBild();
        return true;
    }

    /// <summary>Welle A 0.92.0: ist die Ich-Sicht an oder auf dem Weg hinein?</summary>
    public bool IchAn => AktuelleKamera is { } k && (SpielKamera.ImIch(k) || _ich.Pausiert);

    /// <summary>Welle A 0.92.0: zeichnet die Flaeche gerade nah (Figur ohne Kopf, nichts halb durchsichtig, drinnen Decke)?</summary>
    public static bool IchNah(Kamera k) => k.IchAnteil >= IchSicht.NahAb;

    /// <summary>Welle A 0.92.0 (nur Start-Test, Gegenprobe): Ich-Sicht auf grossen Karten ohne Sichtweite und Fernformen.</summary>
    public static bool IchSichtweiteAusFuerTests { get; set; }

    /// <summary>Welle A 0.92.0 (Start-Test): das letzte Bild drinnen zeichnete die Decke der Ich-Sicht.</summary>
    public bool LetzteIchDecke { get; private set; }
    private bool _innenNeu;
    private GlNetz? _markierung;
    private string _markierungSchluessel = "";
    private GlNetz? _ausruf;
    // Phase 12 Welle 2: Stapel der Pakete vor der Haustuer (neu geladen, wenn sich die Zahl aendert)
    private GlNetz? _lieferpaket;
    private int _lieferpaketZahl;
    private GlNetz? _marke;
    private GlNetz? _ring;

    // Phase 8 W7: Kleingruen (nur bis GruenWeite gezeichnet) und Autos
    private GlNetz? _gruen;
    private Verkehr? _verkehr;
    /// <summary>Welle A 0.67.0: in diesem Bild faehrt der Bus der Linie die Figur (dann wird sie selbst nicht gezeichnet).</summary>
    private bool _busMitFigur;

    /// <summary>Start-Test: im letzten Bild sass die Figur im Bus.</summary>
    public bool BusMitFigur => _busMitFigur;

    private readonly Dictionary<bool, GlNetz> _autoNetze = [];   // alle Autos in einem Netz (Weisung P8-14b), nah und fern
    private readonly Matrix4x4[] _autoLagen = new Matrix4x4[AutosHoechstens];
    // Welle A 0.60.0 (Weisung P12-166): die leuchtenden Lampen der Ampeln, ein Aufruf mit dem Programm der Autos; je Gruppe
    // (Achse und Farbe) die Lage Eins (leuchtet) oder Null (aus, alle Ecken auf einem Punkt)
    private GlNetz? _ampelLichter;
    private readonly Matrix4x4[] _ampelLagen = new Matrix4x4[Ampeln.Gruppen];
    // Phase 8 W11: Festzeiten-Verkehr (Gestalten statt Autos) und das eigene Netz der Laternen, Tannen und Kuerbisse
    private FestVerkehr.Satz _satz = FestVerkehr.Satz.Autos;
    private List<FestVerkehr.Gestalt>? _gestalten;
    private bool _satzInnen = true;
    private long? _satzUhr;
    private readonly Dictionary<bool, GlNetz> _paket = [];
    // Patch A 0.29.1 (Weisung P10-3): Uebergang zwischen Autos und Festgestalten mit der Uhr, ohne Hausbesuch
    private FestUebergang? _uebergang;
    private List<FestVerkehr.Gestalt>? _neuGestalten;
    private readonly Dictionary<bool, GlNetz> _neuNetze = [];
    private SatzVorrat? _uebergangBau;
    private bool _satzPruefen = true;
    private bool _spielNeu = true;
    private string? _szeneFest;

    /// <summary>Phase 8 W11: wer gerade auf den Strassen faehrt bzw. laeuft (Start-Test); im Uebergang der alte Satz.</summary>
    public FestVerkehr.Satz Satz => _satz;

    /// <summary>Patch A 0.29.1: der laufende Uebergang zum neuen Satz (null = keiner).</summary>
    public FestUebergang? Uebergang => _uebergang;

    /// <summary>
    /// Patch A 0.29.1, nur Start-Test: eine geaenderte feste Uhr (<see cref="Tageslicht.FesteUhr"/>) wechselt den Satz
    /// nach und nach wie die Uhr des Spiels (sonst sofort, wie bisher).
    /// </summary>
    public static bool UhrSanft { get; set; }

    /// <summary>Phase 8 W11: die Festgestalten (null = Autos).</summary>
    public IReadOnlyList<FestVerkehr.Gestalt>? Gestalten => _gestalten;
    private int _autoProgramm;
    private int _autoOrtSichtProj;
    private int _autoOrtLagen;
    private int _autoOrtHell;
    private int _autoOrtDurch;

    /// <summary>So viele Autos fasst ein Netz (Groesse des Felds uAutos im Shader).</summary>
    public const int AutosHoechstens = 16;

    /// <summary>Kleingruen nur bis zu diesem Kameraabstand (in der Gesamtansicht waere es nur wenige Pixel gross).</summary>
    public const float GruenWeite = StadtSzene.GruenBis;

    /// <summary>Ab diesem Kameraabstand zeigen Autos eine einfache Fernform (zwei Kaesten in der Lackfarbe).</summary>
    public const float AutoFern = StadtSzene.FernAb;

    /// <summary>Start-Test: feste Fahrzeit je Bild fuer die Autos (gleiche Bilder), 0 = echte Bildzeit.</summary>
    public static double AutoSchritt { get; set; }

    /// <summary>Start-Test: festes Bild der Schrittbewegung (-1 = nach dem Weg der Figur).</summary>
    public static int ProbeBild { get; set; } = -1;

    /// <summary>Die Autos der Stadt (null ohne Grafik oder vor dem ersten Bild).</summary>
    public Verkehr? Verkehr => _verkehr;

    private string _ringSchluessel = "";
    private int _programm;
    private int _ortMvp;
    private int _ortModell;
    private int _ortHell;
    private int _ortDurch;
    private int[] _lichtOrte = [];
    private int[] _autoLichtOrte = [];
    private int[] _dunstOrte = [];
    private int[] _autoDunstOrte = [];
    private Tageslicht.Licht _lichtVon = Tageslicht.Tag;
    private Tageslicht.Licht _lichtZiel = Tageslicht.Tag;
    private double _lichtSeit = double.MinValue;
    private bool _lichtNeu = true;
    private bool _lichtInnen;
    private long? _lichtFest;
    private readonly Stopwatch _uhr = Stopwatch.StartNew();
    private double _letztesBild = -1;
    private readonly Queue<double> _bilder = new();
    private readonly Dictionary<long, Point> _zeiger = [];
    private Point _druckStart;
    private bool _gezogen;
    private double _abstandZwei;
    private double _winkelZwei;
    private double _mitteZwei;
    private double _folgenAb;
    private double _weiterBis;   // Nachtrag 1 zu P7-12: nach einer Bedienung im Takt des Bildschirms weiterzeichnen
    private int _aufrufe;
    private long _dreiecke;
    private double _nachlaufBis;
    private readonly int[] _gcVorher = new int[3];
    private long _bytesVorher = -1;
    private double _endeVorher = -1;
    private long _endeVorherStempel = Stopwatch.GetTimestamp();
    private int _layouts;
    private double _layoutMs;
    private double _auftraegeMs;
    // Weisung P7-17: steht die Bildschleife (kein naechstes Bild angefordert), ist die Zeit bis zum ersten
    // Anlass (Zeiger oder Anforderung) Ruhe, kein Warten; dazu die Zeitstempel fuer langsame Bilder
    private bool _ruht = true;
    private long _anforderungStempel;
    private long _zeigerStempel;

    /// <summary>Nachtrag 1 zu P7-13 (Wunsch 4): Zeitmessung je Bild (Datei spiel_bilder.log, Problembericht).</summary>
    public BildZeiten Zeiten { get; } = new();

    /// <summary>Weisung P7-15: ein Layout-Durchlauf des Fensters seit dem letzten Bild (zaehlt fuer das naechste Bild).</summary>
    public void LayoutGezaehlt() => _layouts++;

    /// <summary>Weisung P7-15: Zeit fuer das Layout der Spielseite seit dem letzten Bild.</summary>
    public void LayoutGemessen(double ms) => _layoutMs += ms;

    /// <summary>Weisung P7-15: Zeit eines eigenen Auftrags auf dem Oberflaechen-Thread ausserhalb des Bildes (Textzeile, Eingabe).</summary>
    public void AuftragGemessen(double ms) => _auftraegeMs += ms;

    private static double MsSeit(long beginn) => Stopwatch.GetElapsedTime(beginn).TotalMilliseconds;

    public StadtFlaeche()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Jeder Punkt innerhalb der Flaeche trifft sie (Gesten, Tippen, Mausrad); Avalonia gibt
    /// den Punkt im Raum des Fensters.</summary>
    public bool HitTest(Point point) =>
        TopLevel.GetTopLevel(this) is Visual wurzel && wurzel.TranslatePoint(point, this) is { } lokal
        && new Rect(Bounds.Size).Contains(lokal);

    /// <summary>Das Spiel (kommt nach dem Laden der Inhalte); bis dahin zeichnet die Flaeche nur den Himmel.</summary>
    public StadtSpiel? Spiel
    {
        get => _spiel;
        set
        {
            if (_spiel is not null)
            {
                _spiel.Geaendert -= SpielGeaendert;
                _spiel.SzeneGeaendert -= SzeneNeu;
                _spiel.InnenGeaendert -= InnenNeu;
            }
            _spiel = value;
            _gehTasten.Clear();
            SticksLos();
            _kameras.Clear();
            _ich.Neu();
            NachladenAusMerken();
            if (value is not null)
            {
                // Nachtrag 1 zu P7-13 (Wunsch 2): naeher an der Figur (vorher Abstand 10, die Figur war am S24
                // rund 45 Bildpunkte hoch); die Figur steht ueber den Versatz in der Mitte des freien Bereichs
                // Welle A 0.80.0 (Weisung P12-268): im Spiel die feste Kamera (Abstand 8, Rest-Zoom 7 bis 9, Neigung 35 bis 70);
                // das Stadtbau-Werkzeug behaelt die freie Kamera
                _kameras[ProbeWelt.Stadt] = FesteKamera
                    ? SpielKamera.Draussen(value.FigurX, value.FigurZ, value.Szene.Breite, value.Szene.Tiefe)
                    : new Kamera(value.FigurX, value.FigurZ, StartAbstand)
                    {
                        Grenzen = (0, 0, value.Szene.Breite, value.Szene.Tiefe),
                    };
                value.Geaendert += SpielGeaendert;
                value.SzeneGeaendert += SzeneNeu;
                // Welle A 0.95.0: beim freien Gehen sperrt eine noch nicht geladene Kachel
                value.GeladenAn = GeladenAn;
                value.InnenGeaendert += InnenNeu;
                _szeneNeu = true;
                _spielNeu = true;
                _innenNeu = true;
                InnenNeu();
                if (_glFrisch)
                {
                    // Weisung P8-55 (W12): das erste Spiel nach dem Einrichten von OpenGL wird vorgeladen
                    VorladenStarten();
                }
            }
            AnfordernBild();
        }
    }

    /// <summary>Neuer Innenraum (oder neue Einrichtung): Netz neu laden; bei einem anderen Ort neue Kamera auf der Figur.</summary>
    private void InnenNeu()
    {
        _innenNeu = true;
        if (_spiel?.Innen is { } innen && (!_kameras.ContainsKey(innen.Ansicht) || _innenGeladen?.Ansicht != innen.Ansicht))
        {
            // schraeg von vorn (Sueden) mit Blick in die Raeume, auf die Mitte des ganzen Innenraums (Nachtrag 2
            // zu P7-12: vorher auf die Figur an der Tuer, dann lag der Laden halb ausserhalb des Bildes)
            // Welle A 0.97.0: dieselbe Kamera wie bisher, gebaut in SpielKamera.Drinnen (die Tests pruefen dieselbe Rechnung)
            _kameras[innen.Ansicht] = SpielKamera.Drinnen(innen.Breite, innen.Tiefe, FesteKamera);
        }
        AnfordernBild();
    }

    /// <summary>Spielstand geaendert (z. B. neues Aussehen): Figuren beim naechsten Bild neu nachschlagen.</summary>
    private void SpielGeaendert()
    {
        _lichtNeu = true;
        _satzPruefen = true;
        _figurZuletzt.Clear();
        _ringAussehen = null;
        AnfordernBild();
    }

    private void SzeneNeu()
    {
        _lichtNeu = true;
        _satzPruefen = true;
        _szeneNeu = true;
        AnfordernBild();
    }

    /// <summary>Abstand der Stadt-Kamera beim Start (Nachtrag 1 zu P7-13, Wunsch 2); im Spiel ab A 0.80.0 <see cref="SpielKamera.Abstand"/>.</summary>
    public const float StartAbstand = 6f;

    /// <summary>
    /// Welle A 0.80.0 (Weisung P12-268): die Flaeche zeigt das Spiel (nicht das Stadtbau-Werkzeug). Dann gilt draussen die feste
    /// Kamera (<see cref="SpielKamera"/>, <see cref="Kamerabedienung"/>); Innenraeume bleiben wie bis A 0.79.0.
    /// </summary>
    public bool FesteKamera { get; init; }

    /// <summary>Gilt gerade die feste Kamera (Spiel, draussen)?</summary>
    public bool KameraFest => FesteKamera && _spiel is { ImInnenraum: false };

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 2): von Kopfzeile (oben) und Textkarte (unten) verdeckte Hoehe in Punkten;
    /// die Kamera stellt ihr Ziel (die Figur) in die Mitte des freien Bereichs dazwischen.
    /// </summary>
    public (double Oben, double Unten) Frei
    {
        get => _frei;
        set
        {
            if (_frei != value)
            {
                _frei = value;
                AnfordernBild();
            }
        }
    }

    private (double Oben, double Unten) _frei;

    /// <summary>Welle A 0.99.0 (Sammelliste 399): waagerechte Lage der mittigen Leiste (von, bis) in Punkten der Flaeche; (0, 0) = keine.</summary>
    public (double Von, double Bis) LeisteX { get; set; }

    /// <summary>Versatz der Kamera fuer den freien Bereich (Bildraum, hoechstens 0,6).</summary>
    private float FreiVersatz() => SpielKamera.FreiVersatz(Bounds.Height, _frei.Oben, _frei.Unten);

    /// <summary>OpenGL steht (Pflicht-Messpunkt "GL-Zusammenhang erzeugt").</summary>
    public bool GlBereit { get; private set; }

    /// <summary>Fehler beim Einrichten (Shader), sonst null.</summary>
    public string? GlFehler { get; private set; }

    /// <summary>"OpenGL ES 3.0 · Renderer · Version" fuer Probe-Zeile und Messdatei.</summary>
    public string GlText { get; private set; } = "";

    /// <summary>Kurzform fuer die Zeile ueber der Stadt: Version und Grafikchip ohne Treiberangaben.</summary>
    public string GlKurz { get; private set; } = "";

    public long BilderGesamt { get; private set; }

    /// <summary>Bilder in der letzten Sekunde.</summary>
    public int BilderJeSekunde => _bilder.Count;

    /// <summary>Wird nach jedem gezeichneten Bild gesetzt (Messung).</summary>
    public event Action? BildGezeichnet;

    public event Action? GlGestartet;

    /// <summary>Kamera der gezeigten Flaeche (Stadt oder Buero); null, solange das Spiel noch laedt.</summary>
    public Kamera? AktuelleKamera
    {
        get
        {
            if (_spiel is null)
            {
                return null;
            }
            var kamera = _kameras[_spiel.FlaecheId];
            kamera.Versatz = FreiVersatz();
            if (FesteKamera && _spiel.ImInnenraum)
            {
                // Welle A 0.97.0 (Weisung P12-312 Punkt 1, Nico 1a, Sammelliste 392): drinnen nur so nah, dass die ganze Figur im
                // freien Bereich bleibt (je Bildschirm, Kopfzeile und Tafel); naeher geht es nur in die Ich-Sicht. Werkzeug unveraendert.
                SpielKamera.InnenBereich(kamera, Bounds.Height, _frei.Oben, _frei.Unten, Innenraum.FigurHoehe);
            }
            return kamera;
        }
    }

    /// <summary>Messung: laeuft, solange gesetzt, ohne Pause weiter (Kameraschwenk).</summary>
    public bool Dauerlauf { get; set; }

    /// <summary>
    /// Patch A 0.72.1 (Sammelliste 330): im Spiel faehrt der Verkehr draussen immer (die Bildschleife ruht draussen nicht mehr,
    /// die Bildbremse der Grafikstufe gilt weiter). true = alte Regel (Autos nur <see cref="Verkehr.Nachlauf"/> Sekunden nach dem
    /// Bedienen); so bleibt es in der 3D-Vorschau des Werkzeugs (E1: keine Dauerlast beim Bauen).
    /// </summary>
    public bool VerkehrNurNachBedienen { get; set; }

    /// <summary>Start-Test: wird je Bild vor dem Zeichnen gerufen (z. B. Kamera ein Stueck drehen).</summary>
    public Action? VorBild { get; set; }

    /// <summary>
    /// Neues Bild anfordern. Nachtrag 1 zu P7-12 (Wunsch 4): danach noch 0,75 s im Takt des Bildschirms
    /// weiterzeichnen; vorher kam beim Ziehen und Zoomen nur je Fingerbewegung ein Bild (am S24 rund 55/s,
    /// obwohl die Grafik 120/s schafft).
    /// </summary>
    public void AnfordernBild()
    {
        _weiterBis = _uhr.Elapsed.TotalSeconds + 0.75;
        // Nico 3a: bedient = die Autos fahren noch Verkehr.Nachlauf Sekunden
        _verkehr?.Bedient(_uhr.Elapsed.TotalSeconds);
        Anfordern();
    }

    /// <summary>Weisung P7-17: naechstes Bild anfordern; steht die Bildschleife, den ersten Anlass merken.</summary>
    private void Anfordern()
    {
        if (_ruht && _anforderungStempel == 0)
        {
            _anforderungStempel = Stopwatch.GetTimestamp();
        }
        // Welle Grafik G1: auch Anlaesse von aussen halten die Bildbremse ein (ohne Bremse sofort wie vorher)
        GebremstAnfordern();
    }

    /// <summary>Zeichenaufrufe im letzten Bild (Messung, Wunsch 4).</summary>
    public int LetzteZeichenaufrufe { get; private set; }

    /// <summary>Dreiecke im letzten Bild (Messung, Wunsch 4).</summary>
    public long LetzteDreiecke { get; private set; }

    /// <summary>
    /// Welle D2 (Weisung P12-157): Teil des letzten Bilds, den die Zaehlung ohne Grafikkarte rechnet (Stadt, Kleingruen,
    /// Paket-Netz, Saetze, Haeuser; ohne Autos, Figur, Kreis, Marke, Pakete): Aufrufe und Dreiecke.
    /// </summary>
    public int LetzteStadtAufrufe { get; private set; }

    /// <inheritdoc cref="LetzteStadtAufrufe"/>
    public long LetzteStadtDreiecke { get; private set; }

    /// <summary>Welle D2: Sicht mal Projektion und Kameraabstand des letzten Bilds (Vergleich mit der Zaehlung).</summary>
    public Matrix4x4 LetzteSichtProj { get; private set; }

    /// <inheritdoc cref="LetzteSichtProj"/>
    public float LetzterAbstand { get; private set; }

    /// <summary>
    /// Welle A 1.13.0 (Start-Test 464): Lage der Figur im letzten Bild (Modell in Einheiten, mal Lage); null = keine Figur gezeichnet
    /// (im Bus). Zusammen mit <see cref="LetzteSichtProj"/> ergibt sie die Stelle der Figur auf dem Bild.
    /// </summary>
    public Matrix4x4? LetzteFigurLage { get; private set; }

    /// <summary>
    /// Welle A 1.13.0 (Sammelliste 464): Rechteck der Figur auf dem letzten Bild in Punkten der Flaeche (Kasten des Modells, Breite
    /// 0,6 und Hoehe 1,05 Einheiten, mit <see cref="LetzteFigurLage"/> und <see cref="LetzteSichtProj"/>); null ohne Figur.
    /// </summary>
    public Rect? FigurAufBild()
    {
        if (LetzteFigurLage is not { } lage || Bounds.Width < 1 || Bounds.Height < 1)
        {
            return null;
        }
        var m = lage * LetzteSichtProj;
        double x1 = double.MaxValue, y1 = double.MaxValue, x2 = double.MinValue, y2 = double.MinValue;
        foreach (var x in FigurKastenXZ)
        {
            foreach (var y in FigurKastenY)
            {
                foreach (var z in FigurKastenXZ)
                {
                    var v = Vector4.Transform(new Vector4(x, y, z, 1), m);
                    if (v.W <= 1e-4f)
                    {
                        return null;
                    }
                    var px = ((v.X / v.W) + 1) / 2 * Bounds.Width;
                    var py = (1 - (v.Y / v.W)) / 2 * Bounds.Height;
                    (x1, y1, x2, y2) = (Math.Min(x1, px), Math.Min(y1, py), Math.Max(x2, px), Math.Max(y2, py));
                }
            }
        }
        return x2 - x1 > 1 && y2 - y1 > 1 ? new Rect(x1, y1, x2 - x1, y2 - y1) : null;
    }

    private static readonly float[] FigurKastenXZ = [-0.3f, 0.3f];
    private static readonly float[] FigurKastenY = [0f, 1.05f];

    private int _festAufrufe;
    private long _festDreiecke;

    /// <summary>Rechenzeit des Programms fuer das letzte Bild in ms (ohne Warten auf die Grafikkarte).</summary>
    public double LetzteZeichenzeitMs { get; private set; }

    /// <summary>Wartet auf das naechste gezeichnete 3D-Bild (Messung); fordert es an.</summary>
    public Task NaechstesBildAsync()
    {
        var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Einmal()
        {
            // Weisung P8-55 (W12): Bilder waehrend des Vorladens zeigen nur den Himmel, gewartet wird auf ein echtes
            if (_laedtVor)
            {
                return;
            }
            BildGezeichnet -= Einmal;
            fertig.TrySetResult();
        }
        BildGezeichnet += Einmal;
        Anfordern();
        return fertig.Task;
    }

    /// <summary>Weisung P8-52 (nur Leistungsprobe): Stadt so oft zeichnen (1 = normal).</summary>
    public static int ProbeMehrfach { get; set; } = 1;

    /// <summary>Weisung P8-52 (nur Leistungsprobe): so viele Zeichenaufrufe je Bild mindestens (0 = normal).</summary>
    public static int ProbeAufrufe { get; set; }

    /// <summary>Weisung P8-52 (nur Leistungsprobe): Stadt beim naechsten Bild neu in die Grafikkarte laden.</summary>
    public void ProbeNeuLaden()
    {
        NachladenAusMerken();
        SzeneNeu();
    }

    /// <summary>Weisung P8-51 (nur Messung W12): jedes Bild zusaetzlich hier, solange gesetzt.</summary>
    public static List<BildZeiten.Bild>? Mitschnitt { get; set; }

    /// <summary>Weisung P8-51 (nur Messung W12): Dauer des letzten Einrichtens (Shader, Grundnetze) in ms.</summary>
    public double GlInitMs { get; private set; } = -1;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        var initBeginn = Stopwatch.GetTimestamp();
        GlFehler = null;
        try
        {
            var v = GlVersion;
            var es = v.Type == GlProfileType.OpenGLES;
            GlText = (es ? "OpenGL ES " : "OpenGL ") + v.Major + "." + v.Minor + " · " + gl.Renderer + " · " + gl.Version;
            var grafik = gl.Renderer ?? "";
            var klammer = grafik.IndexOf(" (", StringComparison.Ordinal);
            GlKurz = (es ? "OpenGL ES " : "OpenGL ") + v.Major + "." + v.Minor + " · " + (klammer > 0 ? grafik[..klammer] : grafik);
            // Weisung P7-15: laeuft das Zeichnen im Oberflaechen-Thread, wartet jedes Bild auf Layout und Auftraege
            Zeiten.Kopf = "Grafik: " + GlText + " · Bild im Oberflächen-Thread: " + (Avalonia.Threading.Dispatcher.UIThread.CheckAccess() ? "ja" : "nein")
                + (Systemwerte.Kopf?.Invoke() is { Length: > 0 } system ? "\n" + system : "");
            _programm = Programm(gl, es, v);
            _ortMvp = gl.GetUniformLocationString(_programm, "uMvp");
            _ortModell = gl.GetUniformLocationString(_programm, "uModell");
            _ortHell = gl.GetUniformLocationString(_programm, "uHell");
            _ortDurch = gl.GetUniformLocationString(_programm, "uDurch");
            _lichtOrte = [.. new[] { "uTag", "uTonR", "uTonG", "uTonB", "uNacht", "uAugeX", "uAugeY", "uAugeZ" }.Select(n => gl.GetUniformLocationString(_programm, n))];
            _dunstOrte = DunstOrte(gl, _programm);
            _autoProgramm = Programm(gl, es, v, autos: true);
            _autoOrtSichtProj = gl.GetUniformLocationString(_autoProgramm, "uSichtProj");
            _autoOrtLagen = gl.GetUniformLocationString(_autoProgramm, "uAutos");
            _autoOrtHell = gl.GetUniformLocationString(_autoProgramm, "uHell");
            _autoOrtDurch = gl.GetUniformLocationString(_autoProgramm, "uDurch");
            _autoLichtOrte = [.. new[] { "uTag", "uTonR", "uTonG", "uTonB", "uNacht", "uAugeX", "uAugeY", "uAugeZ" }.Select(n => gl.GetUniformLocationString(_autoProgramm, n))];
            _autoDunstOrte = DunstOrte(gl, _autoProgramm);
            InstanzenEinrichten(gl, es, v);
            GrafikEinrichten(gl, es, v);
            _genauigkeitGemeldet = false;
            GenauigkeitZeilen = "";
            _marke = GlNetz.Laden(gl, ProbeWelt.Zielmarke());
            _ausruf = GlNetz.Laden(gl, Figur.Ausrufezeichen(0.7f));
            _szeneNeu = true;
            GlBereit = true;
            // Weisung P8-55 (W12): nach jedem Einrichten (Seite geoeffnet, Android nach dem Hintergrund) erst alles
            // vorladen; ist das Spiel schon da, warten die Bilder bis dahin (kein Bauen im Bild)
            _glFrisch = true;
            _hochladen = null;
            if (_spiel is not null)
            {
                _vorladeAufgabe = null;
                _laedtVor = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(VorladenStarten);
            }
        }
        catch (Exception e)
        {
            GlFehler = e.Message;
        }
        GlInitMs = MsSeit(initBeginn);
        GlGestartet?.Invoke();
    }

    /// <summary>Zusammenhang verloren (z. B. Treiber neu gestartet): bis zum neuen Start gilt "nicht bereit".</summary>
    protected override void OnOpenGlLost()
    {
        GlBereit = false;
        _flaechen.Clear();
        _haeuser.Clear();
        _haeuserFern.Clear();
        _hausKugel.Clear();
        _figuren.Clear();
        _figurZuletzt.Clear();
        _innen = null;
        _innenWaende = [];
        _decke = null;
        _innenGl.Clear();
        _hochladen = null;
        _vorladeAufgabe = null;
        _glFrisch = false;
        _laedtVor = false;
        _markierung = null;
        _markierungSchluessel = "";
        _ring = null;
        _ringAussehen = null;
        _ringSchluessel = "";
        _lieferpaket = null;
        _lieferpaketZahl = 0;
        _szeneNeu = true;
        _innenNeu = true;
        _programm = 0;
        _autoProgramm = 0;
        _instanzProgramm = 0;
        _instanzen.Clear();
        _gruen = null;
        _autoNetze.Clear();
        _neuNetze.Clear();
        _paket.Clear();
        _ampelLichter = null;
        GlastuerenFrei(null);
        TuerBlaetterFrei(null);
        NachladenFrei(null);
        GrafikFrei(null);
        base.OnOpenGlLost();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        foreach (var netz in _flaechen.Values.Concat(_haeuser.Values).Concat(_haeuserFern.Values).Concat(_figuren.Values).Concat(_innenGl.Values.SelectMany(e => e.Waende.Values.Append(e.Netz)))
            .Append(_markierung).Append(_marke).Append(_ausruf).Append(_lieferpaket).Append(_ring).Append(_gruen).Concat(_autoNetze.Values).Concat(_neuNetze.Values).Concat(_paket.Values).Append(_ampelLichter))
        {
            netz?.Freigeben(gl);
        }
        _ampelLichter = null;
        GlastuerenFrei(gl);
        TuerBlaetterFrei(gl);
        NachladenFrei(gl);
        GrafikFrei(gl);
        _innenGl.Clear();
        _hochladen = null;
        _vorladeAufgabe = null;
        _glFrisch = false;
        _laedtVor = false;
        _gruen = null;
        _autoNetze.Clear();
        _neuNetze.Clear();
        _paket.Clear();
        if (_autoProgramm != 0)
        {
            gl.DeleteProgram(_autoProgramm);
            _autoProgramm = 0;
        }
        InstanzenFrei(gl);
        if (_instanzProgramm != 0)
        {
            gl.DeleteProgram(_instanzProgramm);
            _instanzProgramm = 0;
        }
        _flaechen.Clear();
        _haeuser.Clear();
        _haeuserFern.Clear();
        _hausKugel.Clear();
        _figuren.Clear();
        _figurZuletzt.Clear();
        _innen = null;
        _innenWaende = [];
        _decke = null;
        _markierung = null;
        _ring = null;
        _ringAussehen = null;
        _ringSchluessel = "";
        // Phase 12 Welle 2: oben freigegeben, beim naechsten Start neu laden (sonst zeichnet er einen geloeschten Puffer)
        _lieferpaket = null;
        _lieferpaketZahl = 0;
        if (_programm != 0)
        {
            gl.DeleteProgram(_programm);
            _programm = 0;
        }
        GlBereit = false;
    }

    /// <summary>
    /// Phase 8 W10 (Nico 3b): Licht nach der Tagesuhr der Stadt und der Jahreszeit, innen immer Tag. Die Uhr wird nur
    /// nach einer Aenderung des Spiels neu gelesen (nicht je Bild); springt sie, blendet das Licht in
    /// <see cref="Tageslicht.Ueberblenden"/> ueber (Wechsel zwischen innen und aussen und feste Uhr sofort).
    /// </summary>
    private Tageslicht.Licht AktuellesLicht()
    {
        var jetzt = _uhr.Elapsed.TotalSeconds;
        var innen = _spiel is null || _spiel.ImInnenraum;
        var fest = Tageslicht.FesteUhr;
        if (_lichtNeu || innen != _lichtInnen || fest != _lichtFest)
        {
            var ziel = innen ? Tageslicht.Tag : Tageslicht.Fuer(fest ?? _spiel!.Jetzt.Uhr, Jahreszeiten.Heute());
            var sofort = innen != _lichtInnen || fest != _lichtFest || _lichtSeit == double.MinValue;
            if (ziel != _lichtZiel || sofort)
            {
                _lichtVon = sofort ? ziel : LichtBei(jetzt);
                _lichtZiel = ziel;
                _lichtSeit = jetzt;
            }
            _lichtNeu = false;
            _lichtInnen = innen;
            _lichtFest = fest;
        }
        var licht = LichtBei(jetzt);
        if (licht != _lichtZiel)
        {
            AnfordernBild();
        }
        return licht;
    }

    private Tageslicht.Licht LichtBei(double jetzt) =>
        Tageslicht.Mischen(_lichtVon, _lichtZiel, (float)((jetzt - _lichtSeit) / Tageslicht.Ueberblenden.TotalSeconds));

    /// <summary>Licht im Bild (Start-Test: Tag, Daemmerung, Nacht).</summary>
    public Tageslicht.Licht LichtJetzt => _lichtZiel;

    private static void LichtSetzen(GlInterface gl, int[] orte, Tageslicht.Licht l, System.Numerics.Vector3 auge)
    {
        gl.Uniform1f(orte[0], l.Hell);
        gl.Uniform1f(orte[1], l.Ton.X);
        gl.Uniform1f(orte[2], l.Ton.Y);
        gl.Uniform1f(orte[3], l.Ton.Z);
        gl.Uniform1f(orte[4], l.Nacht);
        gl.Uniform1f(orte[5], auge.X);
        gl.Uniform1f(orte[6], auge.Y);
        gl.Uniform1f(orte[7], auge.Z);
    }

    private Color Himmel() =>
        this.TryFindResource("Pinsel.bg", ActualThemeVariant, out var r) && r is ISolidColorBrush b ? b.Color : Color.FromRgb(220, 235, 250);

    private bool IstDunkel()
    {
        var c = Himmel();
        return (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B) < 110;
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia.Platform;
using Lernplattform.Core.Spiel.Ton;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 8 W8 (Teilplan P8-15 1.5, Weisungen P8-17 bis P8-17d): Toene des Spiels. Nur auf der Spielseite;
/// aus beim Verlassen, im Hintergrund (Android OnPause) und wenn das Fenster am PC minimiert ist. Laden
/// und Oeffnen des Geraets laufen im Hintergrund, nie auf dem Oberflaechen-Faden. Pruefstand
/// (<c>LERNPLATTFORM_TON=aus</c>) oder kein Geraet: stiller Dienst, der die Anlaesse nur zaehlt
/// (<see cref="Anlaesse"/>, <see cref="Gespielt"/>); er liest dann auch keine Toene.
/// </summary>
public static class TonDienst
{
    private static readonly object Sperre = new();
    private static readonly Mischer Mischer = new();
    private static readonly Random Zufall = new();
    private static readonly Stopwatch Uhr = Stopwatch.StartNew();
    private static readonly Dictionary<string, int> Letzte = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> Autos = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> AutoLaut = new(StringComparer.Ordinal);

    private static ITonausgabe? _ausgabe;
    private static Task? _vorbereitung;
    private static Dictionary<string, List<TonDaten>>? _toene;
    private static readonly Tonschalter Schalter = new();
    private static bool _laeuft;
    private static TonWerte _werte = TonEinstellungen.Lesen([]);
    private static bool _innen;
    private static Stadtlage? _lage;
    private static int _grund;
    private static int _fluss;
    private static float _flussLaut = -1;
    private static int _lied;
    private static bool _liedOeffnet;
    private static string? _liedStill;
    private static double _naechsterRuf = -1;
    private static Core.Spiel.Jahreszeit _jahreszeit = Core.Spiel.Jahreszeit.Sommer;
    private static string? _grundSchluessel;
    private static Timer? _pflege;
    private static double _letzterTakt;

    /// <summary>Anlaesse je Schluessel (Tonplan), auch im stillen Dienst (Start-Test).</summary>
    public static ConcurrentDictionary<string, int> Anlaesse { get; } = new(StringComparer.Ordinal);

    /// <summary>Anlaesse, die wirklich klingen wuerden (Regler ueber 0); im stillen Dienst nur gezaehlt.</summary>
    public static ConcurrentDictionary<string, int> Gespielt { get; } = new(StringComparer.Ordinal);

    /// <summary>Eigene Lieder (Datenordner/musik); setzt der Programmstart.</summary>
    public static EigeneLieder? Lieder { get; set; }

    /// <summary>Messpunkt "Spiel: Töne geladen (im Hintergrund)" (nur zur Ansicht, ohne Grenze).</summary>
    public static double GeladenMs { get; private set; } = -1;

    /// <summary>Wie lange das letzte Oeffnen des Geraets dauerte (im Hintergrund, Weisung P8-17e Punkt 3); -1 = nie.</summary>
    public static double GeoeffnetMs { get; private set; } = -1;

    public static bool Still => _ausgabe is null or StilleAusgabe;

    /// <summary>Sammelliste 142: Zeile fuer den Problembericht (Oeffnungszeit des Geraets oder warum keine).</summary>
    public static string BerichtZeile() => Core.Diagnose.Problembericht.TonText(GeoeffnetMs, _ausgabe is StilleAusgabe, _vorbereitung is not null);

    /// <summary>Jahreszeit des Stadt-Grundklangs, gesetzt beim Betreten der Spielseite (Weisung P8-24).</summary>
    public static Core.Spiel.Jahreszeit Zeit => _jahreszeit;

    /// <summary>Klingt gerade etwas (Spielseite, Geraet offen, Regler ueber 0)?</summary>
    public static bool Klingt => Volatile.Read(ref _laeuft) && !Still;

    /// <summary>
    /// Laeuft der Ton (Spielseite, nicht im Hintergrund, Gesamt ueber 0), auch still ohne Geraet (dort nur gezaehlt,
    /// Start-Test)? Sonst rechnet die Flaeche keine Lage.
    /// </summary>
    public static bool Aktiv => Volatile.Read(ref _laeuft);

    public static string Zustand => Tonwahl.Zeile;

    /// <summary>Wie oft der Ton nach dem Hintergrund von selbst wieder anging (Weisung P8-17e, Start-Test).</summary>
    public static int Wiederanlaeufe
    {
        get
        {
            lock (Sperre)
            {
                return Schalter.Wiederanlaeufe;
            }
        }
    }

    public static void Regler(TonWerte werte)
    {
        lock (Sperre)
        {
            _werte = werte;
            Mischer.Regler(werte.GesamtFaktor, werte.MusikFaktor, werte.UmgebungFaktor);
            Anwenden(Schalter.Setzen(gesamt: werte.Gesamt));
            if (!Schalter.Laeuft && !Pausiert)
            {
                Leeren();
            }
            MusikPruefen();
        }
    }

    public static void SpielBetreten()
    {
        lock (Sperre)
        {
            Vorbereiten();
            // Grundklang je Jahreszeit: gewechselt wird nur beim Betreten der Spielseite
            _jahreszeit = Core.Spiel.Jahreszeiten.Heute();
            Anwenden(Schalter.Setzen(imSpiel: true));
            _pflege ??= new Timer(_ => Pflege(), null, 1000, 1000);
        }
    }

    public static void SpielVerlassen()
    {
        lock (Sperre)
        {
            _lage = null;
            Anwenden(Schalter.Setzen(imSpiel: false));
            // auch wenn der Ton schon im Hintergrund stand: beim Verlassen ist alles weg
            Leeren();
        }
    }

    /// <summary>
    /// Android OnPause/OnResume, PC minimiert/wieder da. Zurueck aus dem Hintergrund geht der Ton von selbst wieder an
    /// (Weisung P8-17e): Geraet neu oeffnen (im Hintergrund), Schleifen setzt der naechste Takt, das Lied die Pflege.
    /// </summary>
    public static void Hintergrund(bool an)
    {
        lock (Sperre)
        {
            Anwenden(Schalter.Setzen(hintergrund: an));
        }
    }

    private static bool Soll => Schalter.Laeuft;

    /// <summary>Ausgabe starten oder anhalten; beim Anhalten sind alle Toene sofort weg.</summary>
    private static void Anwenden(bool? aenderung)
    {
        if (aenderung is not { } an)
        {
            return;
        }
        Volatile.Write(ref _laeuft, an);
        if (!an && Pausiert)
        {
            // Hintergrund: nur das Geraet schliessen; Schleifen und Lied bleiben im Mischer stehen und laufen
            // beim Zurueckkehren an derselben Stelle weiter
            Geraet();
            return;
        }
        if (!an)
        {
            Leeren();
            Geraet();
            return;
        }
        _letzterTakt = Uhr.Elapsed.TotalSeconds;
        Geraet();
        MusikPruefen();
    }

    /// <summary>Alle Toene sofort weg (Spiel verlassen, Gesamt 0).</summary>
    private static void Leeren()
    {
        Mischer.AllesBeenden();
        _grund = _fluss = _lied = 0;
        _grundSchluessel = null;
        _flussLaut = -1;
        _liedStill = null;
        Autos.Clear();
        AutoLaut.Clear();
    }

    /// <summary>Im Hintergrund angehalten: alles bleibt im Mischer stehen (Weisung P8-17e).</summary>
    private static bool Pausiert => !Schalter.Laeuft && Schalter.ImHintergrund && Schalter.ImSpiel && Schalter.Gesamt > 0;

    private static readonly object GeraetSperre = new();

    /// <summary>Geraet passend zu <see cref="_laeuft"/> oeffnen oder schliessen, im Hintergrund (kann dauern).</summary>
    private static void Geraet()
    {
        if (_ausgabe is not { } ausgabe || ausgabe is StilleAusgabe)
        {
            return;
        }
        Task.Run(() =>
        {
            lock (GeraetSperre)
            {
                try
                {
                    if (Volatile.Read(ref _laeuft))
                    {
                        var uhr = Stopwatch.StartNew();
                        ausgabe.Starten(Mischer);
                        GeoeffnetMs = uhr.Elapsed.TotalMilliseconds;
                    }
                    else
                    {
                        ausgabe.Anhalten();
                    }
                }
                catch (Exception)
                {
                    // Geraet weg (z. B. Kopfhoerer gezogen): still weiter
                    lock (Sperre)
                    {
                        _ausgabe = new StilleAusgabe("Gerät verloren");
                    }
                }
            }
        });
    }

    /// <summary>Einmal: Ausgabe waehlen und (wenn sie klingt) alle Toene lesen, beides im Hintergrund.</summary>
    private static void Vorbereiten()
    {
        if (_vorbereitung is not null)
        {
            return;
        }
        _vorbereitung = Task.Run(() =>
        {
            var schalter = Environment.GetEnvironmentVariable(Tonwahl.Schalter);
            ITonausgabe ausgabe;
            lock (GeraetSperre)
            {
                var uhr = Stopwatch.StartNew();
                ausgabe = Tonwahl.Oeffnen(Mischer, () => new SoundFlowAusgabe(), schalter);
                if (ausgabe is not StilleAusgabe)
                {
                    GeoeffnetMs = uhr.Elapsed.TotalMilliseconds;
                }
            }
            Dictionary<string, List<TonDaten>>? toene = null;
            if (ausgabe is not StilleAusgabe)
            {
                var uhr = Stopwatch.StartNew();
                toene = Lesen();
                GeladenMs = uhr.Elapsed.TotalMilliseconds;
            }
            lock (Sperre)
            {
                _ausgabe = ausgabe;
                _toene = toene;
                if (!_laeuft)
                {
                    Geraet();
                }
                MusikPruefen();
            }
        });
    }

    private static Dictionary<string, List<TonDaten>> Lesen()
    {
        const string Basis = "avares://Lernplattform.App/Assets/Toene/";
        var toene = new Dictionary<string, List<TonDaten>>(StringComparer.Ordinal);
        JsonObject liste;
        using (var json = AssetLoader.Open(new Uri(Basis + "toene.json")))
        {
            liste = (JsonObject)JsonNode.Parse(json)!;
        }
        foreach (var eintrag in liste["dateien"]!.AsArray().OfType<JsonObject>())
        {
            var datei = (string)eintrag["datei"]!;
            var anlass = (string)eintrag["anlass"]!;
            try
            {
                using var stream = AssetLoader.Open(new Uri(Basis + datei));
                using var kopie = new MemoryStream();
                stream.CopyTo(kopie);
                kopie.Position = 0;
                var daten = Tondekoder.Lesen(kopie, Path.GetExtension(datei));
                if (!toene.TryGetValue(anlass, out var gruppe))
                {
                    toene[anlass] = gruppe = [];
                }
                gruppe.Add(daten);
            }
            catch (Exception fehler)
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben("Ton: " + datei + " nicht lesbar: " + fehler.GetType().Name);
            }
        }
        return toene;
    }

    private static string Zaehlen(string schluessel, Tongruppe gruppe)
    {
        Anlaesse.AddOrUpdate(schluessel, 1, (_, n) => n + 1);
        if (Mischer.Faktor(gruppe) > 0 && Soll)
        {
            Gespielt.AddOrUpdate(schluessel, 1, (_, n) => n + 1);
        }
        return schluessel;
    }

    /// <summary>Ein einzelner Ton (Schritt, Tuer, Kasse, Melodie, Vogelruf).</summary>
    public static void Anlass(Tonanlass anlass)
    {
        var schluessel = Tonplan.Schluessel(anlass);
        var gruppe = Tonplan.Gruppe(anlass.Art);
        lock (Sperre)
        {
            Zaehlen(schluessel, gruppe);
            if (!Soll || Mischer.Faktor(gruppe) <= 0 || _toene is null || !_toene.TryGetValue(schluessel, out var liste) || liste.Count == 0)
            {
                return;
            }
            var wahl = Tonplan.Variante(liste.Count, Letzte.GetValueOrDefault(schluessel, -1), Zufall);
            Letzte[schluessel] = wahl;
            var laut = anlass.Art == Core.Spiel.Ton.Anlass.Vogelruf ? Tonplan.RufeFuer(_jahreszeit)?.Laut ?? 0f : Tonplan.Laut(anlass.Art);
            Mischer.Starten(liste[wahl], gruppe, laut);
        }
    }

    /// <summary>Je Bild: Schleifen der Stadt (Voegel, Fluss, Autos) oder innen still; Musik je Ort.</summary>
    public static void Takt(bool innen, Stadtlage? lage)
    {
        lock (Sperre)
        {
            var wechsel = innen != _innen;
            _letzterTakt = Uhr.Elapsed.TotalSeconds;
            _innen = innen;
            _lage = innen ? null : lage;
            Schleifen();
            if (wechsel)
            {
                MusikPruefen();
            }
        }
    }

    private static void Schleifen()
    {
        if (!_laeuft)
        {
            return;
        }
        var lage = _lage;
        var grund = Tonplan.StadtGrundFuer(_jahreszeit);
        if (lage is not null && grund != _grundSchluessel)
        {
            // andere Jahreszeit: alte Schleife aus; gezaehlt auch im stillen Dienst (Start-Test)
            if (_grund != 0)
            {
                Mischer.Beenden(_grund);
                _grund = 0;
            }
            _grundSchluessel = grund;
            Zaehlen(grund, Tongruppe.Umgebung);
        }
        if (_toene is null)
        {
            return;
        }
        _grund = Schleife(_grund, grund, lage is null ? 0 : Tonplan.StadtGrund);
        var fluss = lage is null ? 0 : Tonplan.Fluss * lage.Fluss;
        if (Math.Abs(fluss - _flussLaut) > 0.005f)
        {
            _fluss = Schleife(_fluss, Tonplan.FlussSchluessel, fluss);
            _flussLaut = fluss;
        }
        foreach (var modell in Autos.Keys.ToList())
        {
            if (lage is null || lage.Autos.All(a => a.Modell != modell))
            {
                Mischer.Beenden(Autos[modell]);
                Autos.Remove(modell);
                AutoLaut.Remove(modell);
            }
        }
        foreach (var (modell, laut) in lage?.Autos ?? [])
        {
            var soll = Tonplan.Auto * laut;
            if (AutoLaut.TryGetValue(modell, out var alt) && Math.Abs(alt - soll) <= 0.005f)
            {
                continue;
            }
            AutoLaut[modell] = soll;
            Autos[modell] = Schleife(Autos.GetValueOrDefault(modell), Tonplan.AutoSchluessel(modell), soll);
        }
    }

    /// <summary>Schleife starten, leiser/lauter stellen oder (0) ausblenden; Rueckgabe die Nummer im Mischer.</summary>
    private static int Schleife(int id, string schluessel, float laut)
    {
        if (laut <= 0)
        {
            if (id != 0)
            {
                Mischer.Beenden(id);
            }
            return 0;
        }
        if (id != 0 && Mischer.Laeuft(id))
        {
            Mischer.Lautstaerke(id, laut);
            return id;
        }
        if (_toene is null || !_toene.TryGetValue(schluessel, out var liste) || liste.Count == 0)
        {
            return 0;
        }
        return Mischer.Starten(liste[0], Tongruppe.Umgebung, laut, schleife: true);
    }

    /// <summary>Jede Sekunde: Vogelrufe, naechstes Lied (auch wenn das Spiel gerade kein Bild zeichnet).</summary>
    private static void Pflege()
    {
        try
        {
            lock (Sperre)
            {
                if (_lied != 0 && Mischer.Beendet(_lied))
                {
                    _lied = 0;
                }
                MusikPruefen();
                var jetzt = Uhr.Elapsed.TotalSeconds;
                // zeichnet die Flaeche nicht mehr, steht alles still: Autos aus (Voegel und Fluss bleiben)
                if (Soll && Autos.Count > 0 && jetzt - _letzterTakt > 1.0)
                {
                    foreach (var id in Autos.Values)
                    {
                        Mischer.Beenden(id);
                    }
                    Autos.Clear();
                    AutoLaut.Clear();
                }
                // Einzelrufe je Jahreszeit (im Winter keine)
                if (Soll && _lage is { VogelNah: true } && Tonplan.RufeFuer(_jahreszeit) is { } rufe)
                {
                    if (_naechsterRuf < 0)
                    {
                        _naechsterRuf = jetzt + Tonplan.NaechsterRuf(Zufall, rufe);
                    }
                    else if (jetzt >= _naechsterRuf)
                    {
                        _naechsterRuf = jetzt + Tonplan.NaechsterRuf(Zufall, rufe);
                        Anlass(new Tonanlass(Core.Spiel.Ton.Anlass.Vogelruf));
                    }
                }
            }
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben("Ton: Pflege: " + fehler.GetType().Name);
        }
    }

    /// <summary>Eigene Lieder nach der Wahl "Eigene Lieder spielen" (P8-17b); ohne Lieder keine Musik.</summary>
    private static void MusikPruefen()
    {
        if (Pausiert)
        {
            return;
        }
        var ort = _innen ? "innen" : "stadt";
        var soll = Soll && Lieder is not null && TonEinstellungen.MusikHier(_werte.MusikOrte, _innen) && Mischer.Faktor(Tongruppe.Musik) > 0
            && _vorbereitung is { IsCompleted: true };
        if (!soll)
        {
            if (_lied != 0)
            {
                Mischer.Beenden(_lied);
                _lied = 0;
            }
            _liedStill = null;
            return;
        }
        if (_lied != 0)
        {
            Mischer.Lautstaerke(_lied, _innen ? Tonplan.LiedInnen : Tonplan.LiedStadt);
            return;
        }
        if (_liedOeffnet || _liedStill == ort)
        {
            return;
        }
        var lieder = Lieder!;
        var still = Still;
        _liedOeffnet = true;
        Task.Run(() =>
        {
            var lied = lieder.Naechstes();
            lock (Sperre)
            {
                _liedOeffnet = false;
                if (lied is not { } gefunden)
                {
                    _liedStill = ort;
                    return;
                }
                var jetzt = _innen ? "innen" : "stadt";
                if (still || !Soll || jetzt != ort || !TonEinstellungen.MusikHier(_werte.MusikOrte, _innen))
                {
                    // stiller Dienst: nur zaehlen (Start-Test), je Ort einmal
                    gefunden.Strom.Dispose();
                    if (still && Soll && jetzt == ort)
                    {
                        Zaehlen("musik:" + ort, Tongruppe.Musik);
                        _liedStill = ort;
                    }
                    return;
                }
                Zaehlen("musik:" + ort, Tongruppe.Musik);
                _lied = Mischer.StromStarten(gefunden.Strom, Tongruppe.Musik, _innen ? Tonplan.LiedInnen : Tonplan.LiedStadt);
            }
        });
    }

    /// <summary>Lieder haben sich geaendert (hinzugefuegt, entfernt): neu waehlen.</summary>
    public static void LiederGeaendert()
    {
        lock (Sperre)
        {
            if (_lied != 0)
            {
                Mischer.Beenden(_lied);
                _lied = 0;
            }
            _liedStill = null;
            MusikPruefen();
        }
    }
}

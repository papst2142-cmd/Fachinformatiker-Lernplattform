using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 7, W4 (Weisung P7-9): Ablauf der 3D-Stadt ohne Oberflaeche, auf dem gemergten Stadt-Kern
/// (<see cref="Spiel.Stadt"/>: Raster, Wege von Tuer zu Tuer, Tagesuhr, Oeffnungszeiten) und einem
/// <see cref="Spielablauf"/>. Ein Weg wird sofort als Ereignis <c>stadt_gegangen</c> geschrieben
/// (die Uhr springt), die Figur geht ihn danach sichtbar Zelle fuer Zelle ab. Seit W5 (Weisung P7-10)
/// hat jeder Ort mit Ansicht (Buero, Zuhause, Kunde, Filiale, Serverfarm) einen begehbaren
/// <see cref="Innenraum"/> mit Wegen, Personen und Texten wie Python; in der Wohnung stellt
/// "Einrichten" Moebel um (Aktionen wie place_furniture_at, rotate_furniture, box_furniture).
/// Jede Aenderung am Spielstand laeuft ueber <see cref="Spielablauf"/>; ist ein anderer Vorgang am
/// Werk (<see cref="SpielBelegtAusnahme"/>), steht <see cref="AbgleichTexte.Belegt"/> in der Zeile.
/// Die Darstellung ruft <see cref="Fortschreiten"/> je Bild.
/// </summary>
public sealed class StadtSpiel
{
    /// <summary>Gehgeschwindigkeit in Karteneinheiten je Sekunde (fisi_game_gui 7.0 / 2).</summary>
    public const float Tempo = 3.5f;

    private readonly List<(double X, double Y)> _weg = [];
    private string? _zielOrt;
    private string? _danachZu;
    private object? _danach;   // Nachtrag 1 zu P7-12: naechstes Ziel (Ort-Id oder Bodenpunkt), erst nach der Ankunft

    public StadtSpiel(Stadt stadt, Spielablauf ablauf, GrafikBibliothek? grafik = null, PyObjekt? innenraeume = null)
    {
        Stadt = stadt;
        Ablauf = ablauf;
        Grafik = grafik;
        Innenraeume = innenraeume;
        Szene = StadtSzene.Bauen(stadt, ablauf.Stand, grafik);
        var hier = Szene.Ort(Jetzt.Ort);
        FigurX = hier?.StehX ?? 0;
        FigurZ = hier?.StehZ ?? 0;
        // Beim Oeffnen erklaert die Zeile die Bedienung (Weisung P7-9 Punkt 1); ab dem ersten Schritt Ort und Uhr.
        Zeile = SpielProbeTexte.Hinweis;
        Vorbereiten(Jetzt.Ort);
    }

    public Stadt Stadt { get; }

    /// <summary>CC0-Modelle der Stadt (null = eigene Geometrie).</summary>
    public GrafikBibliothek? Grafik { get; }

    public Spielablauf Ablauf { get; }

    /// <summary>Inhalt von inhalte_csharp/spiel/innenraeume.json (Serverfarm), null = ohne.</summary>
    public PyObjekt? Innenraeume { get; }

    public StadtSzene Szene { get; private set; }

    /// <summary>Der Innenraum, in dem die Figur ist (null = Stadt).</summary>
    public Innenraum? Innen { get; private set; }

    public bool ImInnenraum => Innen is not null;

    /// <summary>Knoepfe unter dem Text im Innenraum (Aktion, Beschriftung), nur die, die schon gehen.</summary>
    public IReadOnlyList<(string Aktion, string Name)> Knoepfe { get; private set; } = [];

    /// <summary>Wird gerufen, wenn sich der Innenraum (oder seine Einrichtung) geaendert hat.</summary>
    public event Action? InnenGeaendert;

    /// <summary>
    /// Die Figur steht frei in der Stadt (hingetippt, Nachtrag 1 Wunsch 3), nicht vor dem Ort, an dem
    /// sie zuletzt war: dann kein Hineingehen und kein Warten, bis sie wieder an einem Ort ankommt.
    /// </summary>
    public bool ImFreien { get; private set; }

    /// <summary>"stadt" oder die Ansicht des Innenraums (welche Flaeche die Darstellung zeigt).</summary>
    public string FlaecheId => Innen?.Ansicht ?? ProbeWelt.Stadt;

    public float FigurX { get; private set; }

    public float FigurZ { get; private set; }

    /// <summary>Blickrichtung der Figur in Grad um die Hochachse (0 = nach +Z).</summary>
    public float FigurRichtung { get; private set; }

    public string Zeile { get; private set; }

    public bool Geht => _weg.Count > 0;

    public (double X, double Y)? Ziel => _weg.Count > 0 ? _weg[^1] : null;

    /// <summary>Stand der Stadt aus dem Ereignisprotokoll (Tag, Ort, Uhr).</summary>
    public StadtStand Jetzt => Stadt.Stand(Ablauf.Stand);

    public string Kopfzeile
    {
        get
        {
            var jetzt = Jetzt;
            return string.Format(CultureInfo.InvariantCulture, StadtTexte.Kopfzeile, jetzt.Tag, Stadt.Uhrzeit(jetzt.Uhr));
        }
    }

    /// <summary>Wird bei jeder Aenderung von Ort, Uhr oder Zeile gerufen (Oberflaeche neu beschriften).</summary>
    public event Action? Geaendert;

    /// <summary>Wird gerufen, wenn sich die sichtbaren Orte oder Modellteile geaendert haben (Szene neu laden).</summary>
    public event Action? SzeneGeaendert;

    public string Name(string id) => Szene.Ort(id)?.Name ?? id;

    /// <summary>Zum Ort gehen: Ereignis schreiben (Uhr springt), dann den Weg sichtbar abgehen.</summary>
    public StadtGrund GeheZu(string ziel) => Gemessen(() => GeheZuOrt(ziel));

    private StadtGrund GeheZuOrt(string ziel)
    {
        if (ImInnenraum)
        {
            Verlassen();
        }
        if (Geht)
        {
            // Nachtrag 1 zu P7-12 (Wunsch 5): kein Sprung. Der laufende Weg steht schon ganz im
            // Protokoll (Uhr), darum geht die Figur ihn zu Ende und erst dann zum neuen Ziel
            _danach = ziel;
            Zeile = string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.DanachZu, Name(ziel));
            Geaendert?.Invoke();
            return StadtGrund.Ok;
        }
        var von = Jetzt.Ort;
        // von einem freien Punkt: Weg ueber das Laufraster bis vor die Tuer, die Schritte zaehlt dieser Weg
        List<(double X, double Y)>? frei = null;
        if (ImFreien && Szene.Ort(ziel) is { } zielOrt)
        {
            frei = [.. MitWeg(() => Szene.Laufraster.Weg((FigurX, FigurZ), (zielOrt.StehX, zielOrt.StehZ)))];
            if (frei.Count == 0 && !(Math.Abs(FigurX - zielOrt.StehX) < 0.01 && Math.Abs(FigurZ - zielOrt.StehZ) < 0.01))
            {
                Zeile = SpielProbeTexte.NichtErreichbar;
                Geaendert?.Invoke();
                return StadtGrund.KeinWeg;
            }
        }
        var ergebnis = Schreiben(() => frei is null ? Stadt.Gehen(Ablauf, ziel) : Stadt.Gehen(Ablauf, ziel, Schritte(frei)));
        if (ergebnis is null)
        {
            return StadtGrund.KeinProfil;
        }
        if (!ergebnis.Ok)
        {
            Zeile = StadtTexte.Grund(ergebnis.Grund);
            Geaendert?.Invoke();
            return ergebnis.Grund;
        }
        _weg.Clear();
        if (frei is not null)
        {
            _weg.AddRange(frei);
        }
        else
        {
            var zellen = Stadt.Weg(von, ziel)!.Zellen;
            _weg.AddRange(zellen.Skip(1).Select(z => ((z.X + 0.5) * Stadt.Zelle, (z.Y + 0.5) * Stadt.Zelle)));
            if (Szene.Ort(ziel) is { } o)
            {
                // die letzten Meter von der Tuerzelle auf den Stehplatz vor der Tuer
                _weg.Add((o.StehX, o.StehZ));
            }
        }
        ImFreien = false;
        _zielOrt = ziel;
        Vorbereiten(ziel);
        var daten = ergebnis.Daten!;
        Zeile = string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.Unterwegs, Name(ziel)) + " "
            + string.Format(CultureInfo.InvariantCulture, StadtTexte.Wegdauer, daten["schritte"], daten["minuten"]);
        if (_weg.Count == 0)
        {
            Ankommen();
        }
        Geaendert?.Invoke();
        return StadtGrund.Ok;
    }

    /// <summary>Schritte eines freien Wegs: seine Laenge in Zellen des Stadt-Rasters, aufgerundet.</summary>
    public long Schritte(IReadOnlyList<(double X, double Y)> weg) =>
        (long)Math.Ceiling((Wegsuche.Laenge((FigurX, FigurZ), weg) / Stadt.Zelle) - 1e-9);

    /// <summary>
    /// Zum Bodenpunkt gehen. Im Innenraum ohne Spielzeit; in der Stadt (Nachtrag 1, Wunsch 3) ueber
    /// Strassen, Wege, Wiese und Parks, nicht durch Haeuser, Wasser und Gleis (nur Bruecken und
    /// Uebergaenge). Die Schritte kosten Spielzeit wie ein Weg zu einem Ort (Ereignis stadt_gelaufen).
    /// </summary>
    public bool GeheZu(double x, double z) => Gemessen(() => GeheZuPunkt(x, z));

    private bool GeheZuPunkt(double x, double z)
    {
        if (Innen is not { } innen)
        {
            return InDerStadtGehen(x, z);
        }
        if (Einrichten)
        {
            return EinrichtenTippen(x, z);
        }
        // auf eine Person getippt: bis auf Reichweite heran (walk_path mit REACH), sonst zum Punkt
        var person = innen.PersonBei(x, z);
        var (zx, zz, reichweite) = person is not null ? (person.X, person.Z, Wege.Reichweite) : (x, z, 0d);
        if (person is null && Ort == SpielDaten.OrtZuhause && Wohnung.MoebelBei(Ablauf.Stand, x, z) is { } m)
        {
            // auf ein Moebel: bis neben das Moebel (furniture_near prueft 0,9 vom Rand)
            (zx, zz) = (m.X + (Py.F(m.W) / 2), m.Y + (Py.F(m.H) / 2));
            reichweite = Wohnung.MoebelReichweite + (Math.Min(Py.F(m.W), Py.F(m.H)) / 2);
        }
        else if (person is null && innen.WareBei(x, z) is { } ware)
        {
            // W7: auf eine Ware im Geschaeft: bis neben die Auslage
            (zx, zz) = (Py.F(ware["x"]) + (Py.F(ware["w"]) / 2), Py.F(ware["y"]) + (Py.F(ware["h"]) / 2));
            reichweite = Wohnung.MoebelReichweite + (Math.Min(Py.F(ware["w"]), Py.F(ware["h"])) / 2);
        }
        var weg = MitWeg(() => innen.Weg(FigurX, FigurZ, zx, zz, reichweite));
        _weg.Clear();
        _weg.AddRange(weg);
        if (weg.Count == 0)
        {
            // schon da (oder nicht erreichbar): Text zum Platz
            if (Math.Abs(FigurX - x) + Math.Abs(FigurZ - z) > 1.5 && person is null)
            {
                Zeile = SpielProbeTexte.NichtErreichbar;
                Knoepfe = [];
            }
            else
            {
                InnenText();
            }
        }
        Geaendert?.Invoke();
        return weg.Count > 0;
    }

    /// <summary>Python site des Innenraums (buero, zuhause, filiale, Kundenort, serverfarm), null in der Stadt.</summary>
    public string? Ort => Innen?.Ort;

    private bool InDerStadtGehen(double x, double z)
    {
        if (Geht)
        {
            // Nachtrag 1 zu P7-12 (Wunsch 5): wie bei einem Ort erst nach der Ankunft weiter
            _danach = (x, z);
            Zeile = SpielProbeTexte.DanachDorthin;
            Geaendert?.Invoke();
            return true;
        }
        var weg = MitWeg(() => Szene.Laufraster.Weg((FigurX, FigurZ), (x, z)));
        if (weg.Count == 0)
        {
            Zeile = SpielProbeTexte.NichtErreichbar;
            Geaendert?.Invoke();
            return false;
        }
        var schritte = Math.Max(1, Schritte(weg));
        var ergebnis = Schreiben(() => Stadt.Laufen(Ablauf, schritte));
        if (ergebnis is null)
        {
            return false;
        }
        if (!ergebnis.Ok)
        {
            Zeile = StadtTexte.Grund(ergebnis.Grund);
            Geaendert?.Invoke();
            return false;
        }
        _weg.Clear();
        _weg.AddRange(weg);
        _zielOrt = null;
        ImFreien = true;
        Zeile = string.Format(CultureInfo.InvariantCulture, StadtTexte.Wegdauer, ergebnis.Daten!["schritte"], ergebnis.Daten["minuten"]) + ".";
        Geaendert?.Invoke();
        return true;
    }

    /// <summary>Figur um <paramref name="sekunden"/> weiterbewegen; true, solange sie geht.</summary>
    public bool Fortschreiten(double sekunden)
    {
        if (_weg.Count == 0)
        {
            return false;
        }
        _imBild = true;
        try
        {
            return Weiter(sekunden);
        }
        finally
        {
            _imBild = false;
        }
    }

    // ---- Messung (Weisung P7-15) ----

    private double _aktionenMs;
    private bool _imBild;
    private bool _inAktion;

    /// <summary>
    /// Zeit in Aktionen der Oberflaeche (Gehen zu einem Ort oder Punkt, Warten, Betreten, Knopf, Verlassen) seit dem
    /// letzten Abruf, ohne die Aktionen, die beim Zeichnen eines Bildes entstehen (die zaehlen dort als Logik).
    /// </summary>
    public double AktionenMessen()
    {
        var wert = _aktionenMs;
        _aktionenMs = 0;
        return wert;
    }

    private double _wegMs;
    private double _ereignisMs;

    /// <summary>
    /// Weisung P7-16: Teile der Aktionen seit dem letzten Abruf: Wegsuche (Stadt und Innenraum) und
    /// Ereignis schreiben (Pruefen, Spielablauf, Datenbank). Sie stecken schon in <see cref="AktionenMessen"/>.
    /// </summary>
    public (double WegMs, double EreignisMs) TeileMessen()
    {
        var wert = (_wegMs, _ereignisMs);
        _wegMs = 0;
        _ereignisMs = 0;
        return wert;
    }

    private T MitEreignis<T>(Func<T> schreiben)
    {
        var beginn = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            return schreiben();
        }
        finally
        {
            _ereignisMs += System.Diagnostics.Stopwatch.GetElapsedTime(beginn).TotalMilliseconds;
        }
    }

    private T MitWeg<T>(Func<T> suche)
    {
        var beginn = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            return suche();
        }
        finally
        {
            _wegMs += System.Diagnostics.Stopwatch.GetElapsedTime(beginn).TotalMilliseconds;
        }
    }

    private T Gemessen<T>(Func<T> aktion)
    {
        if (_imBild || _inAktion)
        {
            return aktion();
        }
        var beginn = System.Diagnostics.Stopwatch.GetTimestamp();
        _inAktion = true;
        try
        {
            return aktion();
        }
        finally
        {
            _inAktion = false;
            _aktionenMs += System.Diagnostics.Stopwatch.GetElapsedTime(beginn).TotalMilliseconds;
        }
    }

    private bool Weiter(double sekunden)
    {
        var rest = sekunden * Tempo;
        while (rest > 0 && _weg.Count > 0)
        {
            var (zx, zz) = _weg[0];
            var dx = zx - FigurX;
            var dz = zz - FigurZ;
            var weite = Math.Sqrt((dx * dx) + (dz * dz));
            if (weite > 1e-6)
            {
                FigurRichtung = (float)(Math.Atan2(dx, dz) * 180 / Math.PI);
            }
            if (weite <= rest)
            {
                FigurX = (float)zx;
                FigurZ = (float)zz;
                rest -= weite;
                _weg.RemoveAt(0);
            }
            else
            {
                FigurX += (float)(dx / weite * rest);
                FigurZ += (float)(dz / weite * rest);
                rest = 0;
            }
        }
        if (_weg.Count == 0)
        {
            Ankommen();
        }
        return _weg.Count > 0;
    }

    /// <summary>Sofort ans Ziel (ohne 3D und im Start-Test).</summary>
    public void SofortAnkommen()
    {
        while (Fortschreiten(1000))
        {
        }
    }

    private void Ankommen()
    {
        if (ImInnenraum)
        {
            InnenText();
        }
        else if (_zielOrt is { } ort)
        {
            Zeile = OrtZeile(ort);
            _zielOrt = null;
            NeueSzene();
            Vorbereiten(ort);
            if (_danachZu is { } weiter && weiter == ort)
            {
                // "Zur Arbeit" / "Zurueck ins Buero": am Ziel gleich hinein
                _danachZu = null;
                Betreten();
            }
        }
        else if (ImFreien)
        {
            Zeile = SpielProbeTexte.ImFreien;
        }
        Geaendert?.Invoke();
        if (_danach is { } naechstes)
        {
            // das beim Gehen getippte Ziel: von hier aus weiter (nicht, wenn die Figur hineingegangen ist)
            _danach = null;
            if (!ImInnenraum && naechstes is string naechsterOrt)
            {
                GeheZu(naechsterOrt);
            }
            else if (!ImInnenraum && naechstes is ValueTuple<double, double> punkt)
            {
                InDerStadtGehen(punkt.Item1, punkt.Item2);
            }
        }
    }

    /// <summary>Zeile vor einem Ort: offen bis, immer offen, geschlossen (oeffnet um / heute nicht mehr).</summary>
    public string OrtZeile(string id)
    {
        var inv = CultureInfo.InvariantCulture;
        var name = Name(id);
        var uhr = Jetzt.Uhr;
        if (Stadt.Oeffnungszeit(id) is not { } zeit)
        {
            return string.Format(inv, SpielProbeTexte.ImmerOffen, name) + Innenraum(id);
        }
        if (Stadt.Offen(id, uhr))
        {
            return string.Format(inv, SpielProbeTexte.Offen, name, Stadt.Uhrzeit(zeit.Zu)) + Innenraum(id);
        }
        var oeffnet = zeit.Auf > uhr && zeit.Auf <= Stadt.Abend;
        return name + ": " + (oeffnet ? StadtTexte.Grund(StadtGrund.Geschlossen, Stadt.Uhrzeit(zeit.Auf)) : StadtTexte.OeffnetNichtMehr);
    }

    // der Ortsname steht schon vorn in der Zeile (Nachtrag 1: nicht doppelt)
    private string Innenraum(string id) => HatInnenraum(id) ? "" : " " + SpielProbeTexte.KeinInnenraum;

    /// <summary>Hat der Ort einen Innenraum (W5: Buero, Zuhause, Kunde, Filiale, Serverfarm)?</summary>
    public bool HatInnenraum(string id) =>
        Szene.Ort(id)?.Ansicht is { } ansicht && Probe.Innenraum.OrtFuer(ansicht) is { } ort
        && (ort != Probe.Innenraum.Serverfarm || Innenraeume is not null)
        && (ort != SpielDaten.OrtZuhause || Wohnung.Bleibe(Ablauf.Stand.Wohnung, Ablauf.Stand.Inhalt) is not null);

    public bool KannWarten => !ImInnenraum && !ImFreien && !Geht && Stadt.WartenPruefen(Ablauf.Stand).Ok;

    public bool KannBetreten => !ImInnenraum && !ImFreien && !Geht && HatInnenraum(Jetzt.Ort) && Stadt.Betreten(Ablauf.Stand) == StadtGrund.Ok;

    /// <summary>Vor dem geschlossenen Ort bis zur Oeffnung warten (Ereignis stadt_gewartet).</summary>
    public StadtGrund Warten() => Gemessen(WartenOhneMessung);

    private StadtGrund WartenOhneMessung()
    {
        if (Schreiben(() => Stadt.Warten(Ablauf)) is not { } ergebnis)
        {
            return StadtGrund.KeinProfil;
        }
        Zeile = ergebnis.Ok ? OrtZeile(Jetzt.Ort) : StadtTexte.Grund(ergebnis.Grund);
        Geaendert?.Invoke();
        return ergebnis.Grund;
    }

    public bool Betreten() => Gemessen(BetretenOhneMessung);

    private bool BetretenOhneMessung()
    {
        if (!KannBetreten)
        {
            return false;
        }
        var ansicht = Szene.Ort(Jetzt.Ort)?.Ansicht;
        var innen = Vorbereitet(ansicht) ?? Probe.Innenraum.Bauen(ansicht, Ablauf.Stand, Innenraeume, Grafik);
        if (innen is null)
        {
            return false;
        }
        _weg.Clear();
        Innen = innen;
        EinrichtenBeenden();
        FigurX = innen.StartX;
        FigurZ = innen.StartZ;
        FigurRichtung = 90;
        InnenText();
        InnenGeaendert?.Invoke();
        Geaendert?.Invoke();
        return true;
    }

    private Task<Innenraum?>? _vorbereitung;
    private string? _vorAnsicht;
    private Spielstand? _vorStand;

    /// <summary>
    /// Weisung P7-16: den Innenraum eines Orts ausserhalb des Oberflaechen-Threads vorbereiten (Grundriss,
    /// Netze, Laufraster), solange die Figur hingeht oder davor steht. Gebaut wird aus einer Kopie des
    /// Spielstands; benutzt wird das Ergebnis beim Betreten nur, wenn der Spielstand noch derselbe ist
    /// (jedes Ereignis ersetzt den Stand, STATE_COPY), sonst wird wie bisher sofort gebaut.
    /// </summary>
    public void Vorbereiten(string? ort)
    {
        var ansicht = ort is null ? null : Szene.Ort(ort)?.Ansicht;
        if (Probe.Innenraum.OrtFuer(ansicht) is null || (ansicht == _vorAnsicht && ReferenceEquals(_vorStand, Ablauf.Stand)))
        {
            return;
        }
        var stand = Ablauf.Stand;
        var kopie = stand.Kopie();
        var (innenraeume, grafik) = (Innenraeume, Grafik);
        _vorAnsicht = ansicht;
        _vorStand = stand;
        _vorbereitung = Task.Run(() =>
        {
            var innen = Probe.Innenraum.Bauen(ansicht, kopie, innenraeume, grafik);
            innen?.Vorbereiten();
            return innen;
        });
    }

    /// <summary>Der vorbereitete Innenraum zur Ansicht, wenn er zum jetzigen Spielstand passt (sonst null).</summary>
    private Innenraum? Vorbereitet(string? ansicht)
    {
        var aufgabe = _vorbereitung;
        var passt = aufgabe is not null && ansicht == _vorAnsicht && ReferenceEquals(_vorStand, Ablauf.Stand);
        _vorbereitung = null;
        _vorAnsicht = null;
        _vorStand = null;
        if (!passt)
        {
            return null;
        }
        try
        {
            // laeuft sie noch, ist Warten nie laenger als neu bauen
            var innen = aufgabe!.GetAwaiter().GetResult();
            if (innen is not null)
            {
                VorbereitetGenommen++;
            }
            return innen;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Wie oft das Betreten den vorbereiteten Innenraum genommen hat (Start-Test, Tests).</summary>
    public int VorbereitetGenommen { get; private set; }

    /// <summary>Text und Knoepfe zum Platz der Figur im Innenraum (place_message/home_message).</summary>
    private void InnenText()
    {
        if (Innen is not { } innen)
        {
            return;
        }
        var (titel, text, knoepfe) = innen.Nachricht(FigurX, FigurZ, Ablauf.Stand);
        Zeile = text.Length > 0 ? titel + "\n" + text : titel;
        if (_notiz is { } notiz)
        {
            // W6: Hinweis einmal ueber dem Text (Python notices, z. B. "Feierabend nach Arbeitstag 3")
            Zeile = notiz.Titel + "\n" + notiz.Text + "\n\n" + Zeile;
            _notiz = null;
        }
        Knoepfe = [.. knoepfe];
    }

    private (string Titel, string Text)? _notiz;

    /// <summary>W6: Hinweis, der einmal ueber dem Text des Innenraums steht (Python self.notices).</summary>
    public void NotizSetzen(string titel, string text)
    {
        _notiz = (titel, text);
        InnenText();
        Geaendert?.Invoke();
    }

    /// <summary>Aktion eines Knopfs, die die Oberflaeche ausfuehrt (Auftrag, Feierabend, Lernen, Schlafen, Personal).</summary>
    public event Action<string>? Aktion;

    /// <summary>
    /// W6: nach einer Aenderung ausserhalb der Stadt (Ticket geloest, Firma, Abgleich) Szene, Innenraum,
    /// Text und Knoepfe neu bestimmen; die Figur bleibt stehen.
    /// </summary>
    public void Auffrischen()
    {
        NeueSzene();
        InnenNeu();
        InnenText();
        Geaendert?.Invoke();
    }

    /// <summary>
    /// W6: nach dem Feierabend beginnt der neue Arbeitstag um 07:00 zu Hause (Stadt.Stand): hinaus,
    /// die Figur steht vor der Wohnung und geht gleich hinein.
    /// </summary>
    public void Neubeginn()
    {
        _weg.Clear();
        _danachZu = null;
        _danach = null;
        _zielOrt = null;
        ImFreien = false;
        Innen = null;
        EinrichtenBeenden();
        Knoepfe = [];
        NeueSzene();
        InnenGeaendert?.Invoke();
        var hier = Szene.Ort(Jetzt.Ort);
        FigurX = hier?.StehX ?? 0;
        FigurZ = hier?.StehZ ?? 0;
        FigurRichtung = 0;
        Zeile = OrtZeile(Jetzt.Ort);
        if (!Betreten())
        {
            Geaendert?.Invoke();
        }
    }

    /// <summary>
    /// Einen Knopf aus <see cref="Knoepfe"/> ausfuehren: "buero"/"zuhause" hinaus und zum Ort gehen (dort
    /// gleich hinein), alle anderen meldet <see cref="Aktion"/> an die Oberflaeche.
    /// </summary>
    public bool Knopf(string aktion) => Gemessen(() => KnopfOhneMessung(aktion));

    private bool KnopfOhneMessung(string aktion)
    {
        if (!Knoepfe.Any(k => k.Aktion == aktion))
        {
            return false;
        }
        if (aktion is not ("zuhause" or "buero"))
        {
            // W6: Auftrag, Feierabend, Lernen, Schlafen, Personal fuehrt die Oberflaeche aus (run_action)
            Aktion?.Invoke(aktion);
            return true;
        }
        var ziel = aktion == "zuhause"
            ? Szene.Orte.FirstOrDefault(o => o.Ansicht == "zuhause")
            : Szene.Orte.FirstOrDefault(o => o.Ansicht == "buero");
        Verlassen();
        if (ziel is null)
        {
            return true;
        }
        if (GeheZu(ziel.Id) == StadtGrund.Ok)
        {
            _danachZu = ziel.Id;
            if (!Geht && Jetzt.Ort == ziel.Id)
            {
                _danachZu = null;
                Betreten();
            }
        }
        return true;
    }

    public void Verlassen() => Gemessen(() => { VerlassenOhneMessung(); return true; });

    private void VerlassenOhneMessung()
    {
        if (!ImInnenraum)
        {
            return;
        }
        _weg.Clear();
        Innen = null;
        EinrichtenBeenden();
        Knoepfe = [];
        InnenGeaendert?.Invoke();
        var hier = Szene.Ort(Jetzt.Ort);
        FigurX = hier?.StehX ?? 0;
        FigurZ = hier?.StehZ ?? 0;
        FigurRichtung = 0;
        Zeile = OrtZeile(Jetzt.Ort);
        Geaendert?.Invoke();
    }

    // ---- Einrichten der Wohnung (W5) -----------------------------------------

    /// <summary>Modus "Einrichten" in der Wohnung: Tippen waehlt Moebel und stellt sie auf.</summary>
    public bool Einrichten { get; private set; }

    /// <summary>Das gewaehlte Moebelstueck (Schluessel wie Python "stueck"), null = keins.</summary>
    public string? Ausgewaehlt { get; private set; }

    /// <summary>Vorschau beim Aufstellen (placement_preview) oder null.</summary>
    public PyObjekt? Vorschau { get; private set; }

    /// <summary>Farbe der Vorschau: "gruen" passt, "gelb" versperrt einen Weg, "rot" geht nicht.</summary>
    public string VorschauFarbe { get; private set; } = "";

    private (double X, double Y)? _vorschauPunkt;
    private long _vorschauDreh;

    public bool KannEinrichten => Ort == SpielDaten.OrtZuhause && !Geht;

    /// <summary>"Einrichten" / "Fertig".</summary>
    public void EinrichtenUmschalten()
    {
        if (Einrichten)
        {
            EinrichtenBeenden();
            InnenText();
        }
        else if (KannEinrichten)
        {
            Einrichten = true;
            Zeile = Wohnung.Bleibe(Ablauf.Stand.Wohnung, Ablauf.Stand.Inhalt)?["name"] as string ?? "";
            Knoepfe = [];
        }
        Geaendert?.Invoke();
    }

    private void EinrichtenBeenden()
    {
        Einrichten = false;
        Ausgewaehlt = null;
        Vorschau = null;
        _vorschauPunkt = null;
    }

    /// <summary>Moebel im Karton (stueck, Name), wie boxed_furniture.</summary>
    public List<(string Stueck, string Name)> ImKarton() =>
        Ort != SpielDaten.OrtZuhause ? [] : [.. Wohnung.ImKarton(Ablauf.Stand).Select(k =>
            (Py.Str(k.Stueck), Py.Str(Wohnung.MoebelStueck(k.Moebel, Ablauf.Stand.Inhalt)?["name"] ?? k.Stueck)))];

    /// <summary>Name des gewaehlten Moebels.</summary>
    public string? AusgewaehltName =>
        Ausgewaehlt is { } s && Ablauf.Stand.Moebel.Get(s) is { } id ? Wohnung.MoebelStueck(id, Ablauf.Stand.Inhalt)?["name"] as string : null;

    /// <summary>Steht das gewaehlte Moebel in der Wohnung (sonst im Karton)?</summary>
    public bool AusgewaehltSteht => Ausgewaehlt is { } s && Py.Hat(SpielDaten.Objekt(Wohnung.Einrichtung(Ablauf.Stand)["moebel"]), s);

    /// <summary>Ein Moebel waehlen (aus dem Karton: "Aufstellen", dann auf den Boden tippen).</summary>
    public void Waehlen(string stueck)
    {
        if (!Einrichten || Ablauf.Stand.Moebel.Get(stueck) is null)
        {
            return;
        }
        Ausgewaehlt = stueck;
        Vorschau = null;
        _vorschauPunkt = null;
        _vorschauDreh = Platz(stueck) is { } p ? Py.Int(p[2]) : 0;
        Zeile = (AusgewaehltName ?? stueck) + "\n" + (AusgewaehltSteht ? SpielProbeTexte.EinrichtenVerschieben : SpielProbeTexte.EinrichtenAufstellen);
        Geaendert?.Invoke();
    }

    private List<object?>? Platz(string stueck) =>
        SpielDaten.Get(SpielDaten.Objekt(Wohnung.Einrichtung(Ablauf.Stand)["moebel"]), stueck) as List<object?>;

    private bool EinrichtenTippen(double x, double z)
    {
        if (Vorschau is { } v && Ausgewaehlt is not null
            && Py.F(v["x"]) <= x && x <= Py.F(v["x"]) + Py.F(v["w"]) && Py.F(v["y"]) <= z && z <= Py.F(v["y"]) + Py.F(v["h"]))
        {
            // zweites Tippen auf die Vorschau stellt auf
            return Aufstellen();
        }
        var moebel = Wohnung.MoebelBei(Ablauf.Stand, x, z);
        if (moebel is not null && moebel.Stueck != Ausgewaehlt && Vorschau is null)
        {
            Waehlen(moebel.Stueck);
            return true;
        }
        if (Ausgewaehlt is not { } stueck)
        {
            if (moebel is not null)
            {
                Waehlen(moebel.Stueck);
                return true;
            }
            return false;
        }
        _vorschauPunkt = (x, z);
        VorschauRechnen(stueck);
        return true;
    }

    private void VorschauRechnen(string stueck)
    {
        if (_vorschauPunkt is not var (x, z))
        {
            return;
        }
        Vorschau = Wohnung.Vorschau(Ablauf.Stand, stueck, x, z, _vorschauDreh);
        var (text, farbe) = Wohnung.VorschauText(Vorschau);
        VorschauFarbe = farbe;
        Zeile = (AusgewaehltName ?? stueck) + "\n" + text;
        Geaendert?.Invoke();
    }

    /// <summary>"Aufstellen": die Vorschau wird zur Einrichtung (place_furniture_at).</summary>
    public bool Aufstellen()
    {
        if (Ausgewaehlt is not { } stueck || _vorschauPunkt is not var (x, z) || Vorschau is null || Py.Wahr(Vorschau["problem"]))
        {
            return false;
        }
        var dreh = _vorschauDreh;
        if (!Aendern(() => SpielAktionen.AufstellenBei(Ablauf.Stand, stueck, x, z, dreh)))
        {
            return false;
        }
        Vorschau = null;
        _vorschauPunkt = null;
        Waehlen(stueck);
        return true;
    }

    /// <summary>"Drehen": in der Vorschau eine Vierteldrehung, sonst das aufgestellte Moebel (rotate_furniture).</summary>
    public bool Drehen()
    {
        if (Ausgewaehlt is not { } stueck)
        {
            return false;
        }
        _vorschauDreh = (_vorschauDreh + 1) % 4;
        if (Vorschau is not null || !AusgewaehltSteht)
        {
            VorschauRechnen(stueck);
            return true;
        }
        var ok = Aendern(() => SpielAktionen.Drehen(Ablauf.Stand, stueck));
        if (ok)
        {
            Waehlen(stueck);
        }
        return ok;
    }

    /// <summary>"In den Karton" (box_furniture).</summary>
    public bool InDenKarton()
    {
        if (Ausgewaehlt is not { } stueck || !AusgewaehltSteht)
        {
            return false;
        }
        var ok = Aendern(() => SpielAktionen.Einpacken(Ablauf.Stand, stueck));
        if (ok)
        {
            Waehlen(stueck);
        }
        return ok;
    }

    /// <summary>
    /// W6: Weg oder Warten schreiben (Spielstand in der Datenbank). Laeuft ein anderer Vorgang, steht
    /// AbgleichTexte.Belegt in der Zeile und nichts ist geschrieben (null); ebenso bei einem Fehler des Spiels.
    /// </summary>
    private StadtErgebnis? Schreiben(Func<StadtErgebnis> aktion)
    {
        try
        {
            return MitEreignis(aktion);
        }
        catch (SpielBelegtAusnahme)
        {
            Zeile = AbgleichTexte.Belegt;
        }
        catch (SpielFehler e)
        {
            Zeile = e.Message;
        }
        Geaendert?.Invoke();
        return null;
    }

    /// <summary>
    /// Eine Aenderung des Spielstands ausfuehren (Ereignisse ueber den Spielablauf, also ueber den Weg
    /// aus W8). Fehler des Spiels stehen in der Zeile; laeuft ein anderer Vorgang, AbgleichTexte.Belegt.
    /// </summary>
    private bool Aendern(Func<List<(string Art, PyObjekt Daten)>> aktion)
    {
        try
        {
            MitEreignis(() =>
            {
                var ereignisse = aktion();
                return ereignisse.Count > 0 ? Ablauf.Ausfuehren(ereignisse) : null;
            });
        }
        catch (SpielBelegtAusnahme)
        {
            Zeile = AbgleichTexte.Belegt;
            Geaendert?.Invoke();
            return false;
        }
        catch (SpielFehler e)
        {
            Zeile = e.Message;
            Geaendert?.Invoke();
            return false;
        }
        InnenNeu();
        return true;
    }

    /// <summary>Innenraum nach einer Aenderung neu bauen (Einrichtung, Boden), Figur bleibt stehen.</summary>
    private void InnenNeu()
    {
        if (Innen is not { } alt)
        {
            return;
        }
        Innen = Probe.Innenraum.Bauen(alt.Ansicht, Ablauf.Stand, Innenraeume, Grafik) ?? alt;
        InnenGeaendert?.Invoke();
    }

    /// <summary>Szene neu bauen, wenn sich sichtbare Orte oder Modellteile geaendert haben.</summary>
    private void NeueSzene()
    {
        // Nachtrag 1 zu P7-13 (Wunsch 4): erst die Signatur vergleichen, nur bei einer Aenderung neu bauen
        if (StadtSzene.SignaturFuer(Stadt, Ablauf.Stand) != Szene.Signatur)
        {
            Szene = StadtSzene.Bauen(Stadt, Ablauf.Stand, Grafik);
            SzeneGeaendert?.Invoke();
        }
    }
}

using System.Diagnostics;
using System.Numerics;
using Avalonia.OpenGL;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Nico 1c 3b 4a 5b): im Spiel mit fester Kamera liegt nur die Umgebung der Figur
/// auf der Grafikkarte. Immer geladen: Boden (zwei Quader und Gelaende), Fernkaesten aller Haeuser, Instancing, Ampel-Lichter.
/// Je Kachel (16 × 16): ein Netz aus Stadtnetz, Paket-Netz und Kleingruen (nach Zellen, nur Zellen im Bild) und ihre Haeuser.
/// Welche Kacheln, entscheidet <see cref="Nachlader"/> (Engine, ohne Grafikkarte). Gebaut wird im Hintergrund, hochgeladen in
/// Stuecken (hoechstens <see cref="HochladenJeBildMs"/> je Bild, eine noetige Kachel im Bild sofort). Jede Kachel im Sichtkegel
/// wird in jedem Bild gezeichnet: nah (geladen) oder fern (ihre Fernkaesten); <see cref="KachelnLoch"/> zaehlt Kacheln, bei
/// denen beides fehlt (muss 0 sein). Dazu der leichte Dunst (<see cref="Dunst"/>). Mit offenem Kamerabereich (Messungen der
/// Gesamtansicht) und mit <see cref="NachladenAusFuerTests"/> laedt die Flaeche alles wie bis A 0.81.2, ohne Dunst.
/// </summary>
public sealed partial class StadtFlaeche
{
    /// <summary>
    /// Gegenprobe Welle Nachladen (nur Tests und Start-Test, je Ablauf): alles laden und zeichnen wie bis A 0.81.2, ohne Dunst.
    /// Gemerkt wird der Wert beim Setzen des Spiels und bei <see cref="ProbeNeuLaden"/> (im Ablauf des Aufrufers).
    /// </summary>
    public static bool NachladenAusFuerTests { get => NachladenAus.Value; set => NachladenAus.Value = value; }

    private static readonly AsyncLocal<bool> NachladenAus = new();

    private bool _nachladenAusGemerkt;
    private Nachlader? _lader;
    private GlNetz? _boden;
    private GlNetz? _fernKaesten;
    private readonly Dictionary<Stadtteile.Kachel, (GlNetz Fest, List<StadtHaus> Haeuser)> _kachelGl = [];
    private readonly List<(int Start, int Anzahl)> _fernLaeufe = [];
    private float _dunstJetzt;
    private Vector3 _dunstFarbe;

    /// <summary>Laeuft das Nachladen (Spiel mit fester Kamera, Kamerabereich fest, keine Gegenprobe)?</summary>
    public bool NachladenAktiv => _lader is not null;

    /// <summary>Der Nachlader der Szene (Start-Test: Netzdaten, Bauzeiten), null ohne Nachladen.</summary>
    public Nachlader? Lader => _lader;

    /// <summary>Kacheln im Sichtkegel im letzten Bild, davon nah gezeichnet und fern gezeichnet; Loch = keins von beiden.</summary>
    public int KachelnImBild { get; private set; }

    public int KachelnNah { get; private set; }

    public int KachelnFern { get; private set; }

    public int KachelnLoch { get; private set; }

    /// <summary>Nur Start-Test und Leistungsprobe: Ziel einer Ankunft vorgeben (sonst das Ende des Wegs der Figur).</summary>
    public (float X, float Z)? ZielFuerProbe { get; set; }

    /// <summary>So lange darf ein Bild noetige Kacheln im Bild selbst bauen (danach fern, das naechste Bild baut weiter).</summary>
    public const double SofortBisMs = 120;

    /// <summary>Hochladen von Kacheln im letzten Bild in ms (Teil von „Kachel nachladen“).</summary>
    public double KachelHochladenMs { get; private set; }

    /// <summary>Alle Hochladezeiten je Kachel in ms (Messpunkt „Kachel nachladen“, Hochladen).</summary>
    public List<double> KachelHochladenZeiten { get; } = [];

    /// <summary>Soll die Szene mit Nachladen geladen werden? Nur draussen relevant; innen bleibt der Zustand.</summary>
    private bool NachladenGewuenscht()
    {
        if (_nachladenAusGemerkt || _spiel is null)
        {
            return false;
        }
        if (NachladenGrosseKarte)
        {
            // Werkzeug (Welle S 0.55.0): nur grosse Karten, Mitte = Ziel der Kamera
            return _spiel.Szene.Breite > Stadtteile.AltesFormatBis || _spiel.Szene.Tiefe > Stadtteile.AltesFormatBis;
        }
        if (!FesteKamera || !_kameras.TryGetValue(ProbeWelt.Stadt, out var k))
        {
            return false;
        }
        return k.Bereich.Max <= SpielKamera.AbstandMax;
    }

    /// <summary>
    /// Welle S 0.55.0 (Weisung P12-282 Punkt 3): das Werkzeug laedt Karten groesser als <see cref="Stadtteile.AltesFormatBis"/>
    /// nach wie das Spiel (gleiche Engine-Rechnung: <see cref="Nachlader"/>, <see cref="Ladeplan"/>), nur mit freier Kamera:
    /// Mitte des Nachladens ist das Ziel der Kamera statt der Figur. Kleine Karten (Stadt 1) bleiben wie bisher ganz geladen.
    /// </summary>
    public bool NachladenGrosseKarte { get; init; }

    /// <summary>Mitte des Nachladens: im Spiel die Figur, im Werkzeug das Ziel der Kamera.</summary>
    private (float X, float Z) NachladenMitte(StadtSpiel spiel) =>
        NachladenGrosseKarte && _kameras.TryGetValue(ProbeWelt.Stadt, out var k) ? (k.ZielX, k.ZielZ) : (spiel.FigurX, spiel.FigurZ);

    /// <summary>Gegenprobe merken (im Ablauf des Aufrufers, siehe <see cref="NachladenAusFuerTests"/>).</summary>
    private void NachladenAusMerken() => _nachladenAusGemerkt = NachladenAusFuerTests;

    /// <summary>Alle Netze des Nachladens auf der Grafikkarte freigeben (gl null: Zusammenhang verloren).</summary>
    private void NachladenFrei(GlInterface? gl)
    {
        if (gl is not null)
        {
            _boden?.Freigeben(gl);
            _fernKaesten?.Freigeben(gl);
            foreach (var (fest, _) in _kachelGl.Values)
            {
                fest.Freigeben(gl);
            }
        }
        _boden = null;
        _fernKaesten = null;
        _kachelGl.Clear();
        _lader = null;
    }

    /// <summary>Die Grundlage hochladen (Boden, Fernkaesten, Instancing, Ampel-Lichter); Kacheln danach.</summary>
    private void NachladenGrundlage(GlInterface gl, Nachlader lader)
    {
        NachladenFrei(gl);
        _lader = lader;
        var g = lader.Grundlage;
        _boden = GlNetz.Laden(gl, g.Boden);
        _fernKaesten = GlNetz.Laden(gl, g.FernKaesten);
        InstanzenLaden(gl, g.Instanzen);
        _ampelLichter?.Freigeben(gl);
        _ampelLichter = g.AmpelLichter.Count > 0 ? GlNetz.LadenMitNummern(gl, g.AmpelLichter) : null;
    }

    /// <summary>Szene neu laden mit Nachladen (im Bild, ohne Vorladen): Grundlage und noetige Kacheln sofort.</summary>
    private void NachladenSzeneLaden(GlInterface gl, StadtSpiel spiel)
    {
        foreach (var alt in _flaechen.Values.Concat(_haeuser.Values).Concat(_haeuserFern.Values).Concat(_paket.Values).Append(_gruen))
        {
            alt?.Freigeben(gl);
        }
        _flaechen.Clear();
        _haeuser.Clear();
        _haeuserFern.Clear();
        _hausKugel.Clear();
        _paket.Clear();
        _gruen = null;
        var lader = new Nachlader(spiel.Szene, _gw.Gruen(SpielKamera.AbstandMax));
        NachladenGrundlage(gl, lader);
        var (mx, mz) = NachladenMitte(spiel);
        lader.Takt(mx, mz);
        lader.StartBauen(lader.Noetig);
        foreach (var k in lader.Noetig)
        {
            KachelHochladen(gl, lader.JetztBauen(k));
        }
    }

    private void KachelHochladen(GlInterface gl, Nachlader.Paket p)
    {
        if (_kachelGl.ContainsKey(p.Kachel))
        {
            return;
        }
        var beginn = Stopwatch.GetTimestamp();
        var fest = GlNetz.Laden(gl, p.Fest);
        var haeuser = new List<StadtHaus>();
        foreach (var (haus, netz) in p.Haeuser)
        {
            _haeuser[haus] = GlNetz.Laden(gl, netz);
            _hausKugel[haus] = netz.Kugel();
            haeuser.Add(haus);
        }
        _kachelGl[p.Kachel] = (fest, haeuser);
        _lader?.Hochgeladen(p);
        var ms = MsSeit(beginn);
        KachelHochladenMs += ms;
        KachelHochladenZeiten.Add(ms);
    }

    private void KachelFreigeben(GlInterface gl, Stadtteile.Kachel k)
    {
        if (!_kachelGl.Remove(k, out var e))
        {
            return;
        }
        e.Fest.Freigeben(gl);
        foreach (var haus in e.Haeuser)
        {
            if (_haeuser.Remove(haus, out var netz))
            {
                netz.Freigeben(gl);
            }
            _hausKugel.Remove(haus);
        }
    }

    /// <summary>Kugel um eine Kachel (Boden bis zur hoechsten Stelle eines Hauses, mindestens 6 fuer Baeume und Laternen).</summary>
    private (Vector3 Mitte, float Halb) KachelKugel(Stadtteile.Kachel k) => Nachlader.Kugel(_lader?.Grundlage, k);

    /// <summary>Ein Takt vor dem Zeichnen: freigeben, hochladen (in Stuecken), noetige Kacheln im Bild sofort.</summary>
    private void NachladenTakt(GlInterface gl, StadtSpiel spiel, Matrix4x4 sichtProj)
    {
        KachelHochladenMs = 0;
        if (_lader is not { } lader)
        {
            return;
        }
        // Ankunft mit Bus oder Zug: liegt das Ende des Wegs ausserhalb des Rings, wird es vorher vorbereitet
        lader.Ziel = ZielFuerProbe ?? (!NachladenGrosseKarte && spiel.WegEnde is { } ende && !spiel.ImInnenraum
            && MathF.Abs(ende.X - spiel.FigurX) + MathF.Abs(ende.Z - spiel.FigurZ) > Ladeplan.Vor ? ende : null);
        var (mx, mz) = NachladenMitte(spiel);
        foreach (var k in lader.Takt(mx, mz))
        {
            KachelFreigeben(gl, k);
        }
        // eine noetige Kachel im Bild, die noch fehlt, sofort (naechste zuerst); nach SofortBisMs in diesem Bild nur noch
        // vorbereitete (die uebrigen zeichnet das Bild fern, nie ein Loch; das naechste Bild holt sie)
        var sofort = Stopwatch.GetTimestamp();
        foreach (var k in lader.Noetig)
        {
            if (!_kachelGl.ContainsKey(k) && KachelKugel(k) is var kugel && FestUebergang.ImSichtkegel(sichtProj, kugel.Mitte, kugel.Halb)
                && (lader.Bereit.ContainsKey(k) || MsSeit(sofort) < SofortBisMs))
            {
                KachelHochladen(gl, lader.JetztBauen(k));
            }
        }
        var beginn = Stopwatch.GetTimestamp();
        while (lader.Naechste() is { } p)
        {
            KachelHochladen(gl, p);
            if (MsSeit(beginn) >= HochladenJeBildMs)
            {
                break;
            }
        }
        if (lader.Naechste() is not null || lader.Fehlend().Any())
        {
            // weiter, bis alles Noetige oben ist (auch wenn die Figur steht)
            _weiterBis = Math.Max(_weiterBis, _uhr.Elapsed.TotalSeconds + 0.5);
        }
    }

    /// <summary>Boden, Kacheln im Bild (nah) und Fernkaesten der uebrigen Kacheln im Bild (fern), mit Zaehlung.</summary>
    private unsafe void NachladenZeichnen(GlInterface gl, Matrix4x4 sichtProj)
    {
        if (_lader is not { } lader || _boden is null)
        {
            return;
        }
        ZeichnenFest(gl, _boden, sichtProj);
        int imBild = 0, nah = 0, fern = 0, loch = 0;
        _fernLaeufe.Clear();
        foreach (var k in Stadtteile.Alle(lader.Szene.Breite, lader.Szene.Tiefe))
        {
            var kugel = KachelKugel(k);
            if (!OhneSparenFuerTests && !FestUebergang.ImSichtkegel(sichtProj, kugel.Mitte, kugel.Halb))
            {
                continue;
            }
            imBild++;
            if (_kachelGl.TryGetValue(k, out var e))
            {
                nah++;
                ZeichnenFestImBild(gl, e.Fest, sichtProj);
                continue;
            }
            if (lader.Noetig.Contains(k))
            {
                // eine noetige Kachel im Bild fehlt (darf nach dem Takt nie sein)
                loch++;
            }
            fern++;
            if (lader.Grundlage.FernBereiche.TryGetValue(k, out var b) && b.Anzahl > 0)
            {
                _fernLaeufe.Add(b);
            }
        }
        if (_fernLaeufe.Count > 0 && _fernKaesten is { } fk)
        {
            _fernLaeufe.Sort((a, b) => a.Start.CompareTo(b.Start));
            var modell = Matrix4x4.Identity;
            var mvp = modell * sichtProj;
            gl.UniformMatrix4fv(_ortMvp, 1, false, &mvp);
            gl.UniformMatrix4fv(_ortModell, 1, false, &modell);
            fk.Binden(gl);
            var (start, anzahl) = _fernLaeufe[0];
            for (var i = 1; i <= _fernLaeufe.Count; i++)
            {
                if (i < _fernLaeufe.Count && _fernLaeufe[i].Start == start + anzahl)
                {
                    anzahl += _fernLaeufe[i].Anzahl;
                    continue;
                }
                gl.DrawElements(GL_TRIANGLES, anzahl, GlUnsignedInt, new IntPtr((long)start * sizeof(uint)));
                _aufrufe++;
                _dreiecke += anzahl / 3;
                _festAufrufe++;
                _festDreiecke += anzahl / 3;
                if (i < _fernLaeufe.Count)
                {
                    (start, anzahl) = _fernLaeufe[i];
                }
            }
        }
        (KachelnImBild, KachelnNah, KachelnFern, KachelnLoch) = (imBild, nah, fern, loch);
    }

    /// <summary>Dunst fuer dieses Bild: nur draussen mit Nachladen im Spiel (sonst 0 = Bild wie vorher; das Werkzeug zeigt die
    /// ganze Karte mit freier Kamera ohne Dunst, Welle S 0.55.0).</summary>
    private void DunstFestlegen(bool draussen, Vector3 himmel)
    {
        _dunstJetzt = draussen && _lader is not null && !NachladenGrosseKarte ? Dunst.Staerke : 0f;
        _dunstFarbe = himmel;
    }

    /// <summary>Die Werte des Dunstes in ein Programm (Orte: uDunst, uDunstVon, uDunstBis, uDunstR, uDunstG, uDunstB).</summary>
    private void DunstSetzen(GlInterface gl, int[] orte)
    {
        gl.Uniform1f(orte[0], _dunstJetzt);
        gl.Uniform1f(orte[1], Dunst.Von);
        gl.Uniform1f(orte[2], Dunst.Bis);
        gl.Uniform1f(orte[3], _dunstFarbe.X);
        gl.Uniform1f(orte[4], _dunstFarbe.Y);
        gl.Uniform1f(orte[5], _dunstFarbe.Z);
    }

    private static int[] DunstOrte(GlInterface gl, int programm) =>
        [.. new[] { "uDunst", "uDunstVon", "uDunstBis", "uDunstR", "uDunstG", "uDunstB" }.Select(n => gl.GetUniformLocationString(programm, n))];
}

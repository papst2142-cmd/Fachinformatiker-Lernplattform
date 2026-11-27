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
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtFlaeche"/> mit Figur, Markierung, Saetzen
/// des Verkehrs, Autos und Fest-Netzen. Reiner Umzug aus StadtFlaeche.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtFlaeche
{
    /// <summary>Kreis um die Spielfigur (Radius 0,22 = 1,1 m), neu geladen, wenn sich die Kreisfarbe aendert.</summary>
    private GlNetz Ring(GlInterface gl, PyObjekt? aussehen)
    {
        if (_ring is not null && ReferenceEquals(aussehen, _ringAussehen))
        {
            return _ring;
        }
        _ringAussehen = aussehen;
        var schluessel = Aussehen.Normalisieren(aussehen)["kreis"] as string ?? "";
        if (_ring is null || schluessel != _ringSchluessel)
        {
            _ring?.Freigeben(gl);
            _ring = GlNetz.Laden(gl, Figur.Ring(aussehen, RingRadius, 0.06f));
            _ringSchluessel = schluessel;
        }
        return _ring;
    }

    /// <summary>Radius des Kreises um die Figur in der Stadt.</summary>
    public const float RingRadius = 0.22f;

    /// <summary>Schluessel einer Figur: Person, Aussehen, Kleidung und (beim Gehen) Bild der Schrittbewegung.</summary>
    private static string FigurSchluessel(string id, PyObjekt? aussehen, IReadOnlyList<Figur.Stueck>? kleidung, string? bewegung = null, int bild = -1) =>
        id + "|" + string.Join(",", Aussehen.Normalisieren(aussehen).Select(p => p.Value))
        + (kleidung is { Count: > 0 } ? "|" + string.Join(",", kleidung.Select(k => k.Slot + ":" + k.Form + ":" + k.Farbe)) : "")
        + (bild >= 0 && bewegung is not null ? "|" + Figur.BildName(bewegung, bild) : "");

    /// <summary>Figur (Hoehe 1) mit Aussehen (die Spielfigur mit Kleidung aus dem Kaufhaus), je Person einmal geladen.</summary>
    private (IReadOnlyList<Figur.Stueck> Von, IReadOnlyList<Figur.Stueck> Ohne)? _ohneKopf;

    /// <summary>Welle A 0.81.0: die Kleidung ohne Kopfbedeckung (im Bett), dieselbe Liste, solange sich die Kleidung nicht aendert.</summary>
    private IReadOnlyList<Figur.Stueck> OhneKopf(IReadOnlyList<Figur.Stueck> kleidung)
    {
        if (_ohneKopf is not { } k || !ReferenceEquals(k.Von, kleidung))
        {
            k = (kleidung, [.. kleidung.Where(s => s.Slot != "kopf")]);
            _ohneKopf = k;
        }
        return k.Ohne;
    }

    private GlNetz FigurNetz(GlInterface gl, string id, PyObjekt? aussehen, IReadOnlyList<Figur.Stueck>? kleidung = null, string? bewegung = null, int bild = -1,
        bool ohneKopf = false)
    {
        if (ohneKopf)
        {
            // Welle A 0.92.0 (Weisung P12-301 Punkt 3): eigene Figur in der Ich-Sicht ohne Kopf, je Bild der Bewegung einmal gebaut
            var sk = FigurSchluessel(id, aussehen, kleidung, bewegung, bild) + "|ohneKopf";
            if (!_figuren.TryGetValue(sk, out var nk))
            {
                var grafik = _spiel?.Grafik;
                nk = GlNetz.Laden(gl, _figurenCpu.GetOrAdd(sk, _ => Figur.OhneKopf(grafik, aussehen, kleidung, bewegung, bild, IchSicht.KoerperBis)));
                _figuren[sk] = nk;
            }
            return nk;
        }
        if (bild >= 0 && bewegung is not null)
        {
            // Bilder der Schrittbewegung: je Aussehen, Kleidung und Bild einmal gebaut (Sammelliste 125)
            var sb = FigurSchluessel(id, aussehen, kleidung, bewegung, bild);
            if (!_figuren.TryGetValue(sb, out var nb))
            {
                var grafik = _spiel?.Grafik;
                nb = GlNetz.Laden(gl, _figurenCpu.GetOrAdd(sb, _ => Figur.Netz(grafik, aussehen, 1f, kleidung, bewegung, bild)));
                _figuren[sb] = nb;
            }
            return nb;
        }
        // Wunsch 4: dasselbe Aussehen wie im letzten Bild ohne neuen Schluessel (der kostete je Bild und
        // Figur ein neues Woerterbuch und Zeichenketten, also Arbeit fuer die Speicherbereinigung);
        // StadtSpiel.Kleidung gibt dieselbe Liste zurueck, solange sich nichts aendert
        if (_figurZuletzt.TryGetValue(id, out var zuletzt) && ReferenceEquals(zuletzt.Aussehen, aussehen) && ReferenceEquals(zuletzt.Kleidung, kleidung))
        {
            return zuletzt.Netz;
        }
        var schluessel = FigurSchluessel(id, aussehen, kleidung);
        if (!_figuren.TryGetValue(schluessel, out var netz))
        {
            var (grafik, innen) = (_spiel?.Grafik, _spiel?.Innen);
            netz = GlNetz.Laden(gl, _figurenCpu.GetOrAdd(schluessel, _ => kleidung is { Count: > 0 } ? Figur.Netz(grafik, aussehen, 1f, kleidung)
                : innen?.PersonNetz(id, aussehen) ?? Figur.Netz(grafik, aussehen, 1f)));
            _figuren[schluessel] = netz;
        }
        _figurZuletzt[id] = (aussehen, kleidung, netz);
        return netz;
    }

    /// <summary>Vorschau beim Einrichten (gruen/gelb/rot) und Rahmen um das gewaehlte Moebel; null = nichts.</summary>
    private GlNetz? Markierung(GlInterface gl)
    {
        if (_spiel is not { Einrichten: true } spiel)
        {
            return null;
        }
        var teile = new List<Teil>();
        if (spiel.Vorschau is { } v)
        {
            var farbe = Netz.Farbe(spiel.VorschauFarbe switch { "gruen" => "#34D399", "gelb" => "#FBBF24", _ => "#F87171" });
            float x = (float)Py.F(v["x"]), z = (float)Py.F(v["y"]), w = (float)Py.F(v["w"]), t = (float)Py.F(v["h"]);
            teile.Add(new Teil(TeilArt.Quader, x, 0.04f, z, x + w, 0.9f, z + t, farbe));
        }
        else if (spiel.Ausgewaehlt is { } stueck && Wohnung.AufgestellteMoebel(spiel.Ablauf.Stand).FirstOrDefault(m => m.Stueck == stueck) is { } m)
        {
            float x = (float)m.X, z = (float)m.Y, w = (float)Py.F(m.W), t = (float)Py.F(m.H);
            var farbe = Netz.Farbe("#22D3EE");
            teile.Add(new Teil(TeilArt.Quader, x - 0.08f, 0.04f, z - 0.08f, x + w + 0.08f, 0.07f, z + t + 0.08f, farbe));
        }
        if (teile.Count == 0)
        {
            return null;
        }
        var schluessel = string.Join(";", teile);
        if (schluessel != _markierungSchluessel || _markierung is null)
        {
            _markierung?.Freigeben(gl);
            _markierung = GlNetz.Laden(gl, Netz.Aus(teile));
            _markierungSchluessel = schluessel;
        }
        return _markierung;
    }


    /// <summary>Alle Autos in einem Zeichenaufruf (eigenes Programm), danach wieder das Programm der Szene.</summary>
    /// <summary>
    /// Phase 8 W11: Autos oder Festgestalten setzen (neuer Verkehr nach <see cref="FestVerkehr.VerkehrFuer"/>, die
    /// Netze der Autos bzw. Gestalten werden neu geladen).
    /// </summary>
    private void SatzSetzen(GlInterface gl, StadtSpiel spiel, FestVerkehr.Satz satz, double jetzt) =>
        SatzEinsetzen(gl, SatzVorbereitetNehmen(spiel.Szene, satz) ?? SatzPaketBauen(spiel.Szene, spiel.Grafik!, satz, _gw.AutosHalb), jetzt);

    /// <summary>Weisung P8-55 (W12): Gestalten, Verkehr und beide Netze (nah, fern) eines Satzes, auf dem Prozessor gebaut.</summary>
    /// <remarks>Welle Grafik G1: <paramref name="Halb"/> = Netze mit jedem zweiten Auto gebaut (Stufe Niedrig).</remarks>
    private sealed record SatzPaket(StadtSzene Szene, FestVerkehr.Satz Satz, List<FestVerkehr.Gestalt>? Gestalten, Verkehr Verkehr, List<Netz> Nah, List<Netz> Fern, bool Halb);

    private static SatzPaket SatzPaketBauen(StadtSzene szene, GrafikBibliothek grafik, FestVerkehr.Satz satz, bool halb)
    {
        var gestalten = satz == FestVerkehr.Satz.Autos ? null : FestVerkehr.Gestalten(grafik, satz);
        var verkehr = FestVerkehr.VerkehrFuer(szene, satz);
        return new SatzPaket(szene, satz, gestalten, verkehr, AutoNetze(verkehr, gestalten, grafik, false, halb), AutoNetze(verkehr, gestalten, grafik, true, halb), halb);
    }

    /// <summary>Netze fuer den einen Zeichenaufruf der Autos bzw. Festgestalten (nah oder fern); Welle Grafik G1: <paramref name="halb"/> = jedes zweite.</summary>
    private static List<Netz> AutoNetze(Verkehr verkehr, List<FestVerkehr.Gestalt>? gestalten, GrafikBibliothek grafik, bool fern, bool halb) =>
        FestVerkehr.FahrzeugNetze(verkehr, gestalten, grafik, fern, AutosHoechstens, halb);

    /// <summary>Satz einsetzen: alte Netze frei, neuer Verkehr (oder der alte bei <paramref name="verkehrBleibt"/>), beide Netze hochladen.</summary>
    private void SatzEinsetzen(GlInterface gl, SatzPaket paket, double jetzt, bool verkehrBleibt = false)
    {
        foreach (var alt in _autoNetze.Values.Concat(_neuNetze.Values))
        {
            alt.Freigeben(gl);
        }
        _autoNetze.Clear();
        // Patch A 0.29.1: ein laufender Uebergang endet (der Satz ist sofort ganz da)
        _neuNetze.Clear();
        _uebergang = null;
        _neuGestalten = null;
        _uebergangBau = null;
        if (!verkehrBleibt)
        {
            _satz = paket.Satz;
            _gestalten = paket.Gestalten;
            _verkehr = paket.Verkehr;
            _verkehr.Bedient(jetzt);
        }
        if (paket.Halb == _gw.AutosHalb)
        {
            _autoNetze[false] = GlNetz.LadenMitNummern(gl, paket.Nah);
            _autoNetze[true] = GlNetz.LadenMitNummern(gl, paket.Fern);
        }
        else
        {
            // Welle Grafik G1: im Hintergrund mit der vorigen Stufe gebaut, Netze mit der jetzigen neu
            AutoNetzeNeu(gl);
        }
    }

    /// <summary>
    /// Patch A 0.29.1 (Weisung P10-3, Nico "Nach und nach, unsichtbar"): je Bild pruefen, ob sich draussen der Satz mit
    /// der Uhr aendert (nur nach einer Aenderung des Spiels oder der Szene); dann den neuen Satz im Hintergrund bauen
    /// und, sobald er fertig ist, den Uebergang beginnen (<see cref="FestUebergang"/>). Im Haus endet ein Uebergang
    /// sofort (dort sieht niemand die Strassen).
    /// </summary>
    private void UebergangTakt(GlInterface gl, StadtSpiel spiel, GrafikBibliothek grafik, double jetzt)
    {
        if (spiel.ImInnenraum)
        {
            if (_uebergang is { } innenU)
            {
                innenU.Abschliessen();
                UebergangAbschliessen(gl);
            }
            return;
        }
        if (_uebergangBau is { } bau && bau.Aufgabe.IsCompleted)
        {
            _uebergangBau = null;
            if (bau.Aufgabe.IsCompletedSuccessfully && ReferenceEquals(bau.Szene, spiel.Szene) && _verkehr is { } alt)
            {
                var paket = bau.Aufgabe.Result;
                if (alt.Rundweg)
                {
                    // Welle K3b: Rundwege haben kein Ende am Kartenrand, an dem die Fahrzeuge tauschen koennten; der
                    // Satz wechselt sofort
                    SatzEinsetzen(gl, paket, jetzt);
                }
                else if (paket.Halb != _gw.AutosHalb)
                {
                    // Welle Grafik G1: mit der vorigen Stufe gebaut, sofort einsetzen (Netze mit der jetzigen Stufe)
                    SatzEinsetzen(gl, paket, jetzt);
                }
                else
                {
                    _neuNetze[false] = GlNetz.LadenMitNummern(gl, paket.Nah);
                    _neuNetze[true] = GlNetz.LadenMitNummern(gl, paket.Fern);
                    _neuGestalten = paket.Gestalten;
                    _uebergang = new FestUebergang(_satz, alt, paket.Satz, paket.Verkehr);
                    _verkehr = _uebergang.Verkehr;
                }
            }
            _satzPruefen = true;
        }
        if (!_satzPruefen)
        {
            return;
        }
        _satzPruefen = false;
        var ziel = FestVerkehr.Fuer(spiel.Szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, spiel.Szene.Jahreszeit);
        if (ziel == (_uebergangBau?.Satz ?? _uebergang?.Neu ?? _satz))
        {
            return;
        }
        if (_uebergang is { } laufend)
        {
            // die Uhr hat sich waehrend eines Uebergangs noch einmal geaendert: den laufenden sofort fertig machen
            laufend.Abschliessen();
            UebergangAbschliessen(gl);
            if (ziel == _satz)
            {
                return;
            }
        }
        if (_satz != FestVerkehr.Satz.Autos && ziel != FestVerkehr.Satz.Autos)
        {
            // von einem Fest zu einem anderen gibt es in einer Stadt nicht (eine Szene hat ein Fest); sicherheitshalber sofort
            SatzSetzen(gl, spiel, ziel, jetzt);
            return;
        }
        var szene = spiel.Szene;
        var halbU = _gw.AutosHalb;
        _uebergangBau = new SatzVorrat(szene, ziel, Task.Run(() => SatzPaketBauen(szene, grafik, ziel, halbU)));
    }

    /// <summary>Patch A 0.29.1: Uebergang fertig, der neue Satz ist ganz da (Netze, Gestalten, Verkehr).</summary>
    private void UebergangAbschliessen(GlInterface gl)
    {
        if (_uebergang is not { } u)
        {
            return;
        }
        foreach (var alt in _autoNetze.Values)
        {
            alt.Freigeben(gl);
        }
        _autoNetze.Clear();
        foreach (var (fern, netz) in _neuNetze)
        {
            _autoNetze[fern] = netz;
        }
        _neuNetze.Clear();
        _satz = u.Neu;
        _gestalten = _neuGestalten;
        _verkehr = u.NeuVerkehr;
        _neuGestalten = null;
        _uebergang = null;
    }

    /// <summary>
    /// Autos oder Festgestalten eines Satzes in einem Zeichenaufruf; <paramref name="vorlage"/> = Verkehr des Satzes (gleiche
    /// Reihenfolge wie die Netze), <paramref name="zeigen"/> = nur diese Fahrzeuge zeigen (Uebergang, sonst alle).
    /// </summary>
    private void SatzZeichnen(GlInterface gl, Dictionary<bool, GlNetz> netze, List<FestVerkehr.Gestalt>? gestalten, Verkehr vorlage,
        Func<Verkehr.Auto, bool>? zeigen, bool fern, GrafikBibliothek grafik, Matrix4x4 sichtProj)
    {
        if (!netze.TryGetValue(fern, out var netz))
        {
            netz = GlNetz.LadenMitNummern(gl, AutoNetze(vorlage, gestalten, grafik, fern, _gw.AutosHalb));
            netze[fern] = netz;
        }
        if (gestalten is not null)
        {
            // W11: Festgestalten im selben Zeichenaufruf wie die Autos (nah alle Schrittbilder, das gezeigte je Gestalt)
            ZeichnenAutos(gl, netz, FestVerkehr.Lagen(FestVerkehr.Gezeigt(gestalten, _gw.AutosHalb), vorlage, fern, _autoLagen, zeigen), sichtProj);
            return;
        }
        // Welle Grafik G1: bei „halb“ nur jedes zweite Auto (gleiche Auswahl wie die Netze)
        var autos = FestVerkehr.Gezeigt(vorlage, _gw.AutosHalb);
        // Welle A 0.61.0: nach den Fahrzeugen die wartenden Figuren der Haltestellen (gleiche Reihenfolge wie die Netze)
        var anzahl = Math.Min(autos.Count + vorlage.Wartende.Count, AutosHoechstens);
        for (var i = autos.Count; i < anzahl; i++)
        {
            var w = vorlage.Wartende[i - autos.Count];
            if (!vorlage.Sichtbar(w) || (zeigen is not null && _uebergang is not null) || grafik.Modell(FestVerkehr.WartendModell) is not { } fm)
            {
                _autoLagen[i] = Matrix4x4.CreateScale(0) * Matrix4x4.CreateTranslation(0, -1000, 0);
                continue;
            }
            var fa = new Vector3((fm.Min.X + fm.Max.X) / 2, fm.Min.Y, (fm.Min.Z + fm.Max.Z) / 2);
            _autoLagen[i] = StadtSzene.Lage(fm, fa, new Vector3(FestVerkehr.Massstab(fm)), new Vector3(w.Ort.X, StadtSzene.Hoehe.Gehweg + 0.012f, w.Ort.Y), w.Blick);
        }
        for (var i = 0; i < Math.Min(autos.Count, anzahl); i++)
        {
            var auto = autos[i];
            if (zeigen is not null && !zeigen(auto))
            {
                _autoLagen[i] = Matrix4x4.CreateScale(0) * Matrix4x4.CreateTranslation(0, -1000, 0);
            }
            else if (grafik.Modell(Fahrzeugmodelle.Zeichnen(auto.Modell)) is { } am)
            {
                var (ort, richtung) = auto.Lage(auto.Weg);
                if (_spiel is { ImBus: true } mitfahrt && auto.Nummer == Buslinie.ErsteNummer)
                {
                    _busMitFigur = true;
                    // Welle A 0.67.0 (Sammelliste 302, Nico 5a): der erste Bus der Linie faehrt die Figur im Zeitraffer
                    // (kein eigener Zeichenaufruf); danach steht er wieder auf seinem Umlauf
                    var winkel = mitfahrt.FigurRichtung * MathF.PI / 180f;
                    (ort, richtung) = (new Vector2(mitfahrt.FigurX, mitfahrt.FigurZ), new Vector2(MathF.Sin(winkel), MathF.Cos(winkel)));
                }
                var anker = new Vector3((am.Min.X + am.Max.X) / 2, am.Min.Y, (am.Min.Z + am.Max.Z) / 2);
                _autoLagen[i] = StadtSzene.Lage(am, anker, new Vector3(StadtSzene.Meter * 1.1f), new Vector3(ort.X, StadtSzene.Hoehe.Strasse, ort.Y), richtung);
            }
        }
        ZeichnenAutos(gl, netz, anzahl, sichtProj);
    }

    /// <summary>Patch A 0.29.1: fuer die Toene nur die Autos, die gerade fahren (im Uebergang ohne die Gestalten).</summary>
    private Verkehr? TonVerkehr()
    {
        if (_uebergang is not { } u)
        {
            return _verkehr;
        }
        var autos = u.Alt == FestVerkehr.Satz.Autos ? u.AltVerkehr : u.Neu == FestVerkehr.Satz.Autos ? u.NeuVerkehr : null;
        var v = new Verkehr();
        if (autos is not null)
        {
            v.Autos.AddRange(u.Verkehr.Autos.Where(autos.Autos.Contains));
        }
        return v;
    }

    private sealed record SatzVorrat(StadtSzene Szene, FestVerkehr.Satz Satz, Task<SatzPaket> Aufgabe);

    private volatile SatzVorrat? _satzVorrat;

    /// <summary>Den im Haus vorbereiteten Satz nehmen, wenn er zu Szene und Satz passt (sonst null).</summary>
    private SatzPaket? SatzVorbereitetNehmen(StadtSzene szene, FestVerkehr.Satz satz)
    {
        if (_satzVorrat is not { } v || !ReferenceEquals(v.Szene, szene) || v.Satz != satz)
        {
            return null;
        }
        _satzVorrat = null;
        try
        {
            return v.Aufgabe.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Weisung P8-55 (W12, Teilplan 3.5): im Haus den Satz fuer das Hinausgehen im Hintergrund vorbereiten (Festgestalten
    /// oder Autos mit ihren Netzen), wenn er sich gegenueber dem jetzigen aendert (Daemmerung, neues Fest). Fuer die Szene,
    /// die das Spiel beim Hinausgehen zeigen wird (<see cref="StadtSpiel.SzeneDraussen"/>). Die Seite ruft das im Takt.
    /// </summary>
    public void Vorausbauen()
    {
        if (_spiel is not { ImInnenraum: true } spiel || spiel.Grafik is not { } grafik || _laedtVor || _verkehr is null)
        {
            return;
        }
        var szene = spiel.SzeneDraussen;
        var satz = FestVerkehr.Fuer(szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, szene.Jahreszeit);
        if (satz == _satz || (_satzVorrat is { } v && ReferenceEquals(v.Szene, szene) && v.Satz == satz))
        {
            return;
        }
        var halbV = _gw.AutosHalb;
        _satzVorrat = new SatzVorrat(szene, satz, Task.Run(() => SatzPaketBauen(szene, grafik, satz, halbV)));
    }

    /// <summary>
    /// Welle A 0.60.0 (Weisung P12-166): alle Lampen der Ampeln in einem Aufruf; eine Gruppe, die zur Fahrzeit
    /// <paramref name="zeit"/> nicht leuchtet, bekommt die Lage Null (ihre Dreiecke fallen auf einen Punkt). Zaehlt zum
    /// festen Teil des Bilds (<c>StadtbauGrenzen.Draussen</c>).
    /// </summary>
    private void AmpelLichterZeichnen(GlInterface gl, GlNetz netz, double zeit, Matrix4x4 sichtProj)
    {
        for (var g = 0; g < Ampeln.Gruppen; g++)
        {
            _ampelLagen[g] = Ampeln.GruppeAn(g, zeit) ? Matrix4x4.Identity : default;
        }
        ZeichnenAutos(gl, netz, Ampeln.Gruppen, sichtProj, _ampelLagen);
        _festAufrufe++;
        _festDreiecke += netz.Anzahl / 3;
    }

    private unsafe void ZeichnenAutos(GlInterface gl, GlNetz netz, int anzahl, Matrix4x4 sichtProj, Matrix4x4[]? lagenFeld = null)
    {
        gl.UseProgram(_autoProgramm);
        gl.UniformMatrix4fv(_autoOrtSichtProj, 1, false, &sichtProj);
        fixed (Matrix4x4* lagen = lagenFeld ?? _autoLagen)
        {
            gl.UniformMatrix4fv(_autoOrtLagen, anzahl, false, lagen);
        }
        var licht = AktuellesLicht();
        gl.Uniform1f(_autoOrtHell, (IstDunkel() ? 0.72f : 1f) * licht.Hell);
        LichtSetzen(gl, _autoLichtOrte, licht, AktuelleKamera?.Auge ?? default);
        DunstSetzen(gl, _autoDunstOrte);
        gl.Uniform1f(_autoOrtDurch, 0f);
        netz.Binden(gl);
        gl.DrawElements(GL_TRIANGLES, netz.Anzahl, GlUnsignedInt, IntPtr.Zero);
        _aufrufe++;
        _dreiecke += netz.Anzahl / 3;
        gl.UseProgram(_programm);
    }

    /// <summary>
    /// Welle D1 (Weisung P12-143, Teile A und B): das Netz eines Hauses fuer dieses Bild: null, wenn die Kugel um das Haus
    /// ganz ausserhalb des Sichtkegels liegt (<see cref="FestUebergang.ImSichtkegel"/>, kein neuer Zeichenaufruf, nur
    /// weniger); <paramref name="fern"/> = die Fernform (falls hochgeladen), sonst das Original.
    /// </summary>
    private GlNetz? HausNetzImBild(StadtHaus haus, bool fern, Matrix4x4 sichtProj)
    {
        if (!OhneSparenFuerTests && _hausKugel.TryGetValue(haus, out var kugel)
            && (kugel.Halb < 0 || !FestUebergang.ImSichtkegel(sichtProj, kugel.Mitte, kugel.Halb)))
        {
            return null;
        }
        return fern && _haeuserFern.TryGetValue(haus, out var hausFern) ? hausFern : _haeuser.GetValueOrDefault(haus);
    }

    /// <summary>
    /// Welle 327 (Weisung P12-244, Weg a): ein in Zellen geteiltes Netz der Stadt (Paket-Netz): nur die Zellen im
    /// Sichtkegel, aufeinanderfolgende in einem Aufruf (<see cref="Netz.Laeufe(IReadOnlyList{Netz.Zelle}, int, Matrix4x4, bool, List{ValueTuple{int, int}})"/>,
    /// dieselbe Rechnung wie die Zaehlung ohne Grafikkarte); <see cref="OhneSparenFuerTests"/> = alle Zellen.
    /// </summary>
    private unsafe void ZeichnenFestImBild(GlInterface gl, GlNetz netz, Matrix4x4 sichtProj)
    {
        Netz.Laeufe(netz.Zellen, netz.Anzahl, sichtProj, OhneSparenFuerTests, _laeufe);
        if (_laeufe.Count == 0)
        {
            return;
        }
        var modell = Matrix4x4.Identity;
        var mvp = modell * sichtProj;
        gl.UniformMatrix4fv(_ortMvp, 1, false, &mvp);
        gl.UniformMatrix4fv(_ortModell, 1, false, &modell);
        netz.Binden(gl);
        foreach (var (start, anzahl) in _laeufe)
        {
            gl.DrawElements(GL_TRIANGLES, anzahl, GlUnsignedInt, new IntPtr((long)start * sizeof(uint)));
            _aufrufe++;
            _dreiecke += anzahl / 3;
            _festAufrufe++;
            _festDreiecke += anzahl / 3;
        }
    }

    /// <summary>Welle 327: Laeufe des letzten Bilds (ein Feld fuer alle Bilder, nur im Zeichnen-Faden).</summary>
    private readonly List<(int Start, int Anzahl)> _laeufe = [];

    /// <summary>Welle D2: ein Netz der Stadt (Teil der Zaehlung ohne Grafikkarte, <see cref="LetzteStadtDreiecke"/>).</summary>
    private void ZeichnenFest(GlInterface gl, GlNetz netz, Matrix4x4 sichtProj)
    {
        Zeichnen(gl, netz, Matrix4x4.Identity, sichtProj);
        _festAufrufe++;
        _festDreiecke += netz.Anzahl / 3;
    }

    private unsafe void Zeichnen(GlInterface gl, GlNetz netz, Matrix4x4 modell, Matrix4x4 sichtProj)
    {
        var mvp = modell * sichtProj;
        gl.UniformMatrix4fv(_ortMvp, 1, false, &mvp);
        gl.UniformMatrix4fv(_ortModell, 1, false, &modell);
        netz.Binden(gl);
        gl.DrawElements(GL_TRIANGLES, netz.Anzahl, GlUnsignedInt, IntPtr.Zero);
        _aufrufe++;
        _dreiecke += netz.Anzahl / 3;
    }
}

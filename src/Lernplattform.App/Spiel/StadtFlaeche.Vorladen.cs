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
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtFlaeche"/> mit Vorladen, Hochladen,
/// Anwaermen und den Messungen je Bild. Reiner Umzug aus StadtFlaeche.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtFlaeche
{
    // ---- Weisung P8-55 (W12, Teilplan Abschnitt 3): Vorladen beim Oeffnen der Spielseite ----------------

    /// <summary>So lange darf das Hochladen je Bild dauern (die Ladeanzeige laeuft weiter); mindestens ein Schritt je Bild.</summary>
    public const double HochladenJeBildMs = 10;

    private volatile bool _glFrisch;
    private volatile bool _laedtVor;
    private volatile Task<Vorladepaket>? _vorladeAufgabe;
    private Queue<Action<GlInterface>>? _hochladen;
    private int _hochladenGesamt;
    private int _hochladenErledigt;
    private long _vorladenBeginn;

    /// <summary>
    /// Phase 12 Welle 2 (Weisung P12-5 Abschnitt 2): angekommene Pakete als Kartonstapel vor der Haustuer der Wohnung
    /// (<see cref="Lieferpaket"/>), ein Zeichenaufruf fuer den ganzen Stapel; ohne Paket nichts.
    /// </summary>
    private void LieferpaketZeichnen(GlInterface gl, Matrix4x4 sichtProj)
    {
        var zahl = _spiel!.PaketeVorDerTuer;
        LieferpaketZahl = zahl;
        if (zahl <= 0 || Lieferpaket.Lage(_spiel.Szene, _spiel.Stadt) is not { } lage)
        {
            return;
        }
        if (_lieferpaket is null || _lieferpaketZahl != zahl)
        {
            _lieferpaket?.Freigeben(gl);
            _lieferpaket = GlNetz.Laden(gl, Lieferpaket.Bauen(_spiel.Grafik, zahl));
            _lieferpaketZahl = zahl;
        }
        Zeichnen(gl, _lieferpaket, Lieferpaket.Welt(lage.Mitte, lage.Aussen), sichtProj);
    }

    /// <summary>Phase 12 Welle 2: Zahl der zuletzt gezeichneten Pakete vor der Haustuer (Start-Test, Fotos).</summary>
    public int LieferpaketZahl { get; private set; }

    /// <summary>Alles, was das Vorladen auf dem Prozessor baut (im Hintergrund).</summary>
    private sealed record Vorladepaket(StadtSzene Szene, StadtSzene.Fertig? Netze, SatzPaket? Satz, bool VerkehrBleibt,
        IReadOnlyList<string> Figuren, IReadOnlyList<Innenraum> Innen, PyObjekt? Aussehen, Nachlader? Lader = null);

    /// <summary>
    /// Weisung P8-55 (W12): wird gerade vorgeladen (Ladeanzeige)? Waehrenddessen zeichnet die Flaeche nur den Himmel
    /// und laedt in kleinen Stuecken je Bild hoch; <see cref="NaechstesBildAsync"/> wartet bis danach.
    /// </summary>
    public bool LaedtVor => _laedtVor;

    /// <summary>Anteil des Hochladens fuer den Fortschrittsring (0 bis 1).</summary>
    public double VorladenAnteil => Ladeanzeige.Anteil(_hochladenErledigt, _hochladenGesamt);

    /// <summary>Sekunden seit Beginn des Vorladens (Ladeanzeige erst ab 0,3 s).</summary>
    public double VorladenSekunden => _laedtVor && _vorladenBeginn != 0 ? Stopwatch.GetElapsedTime(_vorladenBeginn).TotalSeconds : 0;

    /// <summary>Dauer des letzten Vorladens bis fertig in ms (Start-Test, nur Info), null = noch keins.</summary>
    public double? VorladenMs { get; private set; }

    /// <summary>Wie oft vorgeladen wurde (Start-Test: Android nach dem Hintergrund, Seite neu geoeffnet).</summary>
    public int VorladenZahl { get; private set; }

    /// <summary>Beginn und Ende des Vorladens (aus dem Faden der Grafik; die Seite reicht es an die Oberflaeche weiter).</summary>
    public event Action? VorladenGeaendert;

    /// <summary>
    /// Vorladen beginnen (Oberflaechen-Thread): auf dem Prozessor im Hintergrund die Netze der Szene, die Autos bzw.
    /// Festgestalten nah und fern, alle Schrittbilder der Spielfigur, den Kreis und alle Innenraeume mit ihren Personen;
    /// danach laedt <see cref="VorladenBild"/> alles hoch und zeichnet jedes Netz einmal unsichtbar (die Shader werden im
    /// Treiber uebersetzt). Schon gebaute Netze (nach dem Hintergrund unter Android) werden nur neu hochgeladen.
    /// </summary>
    private void VorladenStarten()
    {
        if (!_glFrisch || _spiel is not { } spiel || !GlBereit)
        {
            if (_vorladeAufgabe is null)
            {
                _laedtVor = false;
            }
            return;
        }
        _glFrisch = false;
        var szene = spiel.Szene;
        var grafik = spiel.Grafik;
        var satz = grafik is null ? FestVerkehr.Satz.Autos : FestVerkehr.Fuer(szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, szene.Jahreszeit);
        // gleicher Satz: die Autos behalten ihre Lage, nur die Netze werden neu hochgeladen
        // Welle K3b: nur, wenn der Verkehr zu den Strassen der Szene passt (im Werkzeug aendern sie sich)
        var verkehrAlt = _verkehr is not null && satz == _satz && _uebergang is null && _verkehr.Passt(szene) ? _verkehr : null;
        var gestaltenAlt = _gestalten;
        var aussehen = spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
        var kleidung = spiel.Kleidung;
        var innenFertig = spiel.AlleVorbereiten();
        var figurenCpu = _figurenCpu;
        var halb = _gw.AutosHalb;
        // Welle Nachladen A 0.82.0: mit fester Kamera nur Grundlage und die Kacheln um die Figur (nicht die ganze Stadt)
        var nachladen = NachladenGewuenscht();
        spiel.NurGrundlage = nachladen;
        var mitGruen = _gw.Gruen(SpielKamera.AbstandMax);
        var (figurX, figurZ) = nachladen ? NachladenMitte(spiel) : (spiel.FigurX, spiel.FigurZ);
        _vorladenBeginn = Stopwatch.GetTimestamp();
        _hochladenErledigt = 0;
        _hochladenGesamt = 0;
        _vorladeAufgabe = Task.Run(async () =>
        {
            Nachlader? lader = null;
            StadtSzene.Fertig? netze = null;
            if (nachladen)
            {
                lader = new Nachlader(szene, mitGruen);
                lader.Takt(figurX, figurZ);
                lader.StartBauen(lader.Noetig);
            }
            else
            {
                netze = szene.Vorbereiten();
            }
            SatzPaket? satzPaket = null;
            if (grafik is not null)
            {
                satzPaket = verkehrAlt is not null
                    ? new SatzPaket(szene, satz, gestaltenAlt, verkehrAlt, AutoNetze(verkehrAlt, gestaltenAlt, grafik, false, halb), AutoNetze(verkehrAlt, gestaltenAlt, grafik, true, halb), halb)
                    : SatzPaketBauen(szene, grafik, satz, halb);
            }
            var figuren = new List<string>();
            void Figur1(string schluessel, Func<Netz> bauen)
            {
                figurenCpu.GetOrAdd(schluessel, _ => bauen());
                figuren.Add(schluessel);
            }
            // die Spielfigur: stehend und alle Schrittbilder (Rennen in der Stadt, Gehen in den Innenraeumen)
            Figur1(FigurSchluessel("#spieler", aussehen, kleidung), () => kleidung is { Count: > 0 } ? Figur.Netz(grafik, aussehen, 1f, kleidung) : Figur.Netz(grafik, aussehen, 1f));
            foreach (var bewegung in new[] { Figur.Rennen, Figur.Gehen })
            {
                for (var bild = 0; bild < Figur.Bilder; bild++)
                {
                    var b = bild;
                    Figur1(FigurSchluessel("#spieler", aussehen, kleidung, bewegung, b), () => Figur.Netz(grafik, aussehen, 1f, kleidung, bewegung, b));
                }
            }
            await innenFertig.ConfigureAwait(false);
            var innen = spiel.Vorrat;
            foreach (var raum in innen)
            {
                foreach (var person in raum.Personen)
                {
                    Figur1(FigurSchluessel(person.Id, person.Aussehen, null), () => raum.PersonNetz(person.Id, person.Aussehen) ?? Figur.Netz(grafik, person.Aussehen, 1f));
                    if (raum.Sitz(person) is not null)
                    {
                        // Phase 9 W1: sitzende Personen ebenso vorab (Sitzbild)
                        Figur1(FigurSchluessel(person.Id, person.Aussehen, null, Figur.Sitzen, 0), () => Figur.Netz(grafik, person.Aussehen, 1f, null, Figur.Sitzen, 0));
                    }
                }
            }
            return new Vorladepaket(szene, netze, satzPaket, verkehrAlt is not null, figuren, innen, aussehen, lader);
        });
        _laedtVor = true;
        VorladenGeaendert?.Invoke();
        Anfordern();
    }

    /// <summary>Start-Test: das jetzige Spiel noch einmal vorladen wie beim Oeffnen der Seite (nur mit OpenGL).</summary>
    public void Vorladen()
    {
        _glFrisch = GlBereit;
        VorladenStarten();
    }

    /// <summary>
    /// Ein Bild waehrend des Vorladens: nur der Himmel; ist der Prozessor fertig, Hochladen in Stuecken
    /// (hoechstens <see cref="HochladenJeBildMs"/> je Bild), zuletzt unsichtbar zeichnen. Danach zeichnet die Flaeche normal.
    /// </summary>
    private void VorladenBild(GlInterface gl, double jetzt, double abstandMs, long bytesBeginn, bool ruhte, long startStempel)
    {
        var skalierung = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        gl.Viewport(0, 0, (int)Math.Max(1, Bounds.Width * skalierung), (int)Math.Max(1, Bounds.Height * skalierung));
        var himmel = Himmel();
        gl.ClearColor(himmel.R / 255f, himmel.G / 255f, himmel.B / 255f, 1f);
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        var fertig = false;
        if (_vorladeAufgabe is { IsCompleted: true } aufgabe)
        {
            if (!aufgabe.IsCompletedSuccessfully)
            {
                // Fehler beim Bauen: wie vor W12 weiter (die Flaeche baut beim Zeichnen)
                fertig = true;
            }
            else
            {
                _hochladen ??= HochladenPlanen(aufgabe.Result, jetzt);
                var beginn = Stopwatch.GetTimestamp();
                do
                {
                    _hochladen.Dequeue()(gl);
                    _hochladenErledigt++;
                }
                while (_hochladen.Count > 0 && MsSeit(beginn) < HochladenJeBildMs);
                fertig = _hochladen.Count == 0;
            }
        }
        gl.Disable(GL_DEPTH_TEST);
        BilderGesamt++;
        _bilder.Enqueue(jetzt);
        while (_bilder.Count > 0 && _bilder.Peek() < jetzt - 1)
        {
            _bilder.Dequeue();
        }
        var nachLaden = _uhr.Elapsed.TotalSeconds;
        BildGezeichnet?.Invoke();
        Messen(jetzt, abstandMs, nachLaden, nachLaden, 0, nachLaden, bytesBeginn, ruhte, startStempel);
        if (fertig)
        {
            _hochladen = null;
            _vorladeAufgabe = null;
            VorladenMs = Stopwatch.GetElapsedTime(_vorladenBeginn).TotalMilliseconds;
            VorladenZahl++;
            _laedtVor = false;
            VorladenGeaendert?.Invoke();
        }
        RequestNextFrameRendering();
    }

    /// <summary>Die Schritte des Hochladens: Stadt, Haeuser, Gruen, Paket, Autos/Gestalten, Figuren, Kreis, Innenraeume, Anwaermen.</summary>
    private Queue<Action<GlInterface>> HochladenPlanen(Vorladepaket paket, double jetzt)
    {
        var schritte = new Queue<Action<GlInterface>>();
        schritte.Enqueue(gl =>
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
            NachladenFrei(gl);
            if (paket.Netze is { } ganz)
            {
                _flaechen[ProbeWelt.Stadt] = GlNetz.Laden(gl, ganz.Stadt);
            }
            else if (paket.Lader is { } lader)
            {
                // Welle Nachladen: die Grundlage (Boden, Fernkaesten, Instancing, Ampel-Lichter)
                NachladenGrundlage(gl, lader);
            }
        });
        if (paket.Lader is { } laderK)
        {
            foreach (var k in laderK.Noetig)
            {
                schritte.Enqueue(gl => KachelHochladen(gl, laderK.JetztBauen(k)));
            }
        }
        else if (paket.Netze is { } netzeG)
        {
            if (netzeG.StadtFern is { } stadtFern)
            {
                schritte.Enqueue(gl => _flaechen[StadtFern] = GlNetz.Laden(gl, stadtFern));
            }
            foreach (var (haus, netz) in netzeG.Haeuser)
            {
                var kugel = netz.Kugel();
                var hausFern = netzeG.HaeuserFern?.GetValueOrDefault(haus);
                schritte.Enqueue(gl =>
                {
                    _haeuser[haus] = GlNetz.Laden(gl, netz);
                    _hausKugel[haus] = kugel;
                    if (hausFern is not null)
                    {
                        _haeuserFern[haus] = GlNetz.Laden(gl, hausFern);
                    }
                });
            }
            if (netzeG.Gruen is { } gruen)
            {
                schritte.Enqueue(gl => _gruen = GlNetz.Laden(gl, gruen));
            }
            schritte.Enqueue(gl => InstanzenLaden(gl, netzeG.Instanzen));
            if (netzeG.PaketNah is { } nah && netzeG.PaketFern is { } fern)
            {
                schritte.Enqueue(gl => _paket[false] = GlNetz.Laden(gl, nah));
                schritte.Enqueue(gl => _paket[true] = GlNetz.Laden(gl, fern));
            }
            var ampelLichter = netzeG.AmpelLichter;
            schritte.Enqueue(gl =>
            {
                _ampelLichter?.Freigeben(gl);
                _ampelLichter = ampelLichter is not null ? GlNetz.LadenMitNummern(gl, ampelLichter) : null;
            });
        }
        schritte.Enqueue(gl =>
        {
            // eine Szene, die sich waehrend des Vorladens geaendert hat, laedt das naechste Bild wie bisher
            _szeneNeu = !ReferenceEquals(_spiel?.Szene, paket.Szene);
            if (paket.Satz is { } satz)
            {
                SatzEinsetzen(gl, satz, jetzt, paket.VerkehrBleibt);
            }
        });
        foreach (var schluessel in paket.Figuren)
        {
            schritte.Enqueue(gl =>
            {
                if (!_figuren.ContainsKey(schluessel) && _figurenCpu.TryGetValue(schluessel, out var netz))
                {
                    _figuren[schluessel] = GlNetz.Laden(gl, netz);
                }
            });
        }
        schritte.Enqueue(gl => Ring(gl, paket.Aussehen));
        foreach (var raum in paket.Innen)
        {
            schritte.Enqueue(gl => InnenHochladen(gl, raum));
        }
        schritte.Enqueue(gl =>
        {
            _innenNeu = true;
            Anwaermen(gl);
        });
        _hochladenGesamt = schritte.Count;
        return schritte;
    }

    /// <summary>
    /// Jedes hochgeladene Netz einmal zeichnen, in einem Bildpunkt und danach geloescht (unsichtbar): der Treiber
    /// uebersetzt die Shader und legt die Puffer jetzt an, nicht beim ersten echten Bild (Handy-Treiber).
    /// </summary>
    private void Anwaermen(GlInterface gl)
    {
        gl.Viewport(0, 0, 1, 1);
        gl.Enable(GL_DEPTH_TEST);
        gl.DepthFunc(GlLequal);
        gl.UseProgram(_programm);
        gl.Uniform1f(_ortHell, 1f);
        LichtSetzen(gl, _lichtOrte, Tageslicht.Tag, default);
        var netze = _flaechen.Values.Concat(_haeuser.Values).Concat(_haeuserFern.Values).Concat(_paket.Values).Concat(_figuren.Values)
            .Concat(_innenGl.Values.SelectMany(e => e.Waende.Values.Prepend(e.Netz))).Append(_gruen).Append(_marke).Append(_ausruf).Append(_ring)
            .Concat(_instanzen.SelectMany(i => new[] { i.Nah, i.Fern }));
        var durch = 0f;
        foreach (var netz in netze)
        {
            if (netz is not null)
            {
                // halb durchsichtig (Schachbrett) und normal, beide Wege des Pixel-Shaders
                gl.Uniform1f(_ortDurch, durch);
                durch = 1f - durch;
                Zeichnen(gl, netz, Matrix4x4.Identity, Matrix4x4.Identity);
            }
        }
        gl.Uniform1f(_ortDurch, 0f);
        foreach (var netz in _autoNetze.Values)
        {
            ZeichnenAutos(gl, netz, AutosHoechstens, Matrix4x4.Identity);
        }
        if (gl.IsBindVertexArrayAvailable)
        {
            gl.BindVertexArray(0);
        }
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
    }

    /// <summary>Innenraum in die Grafikkarte (oder schon dort): Netz ohne Waende und jede Wand fuer sich (halb durchsichtig).</summary>
    private (GlNetz Netz, Dictionary<InnenWand, GlNetz> Waende) InnenHochladen(GlInterface gl, Innenraum innen)
    {
        if (!_innenGl.TryGetValue(innen, out var geladen))
        {
            // Nachtrag 1 zu P7-13 (Wunsch 3): Waende einzeln, jede kann halb durchsichtig werden
            geladen = (GlNetz.Laden(gl, innen.NetzOhneWaende), innen.Waende.ToDictionary(w => w, w => GlNetz.Laden(gl, innen.WandNetzFertig(w))));
            _innenGl[innen] = geladen;
        }
        return geladen;
    }

    /// <summary>Innenraeume freigeben, die weder gezeigt werden noch im Vorrat des Spiels sind.</summary>
    private void InnenAufraeumen(GlInterface gl, StadtSpiel spiel)
    {
        var behalten = new HashSet<Innenraum>(spiel.Vorrat, ReferenceEqualityComparer.Instance);
        if (spiel.Innen is { } jetzt)
        {
            behalten.Add(jetzt);
        }
        foreach (var alt in _innenGl.Keys.Where(k => !behalten.Contains(k)).ToList())
        {
            var (netz, waende) = _innenGl[alt];
            netz.Freigeben(gl);
            foreach (var wand in waende.Values)
            {
                wand.Freigeben(gl);
            }
            _innenGl.Remove(alt);
        }
    }

    /// <summary>
    /// Weisung P7-17: Ruhe vor diesem Bild in ms. Stand die Bildschleife, zaehlt die Zeit vom Ende des letzten
    /// Bildes bis zum ersten Anlass (Zeiger oder Anforderung) als Ruhe; ohne eigenen Anlass (Avalonia zeichnet
    /// von sich aus neu) die ganze Luecke.
    /// </summary>
    private double Ruhe(bool ruhte, long startStempel)
    {
        if (!ruhte)
        {
            return 0;
        }
        var anlass = Erster(_anforderungStempel, _zeigerStempel > _endeVorherStempel ? _zeigerStempel : 0);
        var bis = anlass > 0 ? Math.Min(anlass, startStempel) : startStempel;
        return Math.Max(0, Stopwatch.GetElapsedTime(_endeVorherStempel, bis).TotalMilliseconds);
    }

    private static long Erster(long a, long b) => a == 0 ? b : b == 0 ? a : Math.Min(a, b);

    /// <summary>
    /// Weisung P7-17: fuer ein langsames Bild die Zeitpunkte der Eingabe vor Bildbeginn (Finger auf/ab von
    /// Android, Zeiger-Ereignis von Avalonia, erste Anforderung), nur die seit dem Ende des letzten Bildes.
    /// </summary>
    private string? Eingabe(long startStempel)
    {
        var teile = new List<string>(3);
        void Dazu(string name, long stempel)
        {
            if (stempel > _endeVorherStempel && stempel <= startStempel)
            {
                teile.Add(name + " " + BildZeiten.Ms(Stopwatch.GetElapsedTime(stempel, startStempel).TotalMilliseconds));
            }
        }
        Dazu("Finger", Systemwerte.Finger);
        Dazu("Zeiger", _zeigerStempel);
        Dazu("Anforderung", _anforderungStempel);
        return teile.Count == 0 ? null : "vor Bildbeginn: " + string.Join(", ", teile) + " ms";
    }

    /// <summary>Teilzeiten des Bildes in <see cref="Zeiten"/> eintragen (Speicherbereinigung und Speicher seit dem letzten Bild).</summary>
    private void Messen(double start, double abstandMs, double nachLaden, double nachLogik, double verdecktSek, double nachZeichnen, long bytesBeginn,
        bool ruhte, long startStempel)
    {
        var ende = _uhr.Elapsed.TotalSeconds;
        Span<int> gc = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var teile = _spiel?.TeileMessen() ?? default;
        var bild = new BildZeiten.Bild(start, abstandMs, (nachLogik - nachLaden) * 1000, ((nachLaden - start) + verdecktSek) * 1000,
            (nachZeichnen - nachLogik - verdecktSek) * 1000, (ende - nachZeichnen) * 1000,
            gc[0] - _gcVorher[0], gc[1] - _gcVorher[1], gc[2] - _gcVorher[2], bytes - _bytesVorher, _spiel?.ImInnenraum ?? false,
            _endeVorher < 0 ? 0 : (start - _endeVorher) * 1000, _layouts, _layoutMs, _auftraegeMs + (_spiel?.AktionenMessen() ?? 0), bytes - bytesBeginn,
            teile.WegMs, teile.EreignisMs, RuheMs: _endeVorher < 0 ? 0 : Ruhe(ruhte, startStempel));
        if (BildZeiten.Dauer(bild) >= BildZeiten.LangsamAbMs)
        {
            // Weisung P7-16: Werte des Systems (Android) seit dem Ende des letzten Bildes, nur fuer langsame Bilder;
            // Weisung P7-17: dazu die Zeitpunkte der Eingabe
            bild = bild with { System = Systemwerte.Seit?.Invoke(_endeVorherStempel), Eingabe = Eingabe(startStempel) };
        }
        _anforderungStempel = 0;
        var aufnehmen = _bytesVorher >= 0;
        gc.CopyTo(_gcVorher);
        _bytesVorher = bytes;
        _layouts = 0;
        _layoutMs = 0;
        _auftraegeMs = 0;
        _endeVorher = ende;
        _endeVorherStempel = Stopwatch.GetTimestamp();
        Mitschnitt?.Add(bild);
        if (aufnehmen)
        {
            // die Messung selbst (alle 10 s eine Zeile) zaehlt als Auftrag vor dem naechsten Bild
            var beginn = Stopwatch.GetTimestamp();
            Zeiten.Aufnehmen(bild);
            AuftragGemessen(MsSeit(beginn));
        }
    }

    /// <summary>Wie viele Haeuser im letzten Bild halb durchsichtig waren (Start-Test, Fotos).</summary>
    public int VerdecktZahl { get; private set; }
}

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
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtFlaeche"/> mit dem Zeichnen eines Bildes
/// (<c>OnOpenGlRender</c>). Reiner Umzug aus StadtFlaeche.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtFlaeche
{
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        _busMitFigur = false;
        var jetzt = _uhr.Elapsed.TotalSeconds;
        var startStempel = Stopwatch.GetTimestamp();
        var ruhte = _ruht;
        _ruht = false;
        var bytesBeginn = GC.GetAllocatedBytesForCurrentThread();
        var abstandMs = _letztesBild < 0 ? 0 : (jetzt - _letztesBild) * 1000;
        // Patch A 1.20.1 (Weisung P12-449 Punkt 1): Zeitschritt auf ganze Takte des Bildschirms (am PC unbekannt: wie bisher)
        var dt = _letztesBild < 0 ? 0 : Math.Min(BildtaktAlt.Value ? jetzt - _letztesBild : Bildtakt.Schritt(jetzt - _letztesBild, Systemwerte.Bildwiederholrate), 0.1);
        _aufrufe = 0;
        _dreiecke = 0;
        _festAufrufe = 0;
        _festDreiecke = 0;
        _letztesBild = jetzt;
        _zielFb = fb;
        GrafikWerteUebernehmen(gl);
        if (GlBereit)
        {
            GenauigkeitMelden(gl, fb);
        }
        if (_laedtVor && GlBereit)
        {
            VorladenBild(gl, jetzt, abstandMs, bytesBeginn, ruhte, startStempel);
            return;
        }
        if (_spiel is { ImInnenraum: false } spielModus && GlBereit && !_szeneNeu && NachladenGewuenscht() != (_lader is not null))
        {
            // Welle Nachladen: Kamerabereich geoeffnet (Messung) oder wieder fest: Szene in der anderen Art neu laden
            _szeneNeu = true;
        }
        if (_spiel is { } spiel && _szeneNeu && GlBereit)
        {
            // Stadt in die Grafikkarte laden: beim Start und wenn sich die sichtbaren Orte aendern;
            // jedes Haus fuer sich (es kann durchsichtig werden)
            foreach (var alt in _flaechen.Values.Concat(_haeuser.Values).Concat(_haeuserFern.Values))
            {
                alt.Freigeben(gl);
            }
            _flaechen.Clear();
            _haeuser.Clear();
            _haeuserFern.Clear();
            _hausKugel.Clear();
            if (NachladenGewuenscht())
            {
                // Welle Nachladen A 0.82.0: nur Grundlage und die noetigen Kacheln, der Rest kommt beim Gehen
                spiel.NurGrundlage = true;
                NachladenSzeneLaden(gl, spiel);
            }
            else
            {
            spiel.NurGrundlage = false;
            NachladenFrei(gl);
            // Weisung P8-55 (W12): die Netze baut die Szene einmal (bei einem Fest- oder Jahreszeitwechsel schon im
            // Haus im Hintergrund), hier wird nur noch hochgeladen
            var fertig = spiel.Szene.Vorbereiten();
            _flaechen[ProbeWelt.Stadt] = GlNetz.Laden(gl, fertig.Stadt);
            if (fertig.StadtFern is { } stadtFern)
            {
                _flaechen[StadtFern] = GlNetz.Laden(gl, stadtFern);
            }
            foreach (var haus in spiel.Szene.Haeuser)
            {
                _haeuser[haus] = GlNetz.Laden(gl, fertig.Haeuser[haus]);
                _hausKugel[haus] = fertig.Haeuser[haus].Kugel();
                if (fertig.HaeuserFern?.GetValueOrDefault(haus) is { } hausFern)
                {
                    _haeuserFern[haus] = GlNetz.Laden(gl, hausFern);
                }
            }
            _gruen?.Freigeben(gl);
            _gruen = fertig.Gruen is { } gn ? GlNetz.Laden(gl, gn) : null;
            InstanzenLaden(gl, fertig.Instanzen);
            foreach (var alt in _paket.Values)
            {
                alt.Freigeben(gl);
            }
            _paket.Clear();
            if (fertig.PaketNah is { } nah && fertig.PaketFern is { } fernNetz)
            {
                // W11 (Nico 5a): Laternen, Tannen und Kuerbisse, nah die Paket-Modelle, fern einfache Kaesten
                _paket[false] = GlNetz.Laden(gl, nah);
                _paket[true] = GlNetz.Laden(gl, fernNetz);
            }
            _ampelLichter?.Freigeben(gl);
            _ampelLichter = fertig.AmpelLichter is { } lichter ? GlNetz.LadenMitNummern(gl, lichter) : null;
            }
            // die Autos behalten ihre Lage, wenn die Szene neu geladen wird (neue Orte sichtbar); eine Szene mit
            // anderer Festzeit (neuer Tag) oder ein neues Spiel bekommt sofort seinen eigenen Satz; aendert sich draussen
            // in derselben Stadt nur die Uhr, wechselt der Satz nach und nach (Patch A 0.29.1)
            if (spiel.Grafik is not null
                && FestVerkehr.Fuer(spiel.Szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, spiel.Szene.Jahreszeit) is var satzNeu
                && (_verkehr is null || !_verkehr.Passt(spiel.Szene)
                    || (satzNeu != (_uebergang?.Neu ?? _satz) && (spiel.ImInnenraum || _spielNeu || _szeneFest != spiel.Szene.Fest))))
            {
                SatzSetzen(gl, spiel, satzNeu, jetzt);
            }
            _spielNeu = false;
            _szeneFest = spiel.Szene.Fest;
            _szeneNeu = false;
        }
        if (_spiel is { } s2 && _innenNeu && GlBereit)
        {
            // Weisung P8-55 (W12): schon hochgeladene Innenraeume (Vorrat) werden genommen, nicht mehr gebrauchte freigegeben
            if (s2.Innen is { } innen)
            {
                var geladen = InnenHochladen(gl, innen);
                _innen = geladen.Netz;
                _innenWaende = geladen.Waende;
            }
            else
            {
                _innen = null;
                _innenWaende = [];
            }
            InnenAufraeumen(gl, s2);
            _innenGeladen = s2.Innen;
            _innenNeu = false;
        }
        if (UhrSanft && Tageslicht.FesteUhr != _satzUhr)
        {
            _satzPruefen = true;
        }
        if (_spiel is { } s3 && GlBereit && _verkehr is not null && s3.Grafik is not null
            && ((_satzInnen && !s3.ImInnenraum) || (Tageslicht.FesteUhr != _satzUhr && !UhrSanft)))
        {
            // W11 (Teilplan 1): beim Betreten der Stadt (oder fester Uhr im Start-Test) sofort der richtige Satz
            var neu = FestVerkehr.Fuer(s3.Szene.Fest, Tageslicht.FesteUhr ?? s3.Jetzt.Uhr, s3.Szene.Jahreszeit);
            if (neu != _satz || _uebergang is not null)
            {
                SatzSetzen(gl, s3, neu, jetzt);
            }
            _satzPruefen = false;
        }
        else if (_spiel is { } s4 && GlBereit && _verkehr is not null && s4.Grafik is { } grafikU && !_laedtVor)
        {
            UebergangTakt(gl, s4, grafikU, jetzt);
        }
        _satzInnen = _spiel?.ImInnenraum ?? true;
        _satzUhr = Tageslicht.FesteUhr;
        var nachLaden = _uhr.Elapsed.TotalSeconds;
        // Welle A 0.73.0: mit dem Bus unterwegs (selbst gefahren) folgt die Kamera dem Bus wie der gehenden Figur
        // Welle A 0.95.0 (Sammelliste 385): gehaltene Gehtasten zuerst (sie beenden einen angetippten Weg)
        var freiGeht = FreiTakt(dt);
        var geht = (_spiel?.Fortschreiten(dt) ?? false) | (_spiel?.MitfahrtAktiv ?? false) | freiGeht;
        // Welle A 0.93.0 (Weisung P12-303): der Sprung laeuft je Bild weiter (nur Darstellung)
        var springt = _spiel?.HaltungTakt((float)dt) ?? false;
        VorBild?.Invoke();
        var nachLogik = _uhr.Elapsed.TotalSeconds;
        var verdecktSek = 0.0;
        var kamera = AktuelleKamera;
        if (geht)
        {
            _nachlaufBis = jetzt + 1.5;
            _verkehr?.Bedient(jetzt);
        }
        if ((geht || jetzt < _nachlaufBis) && kamera is not null && jetzt >= _folgenAb)
        {
            // Kamera folgt der Figur weich (nach der Ankunft noch kurz, bis sie mittig steht), solange
            // niemand selbst verschiebt
            var k = kamera.Folgen(_spiel!.FigurX, _spiel.FigurZ, dt);
            // Welle A 0.66.0 (Gelaende): die Hoehe folgt genauso weich (kein Ruckeln am Hang); ohne Gelaende bleibt sie 0
            if (!_spiel.ImInnenraum && _spiel.Szene.Gelaende is not null)
            {
                kamera.ZielY += (KameraZielHoehe(_spiel) - kamera.ZielY) * k;
            }
        }
        var ichBewegt = false;
        if (_spiel is { } ichSpiel && kamera is not null && FesteKamera)
        {
            // Welle A 0.92.0 (Weisung P12-301): Ich-Sicht weiterfuehren (Auge, Uebergang, Gehrichtung, Pause beim Sitzen/Bus)
            ichBewegt = _ich.Takt(kamera, ichSpiel, (float)dt, _busMitFigur) && (kamera.IchAnteil != kamera.IchZiel || geht || springt);
            if (kamera.Ich != _ichWarAn)
            {
                _ichWarAn = kamera.Ich;
                _ich.RichtungNeu(kamera);
                IchAnsage?.Invoke(kamera.Ich, (kamera.Ich ? SpielProbeTexte.IchSichtAn + " " + IchSicht.Ansage(kamera, ichSpiel) : SpielProbeTexte.IchSichtAus));
            }
            else if (_ich.RichtungNeu(kamera))
            {
                IchAnsage?.Invoke(true, IchSicht.Ansage(kamera, ichSpiel));
            }
        }
        if (_spiel is { } tonSpiel && kamera is not null && TonDienst.Aktiv)
        {
            // Phase 8 W8: Schleifen der Stadt nach der Bildmitte (Fluss, Autos nach Naehe, Voegel)
            TonDienst.Takt(tonSpiel.ImInnenraum, tonSpiel.ImInnenraum ? null
                : Tonplan.Stadt(tonSpiel.Szene, kamera.ZielX, kamera.ZielZ, TonVerkehr(), _verkehr?.Laeuft(jetzt, VerkehrNurNachBedienen) == true && (_gestalten is null || _uebergang is not null)));
        }
        var skalierung = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var breite = (int)Math.Max(1, Bounds.Width * skalierung);
        var hoehe = (int)Math.Max(1, Bounds.Height * skalierung);
        // Welle Grafik G1: bei Aufloesung unter 100 % in den eigenen Bildspeicher (Mittel: wie vorher)
        var (zeichenBreite, zeichenHoehe) = GrafikBildBeginnen(gl, breite, hoehe);
        gl.Viewport(0, 0, zeichenBreite, zeichenHoehe);
        var himmel = Himmel();
        var licht = AktuellesLicht();
        var tagAnteil = (licht.Hell - 0.42f) / 0.58f;
        var hv = System.Numerics.Vector3.Lerp(licht.Himmel, new System.Numerics.Vector3(himmel.R / 255f, himmel.G / 255f, himmel.B / 255f), Math.Clamp(tagAnteil, 0f, 1f));
        gl.ClearColor(hv.X, hv.Y, hv.Z, 1f);
        DunstFestlegen(_spiel is { ImInnenraum: false }, hv);
        gl.ClearDepth(1f);
        gl.Enable(GL_DEPTH_TEST);
        gl.DepthFunc(GlLequal);
        gl.DepthMask(1);
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        ZeitabfrageBeginnen();
        // Welle D1 (Teil C): ab dem Abstand der Fernformen (wie Autos und Laternen) die Stadt mit vereinfachter Fest-Deko
        // Welle Grafik G1: Abstand der Fernformen und des Kleingruens aus der Grafikstufe (Mittel = 25 und 18 wie vorher)
        var fernBild = kamera is not null && _gw.Fern(kamera.Abstand) && !OhneSparenFuerTests;
        var nachladen = _lader is not null && _spiel is { ImInnenraum: false };
        var flaeche = _spiel is null ? null : _spiel.ImInnenraum ? _innen
            : nachladen ? _boden
            : (fernBild ? _flaechen.GetValueOrDefault(StadtFern) : null) ?? _flaechen.GetValueOrDefault(ProbeWelt.Stadt);
        if (GlBereit && _spiel is not null && kamera is not null && flaeche is not null)
        {
            gl.UseProgram(_programm);
            gl.Uniform1f(_ortHell, (IstDunkel() ? 0.72f : 1f) * licht.Hell);
            LichtSetzen(gl, _lichtOrte, licht, kamera.Auge);
            DunstSetzen(gl, _dunstOrte);
            gl.Uniform1f(_ortDurch, 0f);
            // Welle A 1.1.0 (Weisung P12-328): Blickfeld der Ich-Sicht aus dem Regler in der Karte „Steuerung“
            kamera.IchBlickfeld = Lernplattform.Core.Spiel.Blickfeldbedienung.Grad;
            var sichtProj = kamera.Sicht * kamera.Projektion(breite, hoehe);
            LetzteSichtProj = sichtProj;
            LetzterAbstand = kamera.Abstand;
            if (nachladen)
            {
                // Welle Nachladen: freigeben, hochladen, noetige Kacheln im Bild sofort (vor dem Zeichnen)
                NachladenTakt(gl, _spiel, sichtProj);
                gl.UseProgram(_programm);
            }
            // Welle A 0.92.0: grosse Karte in der Ich-Sicht: nah voll, weiter Fernformen, hinter der Sichtweite (Dunst) nichts
            _ichFern = kamera.IchAnteil > 0f && nachladen && !IchSichtweiteAusFuerTests ? new Vector2(kamera.IchAuge.X, kamera.IchAuge.Z) : null;
            _ichAnteil = kamera.IchAnteil;
            // Weisung P8-52 (nur Leistungsprobe der Probe-APK): die Stadt mehrfach an derselben Stelle (sonst einmal)
            for (var mal = 0; mal < (_spiel.ImInnenraum ? 1 : ProbeMehrfach); mal++)
            {
            if (nachladen)
            {
                // Welle Nachladen: Boden, Kacheln nah (Stadt, Paket, Kleingruen in einem Netz je Kachel), sonst Fernkaesten
                NachladenZeichnen(gl, sichtProj);
            }
            else
            {
            ZeichnenFest(gl, flaeche, sichtProj);
            }
            if (!nachladen && !_spiel.ImInnenraum && _gruen is not null && _gw.Gruen(kamera.Abstand))
            {
                ZeichnenFest(gl, _gruen, sichtProj);
            }
            if (!nachladen && !_spiel.ImInnenraum && _paket.TryGetValue(_gw.Fern(kamera.Abstand), out var paket))
            {
                // Welle 327 (Weisung P12-244, Weg a): nur die Zellen im Bild, aufeinanderfolgende in einem Aufruf
                ZeichnenFestImBild(gl, paket, sichtProj);
            }
            if (!_spiel.ImInnenraum && _ampelLichter is { } ampelLichter)
            {
                // Welle A 0.60.0: Lampen der Ampeln nach dem Takt des Verkehrs (ohne Verkehr: Zeit 0)
                AmpelLichterZeichnen(gl, ampelLichter, _verkehr?.Zeit ?? 0, sichtProj);
            }
            if (!_spiel.ImInnenraum)
            {
                // Welle D2 (Weisung P12-157): oft gleiche Modelle, je Satz ein Aufruf, nur Stuecke im Bild
                InstanzenZeichnen(gl, sichtProj, fernBild, kamera.Abstand, licht, kamera.Auge);
            }
            if (mal > 0)
            {
                foreach (var haus in _haeuser.Keys)
                {
                    if (HausNetzImBild(haus, fernBild, sichtProj) is { } netz)
                    {
                        ZeichnenFest(gl, netz, sichtProj);
                    }
                }
            }
            }
            if (!_spiel.ImInnenraum && _verkehr is not null && _spiel.Grafik is { } grafikA)
            {
                var schritt = AutoSchritt > 0 ? AutoSchritt : _verkehr.Laeuft(jetzt, VerkehrNurNachBedienen) ? dt : 0;
                if (_uebergang is { } u)
                {
                    // Patch A 0.29.1: tauschen nur ausserhalb des Sichtkegels, am Rundlauf oder nach der Hoechstzeit
                    u.Schritt(schritt, _spiel.FigurX, _spiel.FigurZ, (p, halb) => FestUebergang.ImSichtkegel(sichtProj, p, halb));
                    if (u.Fertig)
                    {
                        UebergangAbschliessen(gl);
                    }
                }
                else if (schritt > 0)
                {
                    // Welle A 0.73.0: sitzt die Figur im Bus, haelt kein Auto fuer sie (sie ist nicht auf der Fahrbahn)
                    var (fx, fz) = _spiel.SitztImBus ? (-1000f, -1000f) : (_spiel.FigurX, _spiel.FigurZ);
                    _verkehr.Weiter(schritt, fx, fz);
                }
                // Welle A 0.73.0 (Weisung P12-239): Bus an der Haltestelle der Figur, Ein- und Aussteigen, im Bus mitfahren
                _spiel.MitfahrtTakt(_uebergang?.Verkehr ?? _verkehr, schritt);
                if (_spiel.SitztImBus)
                {
                    // die Figur sitzt im Bus (sie selbst wird nicht gezeichnet, der Bus faehrt sie)
                    _busMitFigur = true;
                }
                // ab AutoFern Abstand eine einfache Fernform (zwei Kaesten in der Hauptfarbe des Autos)
                var fern = _gw.Fern(kamera.Abstand);
                Func<Verkehr.Auto, bool>? zeigen = _uebergang is { } u2 ? u2.Aktiv : null;
                SatzZeichnen(gl, _autoNetze, _gestalten, _uebergang?.AltVerkehr ?? _verkehr, zeigen, fern, grafikA, sichtProj);
                if (_uebergang is { } u3)
                {
                    SatzZeichnen(gl, _neuNetze, _neuGestalten, u3.NeuVerkehr, zeigen, fern, grafikA, sichtProj);
                }
            }
            if (_spiel.Ziel is { } ziel && _marke is not null)
            {
                Zeichnen(gl, _marke, Matrix4x4.CreateTranslation((float)ziel.X, _spiel.ImInnenraum ? 0.03f
                    : MathF.Max(StadtSzene.Hoehe.Uebergang + 0.012f, _spiel.Szene.BodenHoehe((float)ziel.X, (float)ziel.Y) + 0.012f), (float)ziel.Y), sichtProj);
            }
            var innen = _spiel.Innen;
            var groesse = innen is null ? Figur.StadtHoehe : Innenraum.FigurHoehe;
            var aussehen = _spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
            // Sammelliste 125: Bild der Schrittbewegung nach dem zurueckgelegten Weg (nie nach der Uhr)
            var bild = ProbeBild >= 0 ? ProbeBild : _spiel.SchrittBild;
            var haltung = _spiel.Haltung;
            GlNetz spieler;
            Matrix4x4 figurLage;
            if (haltung is { } h)
            {
                // Welle A 0.81.0 (Sammelliste 349): sitzend (Haltung der Personen am Schreibtisch) oder liegend (stehende Figur
                // auf dem Ruecken, Kopf gegen den Blick, ohne Kopfbedeckung) auf dem Platz
                // Sammelliste 351 (Weisung P12-276): ohne Tisch die Haltung mit den Haenden auf den Knien, Lage aus StadtSpiel.HaltungLage
                spieler = h.Liegt
                    ? FigurNetz(gl, "#spieler-liegt", aussehen, OhneKopf(_spiel.Kleidung), null, -1)
                    : FigurNetz(gl, "#spieler", aussehen, _spiel.Kleidung, Figur.SitzBild(_spiel.HaltungArt ?? SitzArt.Sitzen, _spiel.MeinPlatz?.Modell), 0);
                figurLage = StadtSpiel.HaltungLage(h, groesse, innen is null ? FigurHoehe(_spiel) : 0f);
            }
            else
            {
                // Welle A 0.92.0 (Weisung P12-301 Punkt 3): in der Ich-Sicht ohne Kopf (das Auge sitzt darin)
                // Welle A 0.93.0 (Weisung P12-303): geduckt, kriechend oder im Sprung das Modell der Haltung (Tabelle Figur.HaltungBild),
                // im Sprung um die Sprunghoehe angehoben; die Figur steht logisch weiter an ihrer Stelle
                spieler = _spiel.HaltungModell is { } modell
                    ? FigurNetz(gl, "#spieler", aussehen, _spiel.Kleidung, modell, _spiel.HaltungBildNummer, IchNah(kamera))
                    : FigurNetz(gl, "#spieler", aussehen, _spiel.Kleidung, _spiel.Bewegung, bild, IchNah(kamera));
                // Welle A 0.99.0 (Sammelliste 400): geduckt oder kriechend gehend wiegt sich die Figur leicht im Schritt (die Haltung
                // hat kein eigenes Gehbild; Wiegen um die Laengsachse und kleines Heben nach dem Weg, nur Darstellung)
                // Welle A 1.7.0 (Sammelliste 447): robbend bewegt sich die Figur selbst, sie wiegt sich nicht
                var wiegen = _spiel.HaltungModell is { } hm && hm != Figur.Robben && !_spiel.Springt && (_spiel.Geht || _spiel.FreiUnterwegs)
                    ? Matrix4x4.CreateRotationZ(0.06f * MathF.Sin((float)(_spiel.Schrittweg / _spiel.Zyklus * 4 * Math.PI)))
                        * Matrix4x4.CreateTranslation(0, 0.012f * MathF.Abs(MathF.Sin((float)(_spiel.Schrittweg / _spiel.Zyklus * 4 * Math.PI))), 0)
                    : Matrix4x4.Identity;
                figurLage = Matrix4x4.CreateScale(groesse) * wiegen * Matrix4x4.CreateRotationY(IchSicht.FigurDrehung(kamera, _spiel) * MathF.PI / 180f)
                    * Matrix4x4.CreateTranslation(_spiel.FigurX, (innen is null ? FigurHoehe(_spiel) : 0f) + (_spiel.SprungHoehe * groesse), _spiel.FigurZ);
            }
            LetzteFigurLage = _busMitFigur ? null : figurLage;
            if (!_busMitFigur)
            {
                // Welle A 0.67.0 (Sammelliste 302): im Bus sitzt die Figur im Bus der Linie (siehe Autos), sie selbst nicht
                Zeichnen(gl, spieler, figurLage, sichtProj);
            }
            if (innen is not null)
            {
                foreach (var person in innen.Personen)
                {
                    if (innen.Sitz(person) is { } sitz)
                    {
                        // Phase 9 W1 (Weisung P9-3): auf dem Schreibtischstuhl sitzend, mit dem Gesicht zum Tisch
                        Zeichnen(gl, FigurNetz(gl, person.Id, person.Aussehen, null, Figur.Sitzen, 0), Matrix4x4.CreateScale(groesse)
                            * Matrix4x4.CreateRotationY(sitz.Blick) * Matrix4x4.CreateTranslation(sitz.X, 0, sitz.Z), sichtProj);
                        if (person.Auftrag && _ausruf is not null)
                        {
                            Zeichnen(gl, _ausruf, Matrix4x4.CreateTranslation(sitz.X, groesse + 0.25f, sitz.Z), sichtProj);
                        }
                        continue;
                    }
                    // Personen schauen zur Spielfigur
                    var blick = MathF.Atan2(_spiel.FigurX - person.X, _spiel.FigurZ - person.Z);
                    Zeichnen(gl, FigurNetz(gl, person.Id, person.Aussehen), Matrix4x4.CreateScale(groesse) * Matrix4x4.CreateRotationY(blick)
                        * Matrix4x4.CreateTranslation(person.X, 0, person.Z), sichtProj);
                    if (person.Auftrag && _ausruf is not null)
                    {
                        Zeichnen(gl, _ausruf, Matrix4x4.CreateTranslation(person.X, groesse + 0.25f, person.Z), sichtProj);
                    }
                }
                // Welle A 0.99.0: Glastueren der Serverschraenke (zu oder offen, Scheibe halb durchsichtig)
                GlastuerenZeichnen(gl, innen, sichtProj);
                // Waende vor der Kamera und zwischen Auge und Figur halb durchsichtig (Sammelliste 102); Welle A 0.92.0 (Nico 4a):
                // in der Ich-Sicht keine Wand durchsichtig, Waende 2,5 m hoch und eine Decke darueber
                var d0 = _uhr.Elapsed.TotalSeconds;
                var ichNah = IchNah(kamera);
                if (ichNah)
                {
                    _durchsichtig.Clear();
                }
                else
                {
                    innen.Durchsichtig(kamera.Auge, _spiel.FigurX, _spiel.FigurZ, groesse, _durchsichtig);
                }
                verdecktSek += _uhr.Elapsed.TotalSeconds - d0;
                LetzteIchDecke = ichNah;
                // Welle A 1.6.0 (Sammelliste 443): Blaetter der Raumtueren (zu oder offen)
                TuerBlaetterZeichnen(gl, innen, sichtProj, ichNah);
                if (ichNah)
                {
                    // Welle A 0.92.0: Waende 2,5 m und Decke in einem Netz (ein Aufruf; nichts ist halb durchsichtig)
                    if (_decke is not { } decke || !ReferenceEquals(decke.Raum, innen))
                    {
                        _decke?.Netz.Freigeben(gl);
                        decke = (innen, GlNetz.Laden(gl, innen.IchHuelle()));
                        _decke = decke;
                    }
                    gl.Uniform1f(_ortDurch, 0f);
                    Zeichnen(gl, decke.Netz, Matrix4x4.Identity, sichtProj);
                }
                else
                {
                    foreach (var (wand, netz) in _innenWaende)
                    {
                        gl.Uniform1f(_ortDurch, _durchsichtig.Contains(wand) ? 1f : 0f);
                        Zeichnen(gl, netz, Matrix4x4.Identity, sichtProj);
                    }
                }
                gl.Uniform1f(_ortDurch, 0f);
                VerdecktZahl = _durchsichtig.Count;
                if (Markierung(gl) is { } markierung)
                {
                    gl.Uniform1f(_ortDurch, 1f);
                    Zeichnen(gl, markierung, Matrix4x4.Identity, sichtProj);
                    gl.Uniform1f(_ortDurch, 0f);
                }
            }
            else
            {
                LetzteIchDecke = false;
                LieferpaketZeichnen(gl, sichtProj);
                // Haeuser; wer zwischen Auge und Figur steht, halb durchsichtig (Sammelliste 102)
                var v0 = _uhr.Elapsed.TotalSeconds;
                if (IchNah(kamera))
                {
                    // Welle A 0.92.0: in der Ich-Sicht steht nichts zwischen Auge und Figur
                    _verdeckt.Clear();
                }
                else
                {
                    _spiel.Szene.Verdeckt(kamera.Auge, _spiel.FigurX, _spiel.FigurZ, Figur.StadtHoehe, _verdeckt);
                }
                verdecktSek += _uhr.Elapsed.TotalSeconds - v0;
                foreach (var haus in _haeuser.Keys)
                {
                    // Welle D1 (Teile A und B): Haeuser ausserhalb des Bilds nicht zeichnen, fern die Fernform
                    if (HausNetzImBild(haus, fernBild, sichtProj) is not { } netz)
                    {
                        continue;
                    }
                    var durch = _verdeckt.Contains(haus);
                    gl.Uniform1f(_ortDurch, durch ? 1f : 0f);
                    ZeichnenFest(gl, netz, sichtProj);
                }
                gl.Uniform1f(_ortDurch, 0f);
                VerdecktZahl = _verdeckt.Count;
                // Nachtrag 1 zu P7-13 (Wunsch 2): Kreis um die Figur in ihrer Kreisfarbe, ohne Tiefenpruefung
                // (kein Haus, Baum oder Schild verdeckt ihn), danach die Figur noch einmal ganz vorn
                if (!_busMitFigur && !IchNah(kamera))
                {
                    gl.Disable(GL_DEPTH_TEST);
                    var ring = haltung is { } hr ? new Vector2(hr.Ort.X, hr.Ort.Z) : new Vector2(_spiel.FigurX, _spiel.FigurZ);
                    Zeichnen(gl, Ring(gl, aussehen), Matrix4x4.CreateTranslation(ring.X, FigurHoehe(_spiel) + 0.01f, ring.Y), sichtProj);
                    gl.Enable(GL_DEPTH_TEST);
                    gl.Clear(GL_DEPTH_BUFFER_BIT);
                    Zeichnen(gl, spieler, figurLage, sichtProj);
                }
            }
            // Weisung P8-52 (nur Leistungsprobe): weitere Zeichenaufrufe bis zur Zahl der Stufe, ein kleines Netz unter dem Boden
            while (_aufrufe < ProbeAufrufe && _marke is not null)
            {
                Zeichnen(gl, _marke, Matrix4x4.CreateTranslation(0f, -50f, 0f), sichtProj);
            }
            if (gl.IsBindVertexArrayAvailable)
            {
                gl.BindVertexArray(0);
            }
        }
        ZeitabfrageEnde();
        GrafikBildEnde(breite, hoehe);
        gl.Disable(GL_DEPTH_TEST);
        LetzteZeichenaufrufe = _aufrufe;
        LetzteDreiecke = _dreiecke;
        LetzteStadtAufrufe = _festAufrufe;
        LetzteStadtDreiecke = _festDreiecke;
        LetzteZeichenzeitMs = (_uhr.Elapsed.TotalSeconds - jetzt) * 1000;
        BilderGesamt++;
        _bilder.Enqueue(jetzt);
        while (_bilder.Count > 0 && _bilder.Peek() < jetzt - 1)
        {
            _bilder.Dequeue();
        }
        var nachZeichnen = _uhr.Elapsed.TotalSeconds;
        BildGezeichnet?.Invoke();
        Messen(jetzt, abstandMs, nachLaden, nachLogik, verdecktSek, nachZeichnen, bytesBeginn, ruhte, startStempel);
        if (_spiel is not null && GlBereit)
        {
            Belastung.Bild(jetzt, LetzteZeichenzeitMs);
        }
        if (geht || springt || ichBewegt || Dauerlauf || jetzt < _nachlaufBis || jetzt < _weiterBis || (_verkehr is not null && _spiel is { ImInnenraum: false } && _verkehr.Laeuft(jetzt, VerkehrNurNachBedienen)))
        {
            // Welle Grafik G1: mit Bildbremse (Niedrig) erst nach 1 / Bilder Sekunden, sonst sofort wie vorher
            NaechstesBildAnfordern(jetzt);
        }
        else
        {
            _ruht = true;
        }
    }
}

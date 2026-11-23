using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.92.0 „Ich-Sicht“ (Weisung P12-301, Kurzplan P12-181, Nico 1a 2b 3c 4a), ueber echte Ereignisse an der Spielflaeche:
/// ganz heranzoomen (Mausrad, zwei Finger, Taste +) fuehrt weich durch den Schulterblick in die Ich-Sicht, heraus derselbe Weg
/// zurueck; Taste I und der Menue-Eintrag „Ich-Sicht“ schalten an und aus, der Screenreader hoert an/aus und die Blickrichtung.
/// Umsehen mit rechter Maus, einem Finger, Q/E und R/F (nie Gehen); beim Gehen dreht der Blick in die Gehrichtung; in ein Haus
/// hinein und hinaus bleibt die Ich-Sicht. Grenzen (<see cref="IchGrenzenAsync"/>): in jeder Grafikstufe an jeder Kachel der
/// Stadt in acht Blickrichtungen und in jedem Innenraum in acht Blickrichtungen (Niedrig 125 000 / 32, Mittel 160 000 / 40,
/// Hoch 200 000 / 48, nie lockern). Gegenprobe: dieselben Stellen ohne Ich-Sicht zeichnen den Kopf der Figur (mehr Dreiecke der
/// Figur) bzw. die Innenkamera wie bisher.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Blickrichtungen der Grenzmessung (45 Grad, das Sichtfeld am PC ist breiter, es bleibt keine Luecke).</summary>
    private static readonly float[] IchRichtungen = [0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f];

    private static async Task<int> IchSichtAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Ich-Sicht " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Ich-Sicht " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Ich-Sicht: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        if (!await GlAbwartenAsync(seite))
        {
            Pruefe("ohne 3D kein Menue-Eintrag", !seite.IchKnopf.IsVisible, "Knopf sichtbar " + seite.IchKnopf.IsVisible);
            return funde;
        }
        try
        {
            await IchBedienungAsync(ansicht, seite, Pruefe);
        }
        finally
        {
            StadtFlaeche.IchTiefeImmerFuerTests = false;
            Dialog.AlleSchliessen();
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    private static async Task IchBedienungAsync(MainView ansicht, SpielSeite seite, Action<string, bool, string> pruefe)
    {
        var inv = CultureInfo.InvariantCulture;
        var spiel = Frisch(seite);
        var fl = seite.Flaeche;
        await fl.NaechstesBildAsync();
        var k = fl.AktuelleKamera!;
        // A 0.96.0: Mitte des freien Bereichs (zwischen Kopfzeile und Tafel), nie im Stick-Bereich unten (Gesten wie bisher)
        var mitte = MitteFrei(fl);
        async Task Bilder(int n)
        {
            for (var i = 0; i < n; i++)
            {
                await fl.NaechstesBildAsync();
            }
        }
        async Task<int> BisFertig()
        {
            var n = 0;
            for (; n < 200 && k.IchAnteil != k.IchZiel; n++)
            {
                await fl.NaechstesBildAsync();
            }
            await ansicht.BildFertigAsync();
            return n;
        }
        pruefe("Start ohne Ich-Sicht", k.IchErlaubt && k.IchAnteil == 0f && seite.IchKnopf.IsVisible
            && Avalonia.Automation.AutomationProperties.GetName(seite.IchKnopf) == SpielProbeTexte.IchSichtKnopf,
            string.Create(inv, $"erlaubt {k.IchErlaubt}, Anteil {k.IchAnteil:F2}, Knopf sichtbar {seite.IchKnopf.IsVisible}, Name \"{Avalonia.Automation.AutomationProperties.GetName(seite.IchKnopf)}\""));

        // 1 Mausrad: von 8 heran weich durch den Schulterblick in die Ich-Sicht (hoechstens 12 Rasten, Zwischenstufen sichtbar)
        var rasten = 0;
        var stufen = new SortedSet<float>();
        while (!SpielKamera.ImIch(k) && rasten < 30)
        {
            Rad(fl, mitte, 1);
            rasten++;
            await Bilder(2);
            if (k.IchAnteil is > 0f and < 1f)
            {
                stufen.Add(MathF.Round(k.IchAnteil, 3));
            }
        }
        var bilderHinein = await BisFertig();
        var ansageAn = Ansage.Zuletzt;
        pruefe("Mausrad hinein weich in die Ich-Sicht", k.Ich && rasten <= 12 && stufen.Count >= 3 && Math.Abs(k.Abstand - SpielKamera.AbstandMin) < 0.001f
            && ansageAn.StartsWith(SpielProbeTexte.IchSichtAn, StringComparison.Ordinal) && ansageAn.Contains("Blick nach ", StringComparison.Ordinal),
            string.Create(inv, $"{rasten} Rasten, {stufen.Count} Zwischenstufen ({string.Join(" ", stufen.Take(8).Select(s => s.ToString("F2", inv)))}), danach {bilderHinein} Bilder bis ganz, Abstand {k.Abstand:F2}, Ansage \"{ansageAn}\""));

        // eigene Figur: kein Kopf vor der Kamera, das Bild ist eigen tief, wo das Geraet weniger als 24 Bit hat (hier erzwungen)
        StadtFlaeche.IchTiefeImmerFuerTests = true;
        await Bilder(2);
        var eigenTief = fl.LetztesBildEigeneTiefe;
        var dreieckeEigen = fl.LetzteDreiecke;
        StadtFlaeche.IchTiefeImmerFuerTests = false;
        await Bilder(2);
        pruefe("eigener Tiefenspeicher (Handy mit 16 Bit)", eigenTief == fl.AufloesungDa && dreieckeEigen == fl.LetzteDreiecke && dreieckeEigen > 0,
            string.Create(inv, $"eigener Bildspeicher {eigenTief} (moeglich {fl.AufloesungDa}), {dreieckeEigen} und {fl.LetzteDreiecke} Dreiecke, Tiefe des Geraets {fl.TiefeBits} Bit"));

        // 1 Mausrad heraus: derselbe Weg zurueck (Schulterblick), dann bis 9, weiter nie
        Rad(fl, mitte, -1);
        var nachEinerRaste = k.IchZiel;
        for (var i = 0; i < 40; i++)
        {
            Rad(fl, mitte, -1);
        }
        await BisFertig();
        pruefe("Mausrad heraus derselbe Weg, weiter als 9 nie", Math.Abs(nachEinerRaste - SpielKamera.SchulterAnteil) < 0.001f && k.IchAnteil == 0f
            && Math.Abs(k.Abstand - SpielKamera.AbstandMax) < 0.001f && Ansage.Zuletzt == SpielProbeTexte.IchSichtAus,
            string.Create(inv, $"nach einer Raste Ziel {nachEinerRaste:F2} (Schulterblick {SpielKamera.SchulterAnteil:F2}), danach Abstand {k.Abstand:F2}, Ich {k.IchAnteil:F2}, Ansage \"{Ansage.Zuletzt}\""));

        // Menue-Eintrag „Ich-Sicht“ (Screenreader: Aufruf ueber den Automation-Peer), Name wechselt, Ansage an/aus
        var peer = ControlAutomationPeer.CreatePeerForElement(seite.IchKnopf);
        var aufruf = peer.GetProvider<IInvokeProvider>();
        aufruf?.Invoke();
        await BisFertig();
        var nameAn = Avalonia.Automation.AutomationProperties.GetName(seite.IchKnopf);
        var ansageKnopfAn = Ansage.Zuletzt;
        aufruf?.Invoke();
        await BisFertig();
        var nameAus = Avalonia.Automation.AutomationProperties.GetName(seite.IchKnopf);
        pruefe("Menue-Eintrag an und aus", aufruf is not null && nameAn == SpielProbeTexte.IchSichtBeenden && nameAus == SpielProbeTexte.IchSichtKnopf
            && ansageKnopfAn.StartsWith(SpielProbeTexte.IchSichtAn, StringComparison.Ordinal) && Ansage.Zuletzt == SpielProbeTexte.IchSichtAus && k.IchAnteil == 0f,
            $"Name an \"{nameAn}\", aus \"{nameAus}\", Ansage an \"{ansageKnopfAn}\", aus \"{Ansage.Zuletzt}\"");

        // Taste I (Standardbelegung), Ereignis an der Flaeche
        fl.Focus();
        TasteAn(fl, Key.I);
        await BisFertig();
        var tasteAn = k.Ich;
        TasteAn(fl, Key.I);
        await BisFertig();
        var tasteAus = k.IchAnteil == 0f;
        pruefe("Taste I an und aus", tasteAn && tasteAus, $"an {tasteAn}, aus {tasteAus}");

        // zwei Finger auseinander: wie das Mausrad hinein in die Ich-Sicht
        var a = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var b = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Point pa = new(mitte.X - 20, mitte.Y), pb = new(mitte.X + 20, mitte.Y);
        Druecken(fl, a, pa);
        Druecken(fl, b, pb);
        for (var i = 1; i <= 30 && !SpielKamera.ImIch(k); i++)
        {
            (pa, pb) = (new Point(mitte.X - 20 - (i * 12), mitte.Y), new Point(mitte.X + 20 + (i * 12), mitte.Y));
            Bewegen(fl, a, pa);
            Bewegen(fl, b, pb);
        }
        Loslassen(fl, a, pa);
        Loslassen(fl, b, pb);
        await BisFertig();
        pruefe("zwei Finger hinein", k.Ich && !spiel.Geht, string.Create(inv, $"Ich {k.IchAnteil:F2}, Abstand {k.Abstand:F2}"));

        // 2 Umsehen: rechte Maus (drehen und Blick heben/senken), ein Finger (drehen), Q/E, R/F; nie Gehen, Abstand bleibt
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var (d0, n0) = (k.Drehung, k.IchNeigung);
        Druecken(fl, maus, mitte, rechts: true);
        Bewegen(fl, maus, new Point(mitte.X + 100, mitte.Y + 60), rechts: true);
        Loslassen(fl, maus, new Point(mitte.X + 100, mitte.Y + 60), rechts: true);
        var mausDreh = ((k.Drehung - d0) % 360f + 360f) % 360f;
        var mausNeig = k.IchNeigung - n0;
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var d1 = k.Drehung;
        Druecken(fl, finger, mitte);
        Bewegen(fl, finger, new Point(mitte.X - 120, mitte.Y));
        Loslassen(fl, finger, new Point(mitte.X - 120, mitte.Y));
        var fingerDreh = Math.Abs(k.Drehung - d1) > 1f;
        var d2 = k.Drehung;
        TasteAn(fl, Key.E);
        var eDreht = Math.Abs(k.Drehung - d2) > 1f;
        for (var i = 0; i < 40; i++)
        {
            TasteAn(fl, Key.F);
        }
        var fGrenze = k.IchNeigung;
        for (var i = 0; i < 40; i++)
        {
            TasteAn(fl, Key.R);
        }
        var rGrenze = k.IchNeigung;
        await Bilder(2);
        pruefe("umsehen (rechte Maus, ein Finger, Q/E, R/F)",
            Math.Abs(mausDreh - (360f - (100 * Kameragriff.GradJePunkt))) < 0.01f /* A 1.6.0 (441): Maus nach rechts = Blick nach rechts */ && Math.Abs(mausNeig) > 1f && fingerDreh && eDreht
            && new[] { fGrenze, rGrenze }.Order().SequenceEqual(new[] { SpielKamera.IchNeigungMin, SpielKamera.IchNeigungMax })
            && k.Ich && !spiel.Geht && Math.Abs(k.Abstand - SpielKamera.AbstandMin) < 0.001f,
            string.Create(inv, $"rechte Maus dreht {mausDreh:F1}, neigt {mausNeig:F1}; ein Finger dreht {fingerDreh}; E dreht {eDreht}; F bis {fGrenze:F0}, R bis {rGrenze:F0}; Figur geht {spiel.Geht}"));
        k.IchNeigung = SpielKamera.IchNeigungStart;

        // Ansage bei neuer Blickrichtung (acht Richtungen)
        var vorAnsage = Ansage.Zuletzt;
        k.Drehung = (k.Drehung + 90f) % 360f;
        await Bilder(3);
        await ansicht.BildFertigAsync();
        pruefe("Ansage der Blickrichtung", Ansage.Zuletzt != vorAnsage && Ansage.Zuletzt.StartsWith(SpielProbeTexte.IchBlick(IchSicht.Richtung(k.Drehung)), StringComparison.Ordinal),
            $"vorher \"{vorAnsage}\", nachher \"{Ansage.Zuletzt}\"");

        // Gehen: Antippen eines Orts wie heute, der Blick dreht in die Gehrichtung, das Auge bleibt im Kopf der Figur
        var ziel = spiel.Szene.Orte.OrderByDescending(o => MathF.Abs(o.StehX - spiel.FigurX) + MathF.Abs(o.StehZ - spiel.FigurZ)).First().Id;
        spiel.GeheZu(ziel);
        // gewertet wird jedes Bild, vor dem die Gehrichtung schon eine Sekunde gleich war (nach einer Ecke dreht der Blick weich nach)
        var schlechteste = 0f;
        var gewertet = 0;
        var bilder = 0;
        var augeAb = 0f;
        var richtung = spiel.FigurRichtung;
        var gleichSeit = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 150 && spiel.Geht; i++)
        {
            await fl.NaechstesBildAsync();
            bilder++;
            augeAb = MathF.Max(augeAb, System.Numerics.Vector3.Distance(k.IchAuge, IchSicht.Auge(spiel, k.Drehung)));
            if (MathF.Abs(spiel.FigurRichtung - richtung) > 0.5f)
            {
                richtung = spiel.FigurRichtung;
                gleichSeit.Restart();
            }
            else if (gleichSeit.Elapsed.TotalSeconds >= 1)
            {
                gewertet++;
                schlechteste = MathF.Max(schlechteste, MathF.Abs(((IchSicht.DrehungZu(spiel.FigurRichtung) - k.Drehung + 540f) % 360f) - 180f));
            }
        }
        pruefe("Gehen in der Ich-Sicht", gewertet > 0 && k.Ich && augeAb < 0.05f && schlechteste < 5f,
            string.Create(inv, $"{bilder} Bilder zu {ziel}, davon {gewertet} eine Sekunde nach der letzten Richtungsaenderung: groesste Abweichung des Blicks von der Gehrichtung {schlechteste:F1} Grad; Auge hoechstens {augeAb:F3} vom Kopf der Figur"));
        spiel.SofortAnkommen();
        await Bilder(2);

        // Haus hinein und hinaus: die Ich-Sicht geht mit; drinnen Decke und Waende 2,5 m (Darstellung)
        if (ZurBitweiche(spiel) && spiel.Betreten())
        {
            await Bilder(3);
            var innen = fl.AktuelleKamera!;
            var drinIch = innen != k && innen.Ich && StadtFlaeche.IchNah(innen);
            spiel.Verlassen();
            await Bilder(3);
            var k2 = fl.AktuelleKamera!;
            pruefe("in ein Haus hinein und hinaus", drinIch && ReferenceEquals(k2, k) && k.Ich,
                string.Create(inv, $"drinnen Ich {innen.IchAnteil:F2}, draussen danach Ich {k.IchAnteil:F2}"));
        }
        else
        {
            pruefe("in ein Haus hinein und hinaus", false, "Bitweiche nicht betreten");
        }
        TasteAn(fl, Key.I);
        await BisFertig();
    }

    /// <summary>
    /// Grenzen in der Ich-Sicht (Weisung P12-301 Punkt 5) in der gerade gesetzten Grafikstufe: draussen an jeder Kachel der Stadt
    /// (Mitte der Kachel, acht Blickrichtungen, Blick waagrecht), drinnen in jedem Innenraum der Stadt (Mitte, acht Richtungen).
    /// Gegenprobe: dasselbe teuerste Bild draussen ohne Ich-Sicht zeichnet die Figur mit Kopf (mehr Dreiecke der Figur).
    /// </summary>
    private static async Task<int> IchGrenzenAsync(SpielSeite seite, Grafikstufe stufe)
    {
        var inv = CultureInfo.InvariantCulture;
        var (grenzeD, grenzeA) = Grafikstufen.Grenze(stufe);
        var name = "Spiel: Ich-Sicht Grenzen " + GrafikTexte.Name(stufe);
        var fl = seite.Flaeche;
        var vorher = seite.Spiel!;
        var funde = 0;
        try
        {
            var spiel = Frisch(seite);
            await fl.NaechstesBildAsync();
            var kacheln = Stadtteile.Alle(spiel.Szene.Breite, spiel.Szene.Tiefe).ToList();
            var (draussenD, draussenA, wo) = await IchKachelnAsync(fl, spiel, kacheln);
            var k = fl.AktuelleKamera!;
            // Gegenprobe: am teuersten Bild ohne Ich-Sicht (Kamera an derselben Stelle) ist die Figur mit Kopf
            var aussehen = spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as Lernplattform.Core.Daten.PyObjekt : null;
            var figurOhneKopf = Figur.OhneKopf(spiel.Grafik, aussehen, null, null, -1, IchSicht.KoerperBis).Indizes.Count / 3;
            var figurGanz = Figur.Netz(spiel.Grafik, aussehen, 1f, null, null, -1).Indizes.Count / 3;
            Messwerte.Notiere(string.Create(inv,
                $"{name} draussen: {kacheln.Count} Kacheln x {IchRichtungen.Length} Richtungen, teuerstes Bild {wo}: {draussenD} Dreiecke, hoechstens {draussenA} Zeichenaufrufe (Grenzen {grenzeD}, {grenzeA}); eigene Figur ohne Kopf {figurOhneKopf} statt {figurGanz} Dreiecke"));
            if (draussenD > grenzeD || draussenA > grenzeA || draussenD <= 0 || figurOhneKopf >= figurGanz)
            {
                Messwerte.Notiere(string.Create(inv, $"  FEHLER: {name} draussen: {draussenD} Dreiecke, {draussenA} Aufrufe (Grenzen {grenzeD}, {grenzeA}) bei {wo}; Figur {figurOhneKopf}/{figurGanz}"));
                funde++;
            }
            k.IchZiel = 0f;
            k.IchAnteil = 0f;

            // drinnen: jeder Innenraum der Stadt in der Ich-Sicht (Decke, Waende 2,5 m), acht Richtungen
            (long D, int A, string Wo) innenTeuerst = (-1, 0, "");
            var innenAufrufe = 0;
            var raeume = 0;
            var ohneDecke = new List<string>();
            foreach (var ort in spiel.Szene.Orte.Where(o => Innenraum.OrtFuer(o.Ansicht) is not null))
            {
                var innen = Innenraum.Bauen(ort.Ansicht, spiel.Ablauf.Stand, spiel.Innenraeume, spiel.Grafik, spiel.GeschaefteDaten, null, spiel.Feste, spiel.Szene.Jahreszeit);
                if (innen is null)
                {
                    continue;
                }
                spiel.Ansehen(innen, innen.Breite / 2, innen.Tiefe / 2, 0f);
                await fl.NaechstesBildAsync();
                var ki = fl.AktuelleKamera!;
                await fl.NaechstesBildAsync();
                var normal = (fl.LetzteDreiecke, fl.LetzteZeichenaufrufe);
                ki.IchZiel = 1f;
                ki.IchAnteil = 1f;
                ki.IchNeigung = SpielKamera.IchNeigungStart;
                raeume++;
                foreach (var dreh in IchRichtungen)
                {
                    ki.Drehung = dreh;
                    await fl.NaechstesBildAsync();
                    var (d, a) = (fl.LetzteDreiecke, fl.LetzteZeichenaufrufe);
                    innenAufrufe = Math.Max(innenAufrufe, a);
                    if (d > innenTeuerst.D)
                    {
                        innenTeuerst = (d, a, string.Create(inv, $"{ort.Id} Drehung {dreh:F0}"));
                    }
                }
                // Decke nur in der Ich-Sicht: Blick nach oben zeichnet mehr als die Innenkamera von heute nicht zeigt
                ki.IchNeigung = SpielKamera.IchNeigungMin;
                await fl.NaechstesBildAsync();
                if (!fl.LetzteIchDecke)
                {
                    ohneDecke.Add(ort.Id);
                }
                ki.IchZiel = 0f;
                ki.IchAnteil = 0f;
                await fl.NaechstesBildAsync();
                if (fl.LetzteIchDecke)
                {
                    ohneDecke.Add(ort.Id + " (Decke auch ohne Ich-Sicht)");
                }
                Messwerte.Notiere(string.Create(inv, $"{name} drinnen {ort.Id}: Innenkamera {normal.Item1}/{normal.Item2}, Ich-Sicht teuerstes bisher {innenTeuerst.D}/{innenAufrufe}"));
            }
            Messwerte.Notiere(string.Create(inv,
                $"{name} drinnen: {raeume} Innenraeume x {IchRichtungen.Length} Richtungen, teuerstes Bild {innenTeuerst.Wo}: {innenTeuerst.D} Dreiecke, hoechstens {innenAufrufe} Zeichenaufrufe (Grenzen {grenzeD}, {grenzeA}); Decke fehlt oder falsch: {(ohneDecke.Count == 0 ? "nirgends" : string.Join(", ", ohneDecke))}"));
            if (innenTeuerst.D > grenzeD || innenAufrufe > grenzeA || innenTeuerst.D <= 0 || raeume == 0 || ohneDecke.Count > 0)
            {
                Messwerte.Notiere(string.Create(inv, $"  FEHLER: {name} drinnen: {innenTeuerst.D} Dreiecke, {innenAufrufe} Aufrufe (Grenzen {grenzeD}, {grenzeA}) bei {innenTeuerst.Wo}, {raeume} Raeume, Decke {ohneDecke.Count}"));
                funde++;
            }
        }
        finally
        {
            seite.SpielSetzen(vorher);
            await fl.NaechstesBildAsync();
        }
        return funde;
    }

    /// <summary>Ich-Sicht an jeder Kachel (Mitte, acht Richtungen, Blick waagrecht): teuerstes Bild und hoechste Zahl der Aufrufe.</summary>
    private static async Task<(long Dreiecke, int Aufrufe, string Wo)> IchKachelnAsync(StadtFlaeche fl, StadtSpiel spiel, IReadOnlyList<Stadtteile.Kachel> kacheln)
    {
        var inv = CultureInfo.InvariantCulture;
        (long D, int A, string Wo) teuerst = (-1, 0, "");
        var aufrufe = 0;
        foreach (var kachel in kacheln)
        {
            var m = kachel.Mitte;
            spiel.HinstellenFuerTests(m.X, m.Y);
            var k = fl.AktuelleKamera!;
            k.IchZiel = 1f;
            k.IchAnteil = 1f;
            k.IchNeigung = SpielKamera.IchNeigungStart;
            k.Setzen(m.X, m.Y);
            await fl.NaechstesBildAsync();
            foreach (var dreh in IchRichtungen)
            {
                k.Drehung = dreh;
                await fl.NaechstesBildAsync();
                var (d, a) = (fl.LetzteDreiecke, fl.LetzteZeichenaufrufe);
                aufrufe = Math.Max(aufrufe, a);
                if (d > teuerst.D)
                {
                    teuerst = (d, a, string.Create(inv, $"Kachel {kachel.Name} Drehung {dreh:F0}"));
                }
            }
        }
        return (teuerst.D, aufrufe, teuerst.Wo);
    }
}

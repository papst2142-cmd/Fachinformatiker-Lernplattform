using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.96.0 (Sammelliste 385 Welle 2, Weisung P12-309): die Sticks am Handy und Tablet mit gedachten Fingern an der echten
/// Spielseite, in den Groessen Handy hoch und quer, Tablet hoch und quer (so gross, wie der Bildschirm erlaubt): ein kurzer Tipp
/// unten geht wie heute (kein Stick), ziehen zeigt den Stick und die Figur geht (nie auf einer Sperre, loslassen: Stick weg,
/// genau ein Halt), beide Sticks zugleich (gehen und drehen), Ring nie ueber Kopfzeile oder Leiste, oben kein Stick, Ich-Sicht
/// (rechter Stick sieht um), Spiel-Handy offen: keine Sticks. Gegenproben: Tipp ohne Ziehen zeigt keinen Stick; zwei Finger
/// ohne Stick-Daumen zoomen wie heute.
/// </summary>
public static partial class AutoMessung
{
    private static readonly (string Name, double Breite, double Hoehe)[] StickGroessen =
        [("Handy hoch", SchmalBreite, SchmalHoehe), ("Handy quer", SchmalHoehe, SchmalBreite), ("Tablet hoch", 800, 1280), ("Tablet quer", 1280, 800)];

    private static async Task FreiSticksAsync(MainView ansicht, SpielSeite seite, Action<string, bool, string> pruefe)
    {
        if (TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("Spiel: Sticks: kein Fenster (Handy-Groessen nur am PC nachstellbar)");
            return;
        }
        var inv = CultureInfo.InvariantCulture;
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
        var finger = 100L;
        try
        {
            foreach (var (name, breite, hoehe) in StickGroessen)
            {
                fenster.Width = Math.Min(breite, hoechstB);
                fenster.Height = Math.Min(hoehe, hoechstH);
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
                var spiel = Frisch(seite);
                var fl = seite.Flaeche;
                await fl.NaechstesBildAsync();
                await fl.NaechstesBildAsync();
                var k = fl.AktuelleKamera!;
                var sz = spiel.Szene;
                bool Gesperrt() => !spiel.ImInnenraum && (!sz.Laufraster.KannStehen(spiel.FigurX, spiel.FigurZ) || sz.Laufraster.FeinGesperrt(spiel.FigurX, spiel.FigurZ));
                var b = fl.StickBereich;
                var groesse = string.Create(inv, $"{name} {fl.Bounds.Width:F0}x{fl.Bounds.Height:F0}");
                if (b.FreiHoehe < Sticks.Radius || !fl.SticksMoeglich)
                {
                    pruefe("Sticks " + groesse, false, string.Create(inv, $"frei {b.FreiHoehe:F0} Punkte hoch, moeglich {fl.SticksMoeglich}"));
                    continue;
                }
                // die Punkte der Daumen je Schritt neu aus dem freien Bereich (am Handy hoch waechst die Tafel nach dem ersten Gehen,
                // der freie Bereich endet dann hoeher; ein alter Punkt laege unter der Tafel, wo es nie einen Stick gibt)
                Point links = default, rechts = default;
                void Punkte()
                {
                    b = fl.StickBereich;
                    var yUnten = b.Hoehe - b.Unten - (b.FreiHoehe / 4);
                    links = new Point(b.Breite / 4, yUnten);
                    rechts = new Point(b.Breite * 3 / 4, yUnten);
                }
                Punkte();

                // a kurzer Tipp unten (auf ein fernes Haus, das unten im Bild steht): kein Stick, die Figur geht wie beim Antippen
                var tippPunkt = (Point?)null;
                var (vx, vz) = Steuerung.Richtung(1, 0, k.Drehung);
                foreach (var ort in sz.Orte.OrderByDescending(o => MathF.Abs(o.StehX - spiel.FigurX) + MathF.Abs(o.StehZ - spiel.FigurZ)))
                {
                    foreach (var d in new[] { 1.5, 2.5, 3.5, 4.5 })
                    {
                        k.Setzen((float)(ort.StehX + (vx * d)), (float)(ort.StehZ + (vz * d)));
                        if (k.Bildpunkt(new Vector3(ort.StehX, StadtSzene.Hoehe.Gehweg, ort.StehZ), (float)b.Breite, (float)b.Hoehe) is { } bp
                            && Sticks.SeiteFuer(bp.X, bp.Y, b) is not null)
                        {
                            tippPunkt = new Point(bp.X, bp.Y);
                            break;
                        }
                    }
                    if (tippPunkt is not null)
                    {
                        break;
                    }
                }
                await fl.NaechstesBildAsync();
                var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
                var id = ++finger;
                var tp = tippPunkt ?? links;
                fl.FingerAuf(id, tp);
                fl.FingerBewegt(id, tp + new Point(Sticks.AnfangAb / 2, 0));
                var stickBeimTipp = fl.StickZustand.Aktiv;
                fl.FingerAb(id, tp + new Point(Sticks.AnfangAb / 2, 0));
                var tippGeht = spiel.Geht || spiel.Ablauf.Stand.Verlauf.Count != zeilen;
                spiel.SofortAnkommen();
                // Gegenprobe: ein Stueck weiter gezogen ist es kein Tipp (der Stick erscheint, die Figur geht nicht dorthin)
                zeilen = spiel.Ablauf.Stand.Verlauf.Count;
                fl.KameraAufFigur();
                await fl.NaechstesBildAsync();
                id = ++finger;
                fl.FingerAuf(id, tp);
                fl.FingerBewegt(id, tp + new Point(Sticks.AnfangAb * 2, 0));
                var stickGezogen = fl.StickZustand.Aktiv;
                fl.FingerBewegt(id, tp);
                fl.FingerAb(id, tp);
                var gezogenGeht = spiel.Geht || spiel.Ablauf.Stand.Verlauf.Count != zeilen;
                spiel.FreiHalt();
                await fl.NaechstesBildAsync();
                pruefe("Tipp unten bleibt Antippen " + groesse, !stickBeimTipp && tippGeht && stickGezogen && !gezogenGeht
                    && seite.StickRing(Sticks.Seite.Links) is null && seite.StickRing(Sticks.Seite.Rechts) is null,
                    string.Create(inv, $"Tipp bei {tp.X:F0},{tp.Y:F0} ({(tippPunkt is null ? "Boden" : "Haus")}): Stick {stickBeimTipp}, Figur geht {tippGeht}; gezogen (Gegenprobe): Stick {stickGezogen}, Figur geht {gezogenGeht}; danach Ring links {seite.StickRing(Sticks.Seite.Links) is not null}, rechts {seite.StickRing(Sticks.Seite.Rechts) is not null}"));

                Punkte();
                // b ziehen: Stick erscheint, die Figur geht nie auf eine Sperre; loslassen: Stick weg, genau ein Halt (frisches Spiel,
                // die Figur steht wieder am Start und nicht am Haus des Tipps)
                spiel = Frisch(seite);
                sz = spiel.Szene;
                await fl.NaechstesBildAsync();
                k = fl.AktuelleKamera!;
                zeilen = spiel.Ablauf.Stand.Verlauf.Count;
                var start = new Vector2(spiel.FigurX, spiel.FigurZ);
                var gehen = false;
                var sperre = false;
                Rect? ring = null;
                var (kopfUnten, leisteOben) = (0.0, 0.0);
                foreach (var (dx, dy) in new[] { (0.0, -1.0), (1.0, 0.0), (0.0, 1.0), (-1.0, 0.0) })
                {
                    id = ++finger;
                    fl.FingerAuf(id, links);
                    fl.FingerBewegt(id, links + new Point(dx * Sticks.Radius, dy * Sticks.Radius));
                    await ansicht.BildFertigAsync();
                    ring ??= seite.StickRing(Sticks.Seite.Links);
                    (kopfUnten, leisteOben) = seite.StickGrenzen;
                    for (var i = 0; i < 25; i++)
                    {
                        await fl.NaechstesBildAsync();
                        sperre |= Gesperrt();
                    }
                    fl.FingerAb(id, links + new Point(dx * Sticks.Radius, dy * Sticks.Radius));
                    if (Vector2.Distance(start, new Vector2(spiel.FigurX, spiel.FigurZ)) > 0.4f)
                    {
                        gehen = true;
                        break;
                    }
                }
                var weg = seite.StickRing(Sticks.Seite.Links) is null && !fl.StickZustand.Aktiv;
                var uhr = System.Diagnostics.Stopwatch.StartNew();
                while (spiel.FreiOffen && uhr.Elapsed.TotalSeconds < 3)
                {
                    await fl.NaechstesBildAsync();
                }
                var halt = !spiel.FreiOffen && spiel.Ablauf.Stand.Verlauf.Count == zeilen + 1;
                pruefe("Stick links gehen " + groesse, gehen && !sperre && weg && halt && ring is not null,
                    string.Create(inv, $"geht {gehen} ({Vector2.Distance(start, new Vector2(spiel.FigurX, spiel.FigurZ)):F2} weit), Sperre {sperre}, nach dem Loslassen weg {weg}, Ereignisse {spiel.Ablauf.Stand.Verlauf.Count - zeilen}"));
                var seiteBreite = seite.Bounds.Width;
                pruefe("Stick nie ueber Kopfzeile oder Leiste " + groesse, ring is { } r && r.Top >= kopfUnten - 0.5 && r.Bottom <= leisteOben + 0.5 && r.Left >= -0.5 && r.Right <= seiteBreite + 0.5
                    && r.Width >= Sticks.Radius - 0.5,
                    ring is { } rr ? string.Create(inv, $"Ring {rr.Left:F0},{rr.Top:F0} bis {rr.Right:F0},{rr.Bottom:F0}; Kopf bis {kopfUnten:F0}, Leiste ab {leisteOben:F0}, Breite {seiteBreite:F0}") : "kein Ring");

                Punkte();
                // c beide Sticks zugleich: links gehen, rechts drehen
                var drehung0 = k.Drehung;
                var (idL, idR) = (++finger, ++finger);
                fl.FingerAuf(idL, links);
                fl.FingerAuf(idR, rechts);
                fl.FingerBewegt(idR, rechts + new Point(Sticks.Radius, 0));
                fl.FingerBewegt(idL, links + new Point(0, -Sticks.Radius));
                await ansicht.BildFertigAsync();
                var beide = seite.StickRing(Sticks.Seite.Links) is not null && seite.StickRing(Sticks.Seite.Rechts) is not null;
                var vorBeide = new Vector2(spiel.FigurX, spiel.FigurZ);
                var sperreBeide = false;
                for (var i = 0; i < 20; i++)
                {
                    await fl.NaechstesBildAsync();
                    sperreBeide |= Gesperrt();
                }
                var gedreht = Math.Abs(((k.Drehung - drehung0 + 540f) % 360f) - 180f);
                fl.FingerAb(idL, links + new Point(0, -Sticks.Radius));
                fl.FingerAb(idR, rechts + new Point(Sticks.Radius, 0));
                await ansicht.BildFertigAsync();
                pruefe("zwei Sticks zugleich " + groesse, beide && gedreht > 5 && !sperreBeide && !fl.StickZustand.Aktiv,
                    string.Create(inv, $"beide sichtbar {beide}, Kamera {gedreht:F0} Grad gedreht, Figur {Vector2.Distance(vorBeide, new Vector2(spiel.FigurX, spiel.FigurZ)):F2} weit, Sperre {sperreBeide}"));

                Punkte();
                // d oben (obere Haelfte des freien Bereichs): kein Stick; zwei Finger ohne Stick-Daumen zoomen wie heute (Gegenprobe)
                id = ++finger;
                var oben = new Point(b.Breite / 4, b.Oben + (b.FreiHoehe / 4));
                fl.FingerAuf(id, oben);
                fl.FingerBewegt(id, oben + new Point(0, -40));
                var obenStick = fl.StickZustand.Aktiv;
                fl.FingerAb(id, oben + new Point(0, -40));
                var (abstand0, drehZwei0) = (k.Abstand, k.Drehung);
                var (z1, z2) = (++finger, ++finger);
                fl.FingerAuf(z1, oben);
                fl.FingerAuf(z2, links);
                fl.FingerBewegt(z2, links + new Point(50, 30));
                var zweiStick = fl.StickZustand.Aktiv;
                fl.FingerAb(z2, links + new Point(50, 30));
                fl.FingerAb(z1, oben);
                var zweiBewegt = Math.Abs(k.Abstand - abstand0) > 1e-3 || Math.Abs(k.Drehung - drehZwei0) > 1e-3;
                pruefe("oben kein Stick, zwei Finger wie heute " + groesse, !obenStick && !zweiStick && zweiBewegt,
                    string.Create(inv, $"Stick oben {obenStick}, Stick bei zwei Fingern {zweiStick}, Abstand {abstand0:F2} -> {k.Abstand:F2}, Drehung {drehZwei0:F0} -> {k.Drehung:F0}"));
                spiel.FreiHalt();

                if (name != "Handy hoch")
                {
                    continue;
                }
                Punkte();
                // e Ich-Sicht: rechter Stick sieht um (Blick hoch), links geht die Figur in Blickrichtung
                if (fl.IchUmschalten())
                {
                    for (var i = 0; i < 40 && !SpielKamera.ImIch(k); i++)
                    {
                        await fl.NaechstesBildAsync();
                    }
                    var neigung0 = k.IchNeigung;
                    var drehungIch = k.Drehung;
                    id = ++finger;
                    fl.FingerAuf(id, rechts);
                    fl.FingerBewegt(id, rechts + new Point(Sticks.Radius * 0.7, -Sticks.Radius * 0.7));
                    for (var i = 0; i < 15; i++)
                    {
                        await fl.NaechstesBildAsync();
                    }
                    fl.FingerAb(id, rechts + new Point(Sticks.Radius * 0.7, -Sticks.Radius * 0.7));
                    pruefe("Ich-Sicht rechter Stick sieht um", SpielKamera.ImIch(k) && k.IchNeigung < neigung0 && Math.Abs(k.Drehung - drehungIch) > 1,
                        string.Create(inv, $"Neigung {neigung0:F1} -> {k.IchNeigung:F1}, Drehung {drehungIch:F0} -> {k.Drehung:F0}"));
                    fl.IchUmschalten();
                    for (var i = 0; i < 40 && k.IchAnteil > 0; i++)
                    {
                        await fl.NaechstesBildAsync();
                    }
                }
                else
                {
                    pruefe("Ich-Sicht rechter Stick sieht um", false, "Ich-Sicht ging nicht an");
                }

                Punkte();
                // f Spiel-Handy offen: keine Sticks (Gegenprobe: zu = Stick geht wieder)
                var handy = await KarteOeffnenAsync(ansicht, seite);
                id = ++finger;
                fl.FingerAuf(id, links);
                fl.FingerBewegt(id, links + new Point(0, -Sticks.Radius));
                var mitHandy = fl.StickZustand.Aktiv || fl.SticksMoeglich;
                fl.FingerAb(id, links);
                await QuestlogSchliessenAsync(ansicht, handy);
                Punkte();
                id = ++finger;
                fl.FingerAuf(id, links);
                fl.FingerBewegt(id, links + new Point(0, -Sticks.Radius));
                var ohneHandy = fl.StickZustand.Aktiv;
                fl.FingerAb(id, links);
                spiel.FreiHalt();
                pruefe("Spiel-Handy offen: keine Sticks", handy is not null && !mitHandy && ohneHandy,
                    $"Handy offen {handy is not null}, Stick mit Handy {mitHandy}, danach (Gegenprobe) {ohneHandy}");
            }
        }
        finally
        {
            seite.Flaeche.SticksLos();
            fenster.Width = breiteVorher;
            fenster.Height = hoeheVorher;
            await ansicht.BildFertigAsync();
        }
    }
}

using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nicos Antworten P12-257 Abschnitt 2, P12-258): feste Kamera draussen und Karte im
/// Spiel-Handy, ueber echte Ereignisse (Maus, Finger, Tastatur) an der Spielflaeche und der Karte. Geprueft wird Nicos Ziel:
/// der Abstand bleibt nach Mausrad, +/−, zwei Fingern, Drehen, Laufen, Bus und Innenraum-Wechsel zwischen 7 und 9 (Start 8);
/// die Neigung nie ausserhalb 35 bis 70 Grad; Ziehen und Pfeiltasten verschieben draussen nicht; drinnen bleibt die Kamera wie
/// bis A 0.79.0. Karte: Antippen eines Orts (Maus, Finger, Eingabetaste, Screenreader) schliesst das Handy und startet den Weg
/// ohne weiteren Schritt; „Mit dem Bus“ faehrt. Gegenproben: eine Kamera ohne Bereich verlaesst 7 bis 9 und 35 bis 70; ein
/// Tipp neben jeden Ort tut nichts. Messpunkt „Spiel: Karte oeffnen (bis zum Bild)“ (wiederholbar, 400 ms). Dreiecke und
/// Aufrufe bei 8 und 9 (Neigung 35, 52, 70) misst die Nahansicht (AutoMessung.D1).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> KameraKarteAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Kamera " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Kamera " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Kamera: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        var gl = await GlAbwartenAsync(seite);
        try
        {
            if (gl)
            {
                funde += await KameraBedienungAsync(ansicht, seite, Pruefe);
            }
            else
            {
                Messwerte.Notiere("Spiel: Kamera nicht geprueft (ohne 3D gibt es keine Kamera, die Ortsliste ist die Bedienung)");
            }
            funde += await KarteAsync(ansicht, seite, gl, Pruefe);
        }
        finally
        {
            Dialog.AlleSchliessen();
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>
    /// Welle A 0.92.0: mit dem Mausrad aus der Ich-Sicht und dem Schulterblick heraus (derselbe Weg zurueck), bis die Kamera
    /// wieder die feste Kamera ist (fuer die Pruefungen dieser Welle, die danach die Kamera ohne Ich-Sicht pruefen).
    /// </summary>
    private static void IchVerlassen(StadtFlaeche fl, Kamera k, Point mitte)
    {
        for (var i = 0; i < 40 && (k.IchZiel > 0f || k.IchAnteil > 0f); i++)
        {
            Rad(fl, mitte, -1);
            if (k.IchZiel == 0f)
            {
                k.IchAnteil = 0f;   // den weichen Rueckweg nicht abwarten (die Bilder laufen hier nicht)
            }
        }
    }

    private static string Kam(Kamera k) => string.Create(CultureInfo.InvariantCulture, $"Abstand {k.Abstand:F2}, Neigung {k.Neigung:F1}, Drehung {k.Drehung:F1}");

    private static bool ImBereich(Kamera k) =>
        k.Abstand >= SpielKamera.AbstandMin - 0.001f && k.Abstand <= SpielKamera.AbstandMax + 0.001f
        && k.Neigung >= SpielKamera.NeigungMin - 0.001f && k.Neigung <= SpielKamera.NeigungMax + 0.001f;

    /// <summary>Zeiger-Ereignis an <paramref name="ziel"/> bei (x, y) in Punkten des Ziels.</summary>
    private static (Visual Wurzel, Point Punkt)? Wurzelpunkt(Control ziel, Point p) =>
        TopLevel.GetTopLevel(ziel) is { } wurzel ? (wurzel, ziel.TranslatePoint(p, wurzel) ?? p) : null;

    private static void Druecken(Control ziel, IPointer zeiger, Point p, bool rechts = false)
    {
        if (Wurzelpunkt(ziel, p) is not { } w)
        {
            return;
        }
        var props = new PointerPointProperties(rechts ? RawInputModifiers.RightMouseButton : RawInputModifiers.LeftMouseButton,
            rechts ? PointerUpdateKind.RightButtonPressed : PointerUpdateKind.LeftButtonPressed);
        ziel.RaiseEvent(new PointerPressedEventArgs(ziel, zeiger, w.Wurzel, w.Punkt, 0, props, KeyModifiers.None));
    }

    private static void Bewegen(Control ziel, IPointer zeiger, Point p, bool rechts = false)
    {
        if (Wurzelpunkt(ziel, p) is not { } w)
        {
            return;
        }
        var props = new PointerPointProperties(rechts ? RawInputModifiers.RightMouseButton : RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
        ziel.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, ziel, zeiger, w.Wurzel, w.Punkt, 0, props, KeyModifiers.None));
    }

    private static void Loslassen(Control ziel, IPointer zeiger, Point p, bool rechts = false)
    {
        if (Wurzelpunkt(ziel, p) is not { } w)
        {
            return;
        }
        var props = new PointerPointProperties(RawInputModifiers.None, rechts ? PointerUpdateKind.RightButtonReleased : PointerUpdateKind.LeftButtonReleased);
        ziel.RaiseEvent(new PointerReleasedEventArgs(ziel, zeiger, w.Wurzel, w.Punkt, 0, props, KeyModifiers.None, rechts ? MouseButton.Right : MouseButton.Left));
    }

    private static void Rad(Control ziel, Point p, double rasten)
    {
        if (Wurzelpunkt(ziel, p) is not { } w)
        {
            return;
        }
        var zeiger = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        ziel.RaiseEvent(new PointerWheelEventArgs(ziel, zeiger, w.Wurzel, w.Punkt, 0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None, new Vector(0, rasten)));
    }

    private static void TasteAn(Control ziel, Key taste)
    {
        ziel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, Source = ziel });
        ziel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = taste, Source = ziel });
    }

    private static async Task<int> KameraBedienungAsync(MainView ansicht, SpielSeite seite, Action<string, bool, string> pruefe)
    {
        var inv = CultureInfo.InvariantCulture;
        var spiel = Frisch(seite);
        var fl = seite.Flaeche;
        await fl.NaechstesBildAsync();
        var k = fl.AktuelleKamera!;
        // A 0.96.0: Mitte des freien Bereichs (zwischen Kopfzeile und Tafel), nie im Stick-Bereich unten (Gesten wie bisher)
        var mitte = MitteFrei(fl);
        pruefe("Start", k.Abstand == SpielKamera.Abstand && k.Neigung == SpielKamera.NeigungStart && fl.KameraFest
            && k.Bereich == (SpielKamera.AbstandMin, SpielKamera.AbstandMax) && k.NeigungBereich == (SpielKamera.NeigungMin, SpielKamera.NeigungMax), Kam(k));
        var nieRaus = new List<string>();
        void Merken(string schritt)
        {
            if (!ImBereich(k))
            {
                nieRaus.Add(schritt + ": " + Kam(k));
            }
        }

        // Maus: Mausrad weit hinaus und weit hinein (Rest-Zoom 7 bis 9)
        for (var i = 0; i < 20; i++)
        {
            Rad(fl, mitte, -1);
            Merken("Mausrad hinaus");
        }
        var weit = k.Abstand;
        for (var i = 0; i < 20; i++)
        {
            Rad(fl, mitte, 1);
            Merken("Mausrad hinein");
        }
        var nah = k.Abstand;
        // Welle A 0.92.0 (Weisung P12-301 Punkt 1): ganz heran fuehrt in die Ich-Sicht (Abstand bleibt 7); heraus wieder bis 9
        var ichNachRad = SpielKamera.ImIch(k);
        IchVerlassen(fl, k, mitte);
        pruefe("Mausrad 7 bis 9", Math.Abs(weit - SpielKamera.AbstandMax) < 0.001f && Math.Abs(nah - SpielKamera.AbstandMin) < 0.001f && ichNachRad
            && k.IchZiel == 0f && k.IchAnteil == 0f,
            string.Create(inv, $"weitester {weit:F2}, naechster {nah:F2}, ganz heran Ich-Sicht {ichNachRad}, danach heraus Ich {k.IchAnteil:F2}"));

        // Maus: rechte Taste waagrecht dreht, nach oben steiler (bis 70), nach unten flacher (bis 35), nie Gehen
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var (d0, zx, zz) = (k.Drehung, k.ZielX, k.ZielZ);
        Druecken(fl, maus, mitte, rechts: true);
        Bewegen(fl, maus, new Point(mitte.X + 100, mitte.Y - 400), rechts: true);
        Merken("rechte Maus steiler");
        var steil = k.Neigung;
        Bewegen(fl, maus, new Point(mitte.X + 100, mitte.Y + 400), rechts: true);
        Merken("rechte Maus flacher");
        var flach = k.Neigung;
        Loslassen(fl, maus, new Point(mitte.X + 100, mitte.Y + 400), rechts: true);
        var gedreht = ((k.Drehung - d0) % 360f + 360f) % 360f;
        pruefe("rechte Maustaste dreht und neigt", steil == SpielKamera.NeigungMax && flach == SpielKamera.NeigungMin
            && Math.Abs(gedreht - (100 * Kameragriff.GradJePunkt)) < 0.01f && !spiel.Geht,
            string.Create(inv, $"steilste {steil:F1}, flachste {flach:F1}, gedreht {gedreht:F1}, Figur geht {spiel.Geht}"));
        k.Neigung = SpielKamera.NeigungStart;

        // Maus: linke Taste ziehen verschiebt draussen nichts mehr (Nico Antwort 3), kein Gehen
        var links = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        (zx, zz) = (k.ZielX, k.ZielZ);
        Druecken(fl, links, mitte);
        Bewegen(fl, links, new Point(mitte.X + 200, mitte.Y + 120));
        Loslassen(fl, links, new Point(mitte.X + 200, mitte.Y + 120));
        pruefe("linke Maus verschiebt nicht", k.ZielX == zx && k.ZielZ == zz && !spiel.Geht, string.Create(inv, $"Ziel {k.ZielX:F2}/{k.ZielZ:F2}, Figur geht {spiel.Geht}"));

        // Finger: ein Finger zieht (nichts), zwei Finger auseinander/zusammen (7 bis 9), drehen, gemeinsam hoch/runter (neigen)
        var f1 = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druecken(fl, f1, mitte);
        Bewegen(fl, f1, new Point(mitte.X - 150, mitte.Y + 90));
        Loslassen(fl, f1, new Point(mitte.X - 150, mitte.Y + 90));
        pruefe("ein Finger verschiebt nicht", k.ZielX == zx && k.ZielZ == zz && !spiel.Geht, string.Create(inv, $"Ziel {k.ZielX:F2}/{k.ZielZ:F2}"));
        var a = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var b = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Point pa = new(mitte.X - 20, mitte.Y), pb = new(mitte.X + 20, mitte.Y);
        Druecken(fl, a, pa);
        Druecken(fl, b, pb);
        for (var i = 1; i <= 10; i++)
        {
            (pa, pb) = (new Point(mitte.X - 20 - (i * 15), mitte.Y), new Point(mitte.X + 20 + (i * 15), mitte.Y));
            Bewegen(fl, a, pa);
            Bewegen(fl, b, pb);
            Merken("zwei Finger auseinander");
        }
        var fingerNah = k.Abstand;
        for (var i = 1; i <= 10; i++)
        {
            (pa, pb) = (new Point(mitte.X - 170 + (i * 15), mitte.Y), new Point(mitte.X + 170 - (i * 15), mitte.Y));
            Bewegen(fl, a, pa);
            Bewegen(fl, b, pb);
            Merken("zwei Finger zusammen");
        }
        var fingerWeit = k.Abstand;
        IchVerlassen(fl, k, mitte);
        var dVor = k.Drehung;
        Bewegen(fl, b, new Point(pb.X, pb.Y + 20));
        var fingerDreh = Math.Abs(k.Drehung - dVor) > 1;
        for (var i = 1; i <= 10; i++)
        {
            (pa, pb) = (new Point(pa.X, pa.Y - 60), new Point(pb.X, pb.Y - 60));
            Bewegen(fl, a, pa);
            Bewegen(fl, b, pb);
            Merken("zwei Finger hoch");
        }
        var fingerSteil = k.Neigung;
        for (var i = 1; i <= 20; i++)
        {
            (pa, pb) = (new Point(pa.X, pa.Y + 60), new Point(pb.X, pb.Y + 60));
            Bewegen(fl, a, pa);
            Bewegen(fl, b, pb);
            Merken("zwei Finger runter");
        }
        var fingerFlach = k.Neigung;
        Loslassen(fl, a, pa);
        Loslassen(fl, b, pb);
        pruefe("zwei Finger zoomen 7 bis 9, drehen und neigen 35 bis 70",
            Math.Abs(fingerNah - SpielKamera.AbstandMin) < 0.001f && Math.Abs(fingerWeit - SpielKamera.AbstandMax) < 0.001f && fingerDreh
            && fingerSteil == SpielKamera.NeigungMax && fingerFlach == SpielKamera.NeigungMin && !spiel.Geht,
            string.Create(inv, $"naechster {fingerNah:F2}, weitester {fingerWeit:F2}, gedreht {fingerDreh}, steilste {fingerSteil:F1}, flachste {fingerFlach:F1}"));
        k.Neigung = SpielKamera.NeigungStart;

        // Tastatur (Ereignisse an der Flaeche): Pfeile nichts, Q/E drehen, R/F neigen, +/− im Bereich
        fl.Focus();
        (zx, zz) = (k.ZielX, k.ZielZ);
        foreach (var t in new[] { Key.Left, Key.Right, Key.Up, Key.Down })
        {
            TasteAn(fl, t);
        }
        var pfeileNichts = k.ZielX == zx && k.ZielZ == zz;
        var dq = k.Drehung;
        TasteAn(fl, Key.Q);
        var qDreht = Math.Abs(k.Drehung - dq) > 1;
        for (var i = 0; i < 12; i++)
        {
            TasteAn(fl, Key.R);
            Merken("Taste R");
        }
        var tSteil = k.Neigung;
        for (var i = 0; i < 12; i++)
        {
            TasteAn(fl, Key.F);
            Merken("Taste F");
        }
        var tFlach = k.Neigung;
        for (var i = 0; i < 12; i++)
        {
            TasteAn(fl, Key.Subtract);
            Merken("Taste −");
        }
        var tWeit = k.Abstand;
        for (var i = 0; i < 12; i++)
        {
            TasteAn(fl, Key.Add);
            Merken("Taste +");
        }
        var tNah = k.Abstand;
        var tIch = SpielKamera.ImIch(k);
        IchVerlassen(fl, k, mitte);
        pruefe("Tastatur", pfeileNichts && qDreht && tSteil == SpielKamera.NeigungMax && tFlach == SpielKamera.NeigungMin
            && Math.Abs(tWeit - SpielKamera.AbstandMax) < 0.001f && Math.Abs(tNah - SpielKamera.AbstandMin) < 0.001f && tIch,
            string.Create(inv, $"Pfeile verschieben {(pfeileNichts ? "nicht" : "NOCH")}, Q dreht {qDreht}, R bis {tSteil:F1}, F bis {tFlach:F1}, − bis {tWeit:F2}, + bis {tNah:F2} (dann Ich-Sicht {tIch})"));
        k.Neigung = SpielKamera.NeigungStart;
        k.Zoomen(k.Abstand / SpielKamera.Abstand);

        // Laufen: die Figur geht zu einem fernen Ort, die Kamera folgt im Bereich
        var ziel = spiel.Szene.Orte.OrderByDescending(o => MathF.Abs(o.StehX - spiel.FigurX) + MathF.Abs(o.StehZ - spiel.FigurZ)).First().Id;
        spiel.GeheZu(ziel);
        var uhr = Stopwatch.StartNew();
        var bilder = 0;
        while (spiel.Geht && uhr.Elapsed.TotalSeconds < 20)
        {
            await fl.NaechstesBildAsync();
            Merken("Laufen");
            bilder++;
        }
        spiel.SofortAnkommen();
        fl.KameraAufFigur();
        await fl.NaechstesBildAsync();
        Merken("angekommen");
        pruefe("Laufen bleibt im Bereich", bilder > 0, string.Create(inv, $"{bilder} Bilder beim Laufen zu {ziel}, {Kam(k)}"));

        // Innenraum: wie bis A 0.79.0 (ganzer Raum, Neigung 58, freier Zoom), danach draussen wieder im Bereich
        if (ZurBitweiche(spiel) && spiel.Betreten())
        {
            await fl.NaechstesBildAsync();
            var innen = fl.AktuelleKamera!;
            var raum = spiel.Innen!;
            var soll = Math.Clamp(Math.Max(raum.Breite, raum.Tiefe) * 0.85f, 12f, 28f);
            var wieHeute = innen != k && !fl.KameraFest && Math.Abs(innen.Abstand - soll) < 0.001f && innen.Neigung == 58f;
            var neigungInnen = innen.Neigung;
            Rad(fl, mitte, -3);
            var innenZoom = innen.Abstand > soll;
            Druecken(fl, maus, mitte, rechts: true);
            Bewegen(fl, maus, new Point(mitte.X, mitte.Y - 300), rechts: true);
            Loslassen(fl, maus, new Point(mitte.X, mitte.Y - 300), rechts: true);
            pruefe("drinnen wie heute", wieHeute && innenZoom && innen.Neigung == neigungInnen,
                string.Create(inv, $"Abstand {soll:F1} soll, nach Mausrad {innen.Abstand:F1}, Neigung {innen.Neigung:F0} (rechte Maus neigt drinnen nicht)"));
            spiel.Verlassen();
            await fl.NaechstesBildAsync();
            Merken("nach dem Innenraum");
            pruefe("nach dem Innenraum draussen", ReferenceEquals(fl.AktuelleKamera, k) && fl.KameraFest && ImBereich(k), Kam(k));
        }
        else
        {
            pruefe("drinnen wie heute", false, "Bitweiche nicht betreten");
        }

        // Bus: mit dem Bus unterwegs bleibt die Kamera im Bereich (Stadt mit Haltestellen)
        var bus = MitBus(seite);
        await fl.NaechstesBildAsync();
        k = fl.AktuelleKamera!;
        if (bus.MitBusZu("amt") == Core.Spiel.StadtGrund.Ok)
        {
            var u = Stopwatch.StartNew();
            var imBus = 0;
            while (bus.Geht && u.Elapsed.TotalSeconds < 90)
            {
                Rad(fl, mitte, imBus % 2 == 0 ? -2 : 2);
                await fl.NaechstesBildAsync();
                Merken("Bus");
                imBus += bus.ImBus ? 1 : 0;
            }
            pruefe("Bus bleibt im Bereich", imBus > 0, string.Create(inv, $"{imBus} Bilder im Bus, {Kam(k)}"));
        }
        else
        {
            pruefe("Bus bleibt im Bereich", false, "Bus faehrt nicht zum Amt");
        }
        pruefe("Abstand 7 bis 9 und Neigung 35 bis 70 nach jedem Schritt", nieRaus.Count == 0, nieRaus.Count == 0 ? "ja" : string.Join("; ", nieRaus.Take(5)));

        // Gegenprobe: eine Kamera ohne Bereich (wie bis A 0.79.0, Werkzeug) verlaesst beides; die Pruefung oben wuerde es sehen
        var frei = new Kamera(0, 0, SpielKamera.Abstand);
        Kamerabedienung.Mausrad(frei, -20);
        Kamerabedienung.RechteMaus(frei, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: -400, Grad: 0));
        pruefe("Gegenprobe ohne Bereich", !ImBereich(frei), Kam(frei) + (ImBereich(frei) ? " (NICHT erkannt)" : " (erkannt)"));
        return 0;
    }

    private static async Task<int> KarteAsync(MainView ansicht, SpielSeite seite, bool gl, Action<string, bool, string> pruefe)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        var spiel = Frisch(seite);
        if (spiel.ImInnenraum)
        {
            spiel.Verlassen();
        }
        await ansicht.BildFertigAsync();
        // Welle A 0.81.0 (Sammelliste 350): die Karte gibt es nur noch im Handy, ohne 3D bleibt der Knopf „Orte“
        var karteUnten = seite.LeisteKnoepfe.Contains(Core.Texte.KarteTexte.Karte);
        pruefe("Karte nur im Handy", !karteUnten && (gl ? !seite.OrteKnopfSichtbar : seite.OrteKnopfSichtbar),
            (karteUnten ? "Knopf Karte noch in der Leiste" : "kein Knopf Karte") + (seite.OrteKnopfSichtbar ? ", Orte da" : ", Orte weg") + (gl ? "" : " (ohne 3D)"));

        // Messpunkt: Karte oeffnen bis zum Bild (seit A 0.81.0 immer ueber das Handy)
        Task? offen = null;
        async Task<double?> Oeffnen()
        {
            var u = Stopwatch.StartNew();
            offen = await KarteOeffnenAsync(ansicht, seite);
            return u.Elapsed.TotalMilliseconds;
        }
        async Task Zu()
        {
            Dialog.AlleSchliessen();
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
        }
        var erste = await Oeffnen();
        var bild = seite.KarteGezeigt;
        var orte = spiel.Szene.Orte.Select(o => o.Id).ToList();
        pruefe("Karte offen", Dialog.Offen is not null && seite.HandyReiterGerade == SpielSeite.HandyReiter.Karte && bild is not null,
            Dialog.Offen is null ? "zu" : "offen, Reiter " + seite.HandyReiterGerade);
        var karte = bild?.Karte;
        var jeOrtEinmal = karte is not null && karte.Orte.Select(o => o.Id).SequenceEqual(orte) && karte.Orte.Select(o => o.Nr).SequenceEqual(Enumerable.Range(1, orte.Count))
            && orte.All(id => seite.KarteHingehen.TryGetValue(id, out var k) && k.IsVisible && Avalonia.Automation.AutomationProperties.GetName(k) is { Length: > 0 });
        pruefe("Karte jeder Ort genau einmal mit Knopf", jeOrtEinmal, string.Create(inv, $"{karte?.Orte.Count ?? 0} Orte auf der Karte, {seite.KarteHingehen.Count} Knoepfe, {orte.Count} in der Stadt"));
        var marken = bild is null ? 0 : orte.Count(id => bild.MarkeVon(id) is { } p && p.X >= 0 && p.Y >= 0 && p.X <= bild.Bounds.Width && p.Y <= bild.Bounds.Height);
        pruefe("Karte alle Marken im Bild", marken == orte.Count && bild!.Bounds.Width > 100, string.Create(inv, $"{marken} von {orte.Count}, Karte {bild?.Bounds.Width:F0} x {bild?.Bounds.Height:F0}"));
        // Welle A 0.83.0 (Weisung P12-283 Fund 1): jede Marke ganz in der Karte, keine ueberlappt (offene Karte und Lagen)
        var offenFund = bild?.MarkenFund() ?? ["keine Karte"];
        pruefe("Karte Marken ganz zu sehen (offen)", offenFund.Count == 0, offenFund.Count == 0 ? "ohne Fund" : string.Join("; ", offenFund));
        var (lagenFund, gegenRot, lagenWert) = MarkenPruefen(spiel.Szene);
        pruefe("Karte Marken ganz zu sehen (Lagen)", lagenFund.Count == 0, lagenFund.Count == 0 ? lagenWert : string.Join("; ", lagenFund));
        pruefe("Karte Marken Gegenprobe wie bis A 0.82.0", gegenRot, gegenRot ? "Pruefung meldet Funde" : "Pruefung erkennt Ueberlappung nicht");
        await Zu();
        funde += Ergebnis("Spiel: Karte oeffnen (bis zum Bild)", await Messwiederholung.MessenAsync(erste, Geraetegrenze, Oeffnen, Zu, Task.Delay));
        await Zu();

        // Antippen: Maus, Finger, Eingabetaste, Screenreader; danach Handy zu und die Figur unterwegs, genau ein neuer Weg
        var wege = new List<string>();
        async Task<bool> Probe(string art, Func<StadtKarteBild, string, Task> tun)
        {
            var s = Frisch(seite);
            if (s.ImInnenraum)
            {
                s.Verlassen();
            }
            await ansicht.BildFertigAsync();
            var z = s.Szene.Orte.Select(o => o.Id).First(id => id != s.Jetzt.Ort && s.Szene.Ort(id) is { } o
                && MathF.Abs(o.StehX - s.FigurX) + MathF.Abs(o.StehZ - s.FigurZ) > 4);
            var t = await KarteOeffnenAsync(ansicht, seite);
            if (seite.KarteGezeigt is not { } b)
            {
                wege.Add(art + ": Karte nicht offen");
                return false;
            }
            var zeilen = s.Ablauf.Stand.Verlauf.Count;
            await tun(b, z);
            if (t is not null)
            {
                await Task.WhenAny(t, Task.Delay(3000));
            }
            await ansicht.BildFertigAsync();
            var zu = Dialog.Offen is null;
            var los = s.Geht || s.Jetzt.Ort == z;
            s.SofortAnkommen();
            var dort = s.Jetzt.Ort == z;
            var einWeg = s.Ablauf.Stand.Verlauf.Count == zeilen + 1;
            wege.Add(string.Create(inv, $"{art}: Handy {(zu ? "zu" : "OFFEN")}, Figur {(los ? "geht los" : "STEHT")}, Ziel {z} {(dort ? "erreicht" : "NICHT")}, {s.Ablauf.Stand.Verlauf.Count - zeilen} Schritte"));
            Dialog.AlleSchliessen();
            await ansicht.BildFertigAsync();
            return zu && los && dort && einWeg;
        }
        var okMaus = await Probe("Maus", async (b, z) =>
        {
            var p = b.MarkeVon(z)!.Value;
            b.BringIntoView(new Rect(p.X - 20, p.Y - 20, 40, 40));
            await ansicht.BildFertigAsync();
            var m = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            Druecken(b, m, p);
            Loslassen(b, m, p);
        });
        var okFinger = await Probe("Finger", async (b, z) =>
        {
            var p = b.MarkeVon(z)!.Value;
            b.BringIntoView(new Rect(p.X - 20, p.Y - 20, 40, 40));
            await ansicht.BildFertigAsync();
            var f = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
            Druecken(b, f, p);
            Loslassen(b, f, p);
        });
        var okTaste = await Probe("Eingabetaste", async (b, z) =>
        {
            var knopf = seite.KarteHingehen[z];
            knopf.BringIntoView();
            knopf.Focus(NavigationMethod.Tab);
            await ansicht.BildFertigAsync();
            knopf.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = knopf });
        });
        var okLeser = await Probe("Screenreader", (b, z) =>
        {
            (ControlAutomationPeer.CreatePeerForElement(seite.KarteHingehen[z]) as IInvokeProvider)?.Invoke();
            return Task.CompletedTask;
        });
        pruefe("Karte antippen geht sofort los", okMaus && okFinger && okTaste && okLeser, string.Join("; ", wege));

        // Gegenprobe: ein Tipp auf freie Wiese (weit weg von jeder Marke und jedem Ort) tut nichts, das Handy bleibt offen
        Frisch(seite);
        var tg = await KarteOeffnenAsync(ansicht, seite);
        var leer = false;
        if (seite.KarteGezeigt is { } kb && kb.Karte is { } kk)
        {
            var s = kb.Massstab;
            var frei = Enumerable.Range(0, 40).SelectMany(i => Enumerable.Range(0, 40).Select(j => new Point((i + 0.5) * kb.Bounds.Width / 40, (j + 0.5) * kb.Bounds.Height / 40)))
                .FirstOrDefault(p => kk.Treffer(new System.Numerics.Vector2((float)p.X, (float)p.Y), s, (float)Design.Schrift.Faktor) is null);
            var m = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            Druecken(kb, m, frei);
            Loslassen(kb, m, frei);
            await ansicht.BildFertigAsync();
            leer = Dialog.Offen is not null && !seite.Spiel!.Geht;
        }
        pruefe("Karte Gegenprobe freie Stelle", leer, leer ? "nichts passiert, Handy offen" : "Tipp neben die Orte hat etwas ausgeloest");
        await QuestlogSchliessenAsync(ansicht, tg);

        // Mit dem Bus aus der Karte (Stadt mit Haltestellen): faehrt, ein Ereignis wie die Ortsliste
        var bus = MitBus(seite);
        await ansicht.BildFertigAsync();
        var tb = await KarteOeffnenAsync(ansicht, seite);
        var zeilenBus = bus.Ablauf.Stand.Verlauf.Count;
        var mitBus = seite.KarteMitBus.TryGetValue("amt", out var busKnopf);
        busKnopf?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        if (tb is not null)
        {
            await Task.WhenAny(tb, Task.Delay(3000));
        }
        await ansicht.BildFertigAsync();
        var faehrt = mitBus && Dialog.Offen is null && bus.Ablauf.Stand.Verlauf.Count == zeilenBus + 1
            && Core.Spiel.Py.Gleich(Core.Spiel.SpielDaten.Get(bus.Ablauf.Stand.Verlauf[^1].Daten, Core.Spiel.Stadt.Mit, null), Core.Spiel.Stadt.MitBus);
        pruefe("Karte Mit dem Bus", faehrt, mitBus ? (faehrt ? "faehrt, ein Ereignis" : "Knopf da, faehrt nicht") : "kein Knopf beim Amt");
        bus.SofortAnkommen();
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
        return funde;
    }
}

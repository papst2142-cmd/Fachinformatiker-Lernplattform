using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.97.0 (Weisung P12-312 Punkt 1, Nico 1a, Sammelliste 392): drinnen zoomt die Kamera nur so nah, dass Scheitel und
/// Fuesse der Figur im freien Bereich zwischen Kopfzeile und Tafel bleiben; naeher geht es nur in die Ich-Sicht. Abnahmesatz:
/// „Drinnen ist bei jedem Zoom auf Handy hoch/quer und Tablet hoch/quer die ganze Figur zu sehen.“ Die Tests nehmen dieselbe
/// Rechnung wie die Spielflaeche (<see cref="SpielKamera.Drinnen"/>, <see cref="SpielKamera.InnenBereich"/>,
/// <see cref="Kamerabedienung"/>) und pruefen das Bild ueber <see cref="Kamera.Bildpunkt"/>. Gegenprobe: mit der alten Grenze 2
/// verlaesst der Kopf auf jedem Bildschirm den freien Bereich.
/// </summary>
public sealed class SpielInnenKameraTests
{
    /// <summary>
    /// Bildschirme mit Kopfzeile oben und Tafel unten in Punkten, wie das Spiel sie hat (gemessen fuer Meldung P12-191: freier
    /// Bereich Handy hoch 64 bis 597, Tablet hoch 64 bis 1105, Tablet quer 64 bis 625, Handy quer 64 bis 209); dazu zwei
    /// Faelle mit groesserer Tafel.
    /// </summary>
    public static readonly (string Name, float Breite, float Hoehe, float Oben, float Unten)[] Bildschirme =
    [
        ("Handy hoch", 384, 832, 64, 235),
        ("Handy quer", 832, 384, 64, 175),
        ("Tablet hoch", 800, 1280, 64, 175),
        ("Tablet quer", 1280, 800, 64, 175),
        ("Handy hoch, Tafel gross", 384, 832, 64, 400),
        ("Handy quer, Tafel gross", 832, 384, 64, 230),
    ];

    private static readonly float[] Drehungen = [0f, 90f, 180f, 270f, 37f];

    /// <summary>Funde fuer eine Kamera, deren Ziel auf der Figur steht: Scheitel oder Fuesse ausserhalb des freien Bereichs.</summary>
    private static string? Fund(Kamera k, (string Name, float Breite, float Hoehe, float Oben, float Unten) b)
    {
        k.Versatz = SpielKamera.FreiVersatz(b.Hoehe, b.Oben, b.Unten);
        var fuss = k.Bildpunkt(k.Ziel, b.Breite, b.Hoehe);
        var kopf = k.Bildpunkt(k.Ziel + new Vector3(0, Innenraum.FigurHoehe * SpielKamera.ScheitelZugabe, 0), b.Breite, b.Hoehe);
        if (fuss is not { } f || kopf is not { } s)
        {
            return $"{b.Name}: hinter der Kamera (Abstand {k.Abstand})";
        }
        if (s.Y < b.Oben || f.Y > b.Hoehe - b.Unten || s.X < 0 || s.X > b.Breite || f.X < 0 || f.X > b.Breite)
        {
            return $"{b.Name}, Abstand {k.Abstand:F2}, Drehung {k.Drehung}: Scheitel y {s.Y:F0}, Fuesse y {f.Y:F0}, frei {b.Oben} bis {b.Hoehe - b.Unten}";
        }
        return null;
    }

    /// <summary>Raeume aller Fassungen (Groesse, Grenzen) wie im Spiel.</summary>
    private static List<Innenraum> Raeume()
    {
        var raeume = new List<Innenraum>();
        var groessen = new HashSet<(float, float)>();
        foreach (var (_, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume))
            {
                if (groessen.Add((innen.Breite, innen.Tiefe)))
                {
                    raeume.Add(innen);
                }
            }
        }
        return raeume;
    }

    /// <summary>
    /// Ganz heranzoomen mit Mausrad, Taste + und zwei Fingern (je Schritt ein Bild) bis in die Ich-Sicht; vor der Ich-Sicht muss
    /// die ganze Figur im Bild sein. <paramref name="bereichSetzen"/> = false ist die Gegenprobe (alte Grenze 2).
    /// </summary>
    private static List<string> HeranZoomen(Innenraum innen, (string Name, float Breite, float Hoehe, float Oben, float Unten) b,
        float drehung, Vector2 figur, int weg, bool bereichSetzen)
    {
        var funde = new List<string>();
        var k = SpielKamera.Drinnen(innen.Breite, innen.Tiefe, true);
        k.Drehung = drehung;
        k.Setzen(figur.X, figur.Y);
        for (var i = 0; i < 200 && k.IchZiel < 1f; i++)
        {
            if (bereichSetzen)
            {
                // die Spielflaeche setzt den Bereich bei jedem Zugriff auf die Kamera (AktuelleKamera)
                SpielKamera.InnenBereich(k, b.Hoehe, b.Oben, b.Unten, Innenraum.FigurHoehe);
            }
            if (Fund(k, b) is { } f)
            {
                funde.Add($"{innen.Ansicht} {f}");
                break;
            }
            switch (weg)
            {
                case 0:
                    Kamerabedienung.Mausrad(k, 1);
                    break;
                case 1:
                    Kamerabedienung.Taste(k, false, Tastenbelegung.Naeher, b.Hoehe);
                    break;
                default:
                    Kamerabedienung.ZweiFinger(k, false, 1.07, 0, 0);
                    break;
            }
        }
        return funde;
    }

    [Fact]
    public void Drinnen_JederZoom_GanzeFigurImFreienBereich()
    {
        var raeume = Raeume();
        Assert.True(raeume.Count >= 5, "Raeume: " + raeume.Count);
        var funde = new List<string>();
        var geprueft = 0;
        foreach (var innen in raeume)
        {
            Vector2[] stellen = [new(innen.Breite / 2, innen.Tiefe / 2), new(0.5f, 0.5f), new(innen.Breite - 0.5f, innen.Tiefe - 0.5f)];
            foreach (var b in Bildschirme)
            {
                foreach (var drehung in Drehungen)
                {
                    foreach (var stelle in stellen)
                    {
                        for (var weg = 0; weg < 3; weg++)
                        {
                            funde.AddRange(HeranZoomen(innen, b, drehung, stelle, weg, true));
                            geprueft++;
                        }
                    }
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde.Take(20)));
        Assert.True(geprueft >= 5 * Bildschirme.Length * Drehungen.Length * 9, "geprueft: " + geprueft);
    }

    [Fact]
    public void Gegenprobe_AlteGrenze2_KopfVerlaesstDenFreienBereich()
    {
        // so war es bis A 0.96.0: Bereich 2 bis 40, der Kopf geht beim Heranzoomen ueber den oberen Rand (Befund 392)
        var innen = Raeume()[0];
        foreach (var b in Bildschirme)
        {
            var funde = HeranZoomen(innen, b, 0f, new Vector2(innen.Breite / 2, innen.Tiefe / 2), 0, false);
            Assert.True(funde.Count > 0, b.Name + ": mit der alten Grenze kein Fund (Gegenprobe greift nicht)");
        }
    }

    [Fact]
    public void KleinsterAbstand_JeBildschirm_WieGemessen_UndIchSichtBleibt()
    {
        // Meldung P12-191: Kopf ganz sichtbar ab Abstand 10 (Handy hoch), 9 (Tablet hoch und quer), 15 (Handy quer) bei der
        // Figur bis A 1.0.0 (0,6 m je Einheit); seit A 1.1.0 (Massstab innen 0,75 m, Weisung P12-328) ist die Figur in Einheiten
        // im Verhaeltnis 0,8 kleiner, der kleinste Abstand ebenso (7,66 / 11,97 / 6,44 / 7,17), die Figur fuellt das Bild gleich
        float Min((string Name, float Breite, float Hoehe, float Oben, float Unten) b, float figur) =>
            SpielKamera.InnenAbstandMin(b.Hoehe, b.Oben, b.Unten, 58f, 40f, figur);
        var bisher = 1.75f * Innenraum.MeterBisA100;
        Assert.InRange(Min(Bildschirme[0], bisher), 9f, 10f);
        Assert.InRange(Min(Bildschirme[1], bisher), 14f, 15f);
        Assert.InRange(Min(Bildschirme[2], bisher), 8f, 9f);
        Assert.InRange(Min(Bildschirme[3], bisher), 8f, 9f);
        Assert.InRange(Min(Bildschirme[0], Innenraum.FigurHoehe), 7.6f, 7.7f);
        Assert.InRange(Min(Bildschirme[1], Innenraum.FigurHoehe), 11.9f, 12f);
        Assert.InRange(Min(Bildschirme[2], Innenraum.FigurHoehe), 6.4f, 6.5f);
        Assert.InRange(Min(Bildschirme[3], Innenraum.FigurHoehe), 7.1f, 7.2f);
        // naeher als der kleinste Abstand fuehrt weiter in die Ich-Sicht und zurueck
        var b = Bildschirme[0];
        var k = SpielKamera.Drinnen(20, 20, true);
        SpielKamera.InnenBereich(k, b.Hoehe, b.Oben, b.Unten, Innenraum.FigurHoehe);
        Kamerabedienung.Mausrad(k, 100);
        Assert.Equal(Min(b, Innenraum.FigurHoehe), k.Abstand);
        Assert.Equal(0f, k.IchZiel);
        Kamerabedienung.Mausrad(k, 1);
        Assert.Equal(1f, k.IchZiel);
        Kamerabedienung.Mausrad(k, -1);
        Assert.Equal(0f, k.IchZiel);
        // weitester Abstand bleibt 40
        Kamerabedienung.Mausrad(k, -100);
        Assert.Equal(Kamera.AbstandMax, k.Abstand);
    }

    [Fact]
    public void Drehen_DesGeraets_HoltDieKameraZurueck_UndMessungBleibtFrei()
    {
        var hoch = Bildschirme[0];
        var quer = Bildschirme[1];
        var k = SpielKamera.Drinnen(20, 20, true);
        SpielKamera.InnenBereich(k, hoch.Hoehe, hoch.Oben, hoch.Unten, Innenraum.FigurHoehe);
        Kamerabedienung.Mausrad(k, 100);
        Assert.Null(Fund(k, hoch));
        // Geraet gedreht: im naechsten Bild holt der Bereich die Kamera weiter weg (ganze Figur auch quer)
        SpielKamera.InnenBereich(k, quer.Hoehe, quer.Oben, quer.Unten, Innenraum.FigurHoehe);
        Assert.Null(Fund(k, quer));
        // waehrend einer Messung (feste Abstaende) setzt die Flaeche den Bereich nicht
        using (k.MessungFrei())
        {
            Assert.True(k.MessungLaeuft);
            SpielKamera.InnenBereich(k, quer.Hoehe, quer.Oben, quer.Unten, Innenraum.FigurHoehe);
            Assert.Equal((Kamera.AbstandMin, Kamera.AbstandMax), k.Bereich);
        }
        Assert.False(k.MessungLaeuft);
        Assert.Equal(SpielKamera.InnenAbstandMin(quer.Hoehe, quer.Oben, quer.Unten, 58f, 40f, Innenraum.FigurHoehe), k.Bereich.Min);
    }

    [Fact]
    public void Drinnen_StartWieBisher_UndWerkzeugOhneIch()
    {
        // Standardabstand beim Betreten unveraendert (0,85 x groesste Seite, 12 bis 28), Neigung 58, Mitte des Raums
        var k = SpielKamera.Drinnen(30, 20, true);
        Assert.Equal(Math.Clamp(30 * 0.85f, 12f, 28f), k.Abstand);
        Assert.Equal(58f, k.Neigung);
        Assert.Equal((15f, 10f), (k.ZielX, k.ZielZ));
        Assert.Equal((Kamera.AbstandMin, Kamera.AbstandMax), k.Bereich);
        Assert.False(SpielKamera.Drinnen(30, 20, false).IchErlaubt);
    }
}

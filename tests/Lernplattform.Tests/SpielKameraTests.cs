using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nicos Antworten P12-257 Abschnitt 2): feste Kamera draussen im Spiel. Die Tests pruefen
/// Nicos Ziel ueber dieselbe Rechnung wie die Spielflaeche (<see cref="Kamerabedienung"/>): Abstand 8, Rest-Zoom nur 7 bis 9
/// nach beliebig vielen Schritten mit Mausrad, +/−, zwei Fingern und Drehen; Neigung nie ausserhalb 35 bis 70; draussen kein
/// Verschieben (Ziehen, ein Finger, Pfeiltasten); drinnen (ohne <c>fest</c>) alles wie bis A 0.79.0. Gegenproben: eine Kamera
/// ohne Bereich (Werkzeug, bis A 0.79.0) verlaesst beide Bereiche.
/// </summary>
public sealed class SpielKameraTests
{
    private static Kamera Draussen() => SpielKamera.Draussen(20, 20, 64, 64);

    private static bool ImBereich(Kamera k) =>
        k.Abstand >= SpielKamera.AbstandMin && k.Abstand <= SpielKamera.AbstandMax
        && k.Neigung >= SpielKamera.NeigungMin && k.Neigung <= SpielKamera.NeigungMax;

    [Fact]
    public void Start_Abstand8_Neigung52()
    {
        var k = Draussen();
        Assert.Equal(8f, k.Abstand);
        Assert.Equal(52f, k.Neigung);
        Assert.Equal((7f, 9f), k.Bereich);
        Assert.Equal((35f, 70f), k.NeigungBereich);
        Assert.Equal((0f, 0f, 64f, 64f), k.Grenzen);
    }

    [Fact]
    public void ZufaelligeBedienung_BleibtImmerImBereich()
    {
        // viele zufaellige Schritte aller Bedienwege (fester Startwert: immer dieselbe Folge)
        var zufall = new Random(268);
        var k = Draussen();
        string[] tasten = [Tastenbelegung.Naeher, Tastenbelegung.Weiter, Tastenbelegung.DrehLinks, Tastenbelegung.DrehRechts,
            Tastenbelegung.Steiler, Tastenbelegung.Flacher, Tastenbelegung.Links, Tastenbelegung.Hoch];
        for (var i = 0; i < 5000; i++)
        {
            switch (zufall.Next(4))
            {
                case 0:
                    Kamerabedienung.Mausrad(k, (zufall.NextDouble() * 20) - 10);
                    break;
                case 1:
                    Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: (zufall.NextDouble() * 800) - 400, Grad: (float)(zufall.NextDouble() * 90) - 45));
                    break;
                case 2:
                    Kamerabedienung.ZweiFinger(k, true, 0.2 + (zufall.NextDouble() * 5), (zufall.NextDouble() * 60) - 30, (zufall.NextDouble() * 600) - 300);
                    break;
                default:
                    Kamerabedienung.Taste(k, true, tasten[zufall.Next(tasten.Length)], 800);
                    break;
            }
            Assert.True(ImBereich(k), $"Schritt {i}: Abstand {k.Abstand}, Neigung {k.Neigung}");
        }
    }

    [Fact]
    public void RestZoom_ErreichtGenau7Und9()
    {
        var k = Draussen();
        Kamerabedienung.Mausrad(k, -50);
        Assert.Equal(9f, k.Abstand);
        Kamerabedienung.Mausrad(k, 50);
        Assert.Equal(7f, k.Abstand);
        Kamerabedienung.ZweiFinger(k, true, 0.01, 0, 0);
        Assert.Equal(9f, k.Abstand);
        for (var i = 0; i < 10; i++)
        {
            Assert.True(Kamerabedienung.Taste(k, true, Tastenbelegung.Naeher, 800));
        }
        Assert.Equal(7f, k.Abstand);
    }

    [Fact]
    public void RechteMaus_DrehtUndNeigt_NachObenSteiler()
    {
        var k = Draussen();
        Assert.True(Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: -40, Grad: 10)));
        Assert.Equal(10f, k.Drehung);
        Assert.Equal(52f + (40 * SpielKamera.NeigenJePunkt), k.Neigung);
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: -10000));
        Assert.Equal(70f, k.Neigung);
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: 10000));
        Assert.Equal(35f, k.Neigung);
        // ein Ziehen-Schritt (linke Taste) ist keine Drehung
        Assert.False(Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Ziehen, 5, 5)));
    }

    [Fact]
    public void Draussen_KeinVerschieben()
    {
        var k = Draussen();
        Assert.False(Kamerabedienung.Ziehen(k, true, 300, 200, 800));
        foreach (var t in new[] { Tastenbelegung.Links, Tastenbelegung.Rechts, Tastenbelegung.Hoch, Tastenbelegung.Runter })
        {
            Assert.False(Kamerabedienung.Taste(k, true, t, 800));
        }
        Assert.Equal((20f, 20f), (k.ZielX, k.ZielZ));
    }

    [Fact]
    public void Drinnen_WieBisher_FreiUndOhneNeigen()
    {
        // Innenraum-Kamera wie StadtFlaeche.InnenNeu (Abstand nach Raumgroesse, Neigung 58), Bedienung ohne fest
        var k = new Kamera(5, 5, 14) { Neigung = 58f, Grenzen = (0, 0, 10, 10) };
        Assert.True(Kamerabedienung.Ziehen(k, false, 100, 0, 800));
        Assert.NotEqual(5f, k.ZielX);
        Assert.True(Kamerabedienung.Taste(k, false, Tastenbelegung.Hoch, 800));
        Kamerabedienung.Mausrad(k, -10);
        Assert.True(k.Abstand > 30f);
        Kamerabedienung.RechteMaus(k, false, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: -400, Grad: 5));
        Kamerabedienung.ZweiFinger(k, false, 1, 0, -400);
        Assert.Equal(58f, k.Neigung);
        Assert.False(Kamerabedienung.Taste(k, false, Tastenbelegung.Steiler, 800));
        Assert.Equal(58f, k.Neigung);
    }

    [Fact]
    public void Gegenprobe_KameraOhneBereich_VerlaesstBeides()
    {
        var k = new Kamera(20, 20, SpielKamera.Abstand);
        Kamerabedienung.Mausrad(k, -10);
        Assert.True(k.Abstand > SpielKamera.AbstandMax);
        k.Neigen(30);
        Assert.True(k.Neigung > SpielKamera.NeigungMax);
        Assert.False(ImBereich(k));
    }

    [Fact]
    public void MessungFrei_OeffnetUndSchliesstDenBereich()
    {
        var k = Draussen();
        using (k.MessungFrei())
        {
            k.Zoomen(k.Abstand / Kamera.AbstandMax);
            Assert.Equal(Kamera.AbstandMax, k.Abstand);
        }
        Assert.Equal((7f, 9f), k.Bereich);
        Assert.Equal(9f, k.Abstand);
    }

    [Fact]
    public void Werkzeug_NeigungsgrenzenUnveraendert()
    {
        // das Stadtbau-Werkzeug kippt 25 bis 85 Grad: ohne Bereich bleibt jede dieser Neigungen stehen
        var k = new Kamera(0, 0, 20);
        k.Neigung = 25f;
        Assert.Equal(25f, k.Neigung);
        k.Neigung = 85f;
        Assert.Equal(85f, k.Neigung);
        k.Zoomen(0.01f);
        Assert.Equal(Kamera.AbstandMax, k.Abstand);
    }

    [Fact]
    public void Karte_JederOrtGenauEinmal_TrefferUndFreieStelle()
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", new Lernplattform.Core.Daten.PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        var spiel = new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
        var karte = new StadtKarte(spiel.Szene, new HashSet<string>(StringComparer.Ordinal) { spiel.Szene.Orte[0].Id });
        Assert.Equal(spiel.Szene.Orte.Select(o => o.Id), karte.Orte.Select(o => o.Id));
        Assert.Equal(Enumerable.Range(1, karte.Orte.Count), karte.Orte.Select(o => o.Nr));
        Assert.True(karte.Orte[0].Aufgabe);
        Assert.All(karte.Orte.Skip(1), o => Assert.False(o.Aufgabe));
        var s = karte.Massstab(400, 400);
        foreach (var o in karte.Orte)
        {
            // die eigene Marke trifft genau diesen Ort (oder einen mit derselben Mitte, wenn zwei uebereinander liegen)
            var t = karte.Treffer(StadtKarte.Bild(o.Mitte, s), s);
            Assert.NotNull(t);
            Assert.Equal(o.Mitte, t!.Mitte);
        }
        // Gegenprobe: eine Stelle weit weg von jeder Marke und jedem Haus eines Orts trifft nichts
        var frei = Enumerable.Range(0, 80).SelectMany(i => Enumerable.Range(0, 80).Select(j => new Vector2(i * 5f, j * 5f)))
            .First(p => karte.Orte.All(o => Vector2.Distance(StadtKarte.Bild(o.Mitte, s), p) > StadtKarte.TreffRadius + 5
                && !(p.X / s >= o.Min.X && p.X / s <= o.Max.X && p.Y / s >= o.Min.Y && p.Y / s <= o.Max.Y)));
        Assert.Null(karte.Treffer(frei, s));
    }
}

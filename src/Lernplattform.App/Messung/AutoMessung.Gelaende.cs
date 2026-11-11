using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.66.0 (Weisung P12-192): die Stadt bekommt im Start-Test einen Abschnitt <c>gelaende</c> (nur im Speicher): ein
/// begehbarer Huegel auf der groessten freien Wiese der Fassung 0 (Messung <see cref="StadtbauPruefung.GelaendePlatz"/>).
/// Messpunkt „Spiel: Figur auf Huegel (bis zum Bild)“ (wiederholbar, 400 ms): die Figur steht oben auf der Hoehe des Gelaendes
/// (Gegenprobe am Fuss: Hoehe der Strasse). Die Kamera folgt beim Hinaufgehen ohne Sprung (je Bild hoechstens
/// <see cref="KameraSprungHoechstens"/>). Dazu die Nahansicht mit 16 Huegeln (Hoechstzahl, Radius 3, Hoehe 1,5) gegen
/// dieselbe Grenze (160 000 / 40). Layout: Figur auf dem Huegel.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>So weit darf die Hoehe des Kameraziels von einem Bild zum naechsten springen (weich nachgefuehrt).</summary>
    internal const float KameraSprungHoechstens = 0.1f;

    private static PyObjekt GelaendeHuegel(double x, double y, double radius, double hoehe) =>
        new() { { "x", x }, { "y", y }, { "radius", radius }, { "hoehe", hoehe }, { "form", "rund" } };

    /// <summary>Frisches Spiel in der Stadt mit dem Abschnitt <paramref name="gelaende"/> (nur im Speicher).</summary>
    internal static StadtSpiel MitGelaende(SpielSeite seite, PyObjekt gelaende)
    {
        var alt = seite.Spiel!;
        var daten = (PyObjekt)Py.Kopie(alt.Stadt.Daten)!;
        Py.Setzen(daten, Gelaende.Abschnitt, gelaende);
        var stadt = new Stadt(alt.Stadt.Inhalt, daten) { Fassung = alt.Stadt.Fassung };
        var spiel = SpielStart.Neu(stadt, alt.Grafik, alt.Innenraeume, alt.GeschaefteDaten, alt.Feste, Festzeit.Heute, Jahreszeiten.Heute);
        seite.SpielSetzen(spiel);
        return spiel;
    }

    /// <summary>Ein begehbarer Huegel auf der groessten freien Wiese (Mitte, Radius bis 0,1 abgerundet, Hoehe 0,8).</summary>
    internal static (PyObjekt Gelaende, float X, float Z, float Radius) GelaendeProbe(StadtSzene szene, StadtFassung fassung)
    {
        var (_, r, stelle) = StadtbauPruefung.GelaendePlatz(szene, fassung);
        var radius = MathF.Floor((float)r * 10) / 10;
        return (new PyObjekt { { "huegel", new List<object?> { GelaendeHuegel(stelle.X, stelle.Y, radius, 0.8) } } }, stelle.X, stelle.Y, radius);
    }

    private static async Task<int> GelaendeSpielAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Gelaende " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Gelaende " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel?.Stadt.Fassung is not { } fassung)
        {
            Messwerte.Notiere("  FEHLER: Spiel Gelaende: Spielseite nicht gebaut");
            return 1;
        }
        var gl = await GlAbwartenAsync(seite);
        async Task Bild()
        {
            if (gl)
            {
                await seite.Flaeche.NaechstesBildAsync();
            }
            await ansicht.BildFertigAsync();
        }
        var vorher = seite.Spiel;
        try
        {
            var (gelaende, hx, hz, radius) = GelaendeProbe(vorher.Szene, fassung);
            Pruefe("freie Wiese", radius >= Gelaende.RadiusMin, string.Create(inv, $"groesster Huegel Radius {radius} bei ({hx}; {hz})"));
            var spiel = MitGelaende(seite, gelaende);
            await Bild();
            var fuss = (X: hx, Z: hz + radius + 0.6f);
            spiel.GeheZu(fuss.X, fuss.Z);
            spiel.SofortAnkommen();
            await Bild();
            // Gegenprobe am Fuss: die Figur steht auf der Hoehe der Strasse
            Pruefe("Figur am Fuss", MathF.Abs(StadtFlaeche.FigurHoehe(spiel) - StadtSzene.Hoehe.Figur) < 0.001f,
                StadtFlaeche.FigurHoehe(spiel).ToString("0.###", inv));

            // 1. Figur auf den Huegel (wiederholbar: hinauf, dann wieder an den Fuss)
            async Task<double?> Hinauf()
            {
                var u = Stopwatch.StartNew();
                spiel.GeheZu(hx, hz);
                spiel.SofortAnkommen();
                await Bild();
                return u.Elapsed.TotalMilliseconds;
            }
            async Task Hinab()
            {
                spiel.GeheZu(fuss.X, fuss.Z);
                spiel.SofortAnkommen();
                await Bild();
            }
            var erstes = await Hinauf();
            var oben = spiel.Szene.BodenHoehe(spiel.FigurX, spiel.FigurZ);
            Pruefe("Figur oben", oben > 0.5f && MathF.Abs(StadtFlaeche.FigurHoehe(spiel) - oben) < 0.001f,
                string.Create(inv, $"Figur {StadtFlaeche.FigurHoehe(spiel):0.###}, Boden {oben:0.###} bei ({spiel.FigurX:0.##}; {spiel.FigurZ:0.##})"));
            funde += Ergebnis("Spiel: Figur auf Huegel (bis zum Bild)",
                await Messwiederholung.MessenAsync(erstes, Geraetegrenze, Hinauf, Hinab, Task.Delay));

            // 2. Kamera folgt beim Hinaufgehen weich (kein Sprung von Bild zu Bild)
            if (gl && seite.Flaeche.AktuelleKamera is { } kamera)
            {
                await Hinab();
                seite.Flaeche.KameraAufFigur();
                spiel.GeheZu(hx, hz);
                var (sprung, letzte, bilder) = (0f, kamera.ZielY, 0);
                var uhr = Stopwatch.StartNew();
                while (uhr.Elapsed.TotalSeconds < 8 && (spiel.Geht || MathF.Abs(kamera.ZielY - StadtFlaeche.KameraZielHoehe(spiel)) > 0.02f))
                {
                    await seite.Flaeche.NaechstesBildAsync();
                    sprung = MathF.Max(sprung, MathF.Abs(kamera.ZielY - letzte));
                    letzte = kamera.ZielY;
                    bilder++;
                }
                Pruefe("Kamera folgt ohne Sprung", sprung <= KameraSprungHoechstens && MathF.Abs(kamera.ZielY - StadtFlaeche.KameraZielHoehe(spiel)) <= 0.02f,
                    string.Create(inv, $"groesster Sprung {sprung:0.###} in {bilder} Bildern, Ziel {kamera.ZielY:0.###} von {StadtFlaeche.KameraZielHoehe(spiel):0.###}"));
            }
            else
            {
                Messwerte.Notiere("Spiel: Gelaende Kamera folgt nicht geprueft (ohne 3D)");
            }

            // 3. Nahansicht im teuersten Fall: 16 Huegel (Radius 3, Hoehe 1,5), alle Orte, Weihnachten, Winter, nachts
            if (gl)
            {
                var liste = new List<object?>();
                for (var i = 0; i < Gelaende.HuegelHoechstens; i++)
                {
                    liste.Add(GelaendeHuegel(5 + (i % 4 * 7), 5 + (i / 4 * 7), Gelaende.RadiusMax, Gelaende.HoeheMax));
                }
                Tageslicht.FesteUhr = 1260;
                Jahreszeiten.Probe(Jahreszeit.Winter);
                Festzeit.Probe(Festzeit.Weihnachten);
                try
                {
                    var mit = MitGelaende(seite, new PyObjekt { { "huegel", liste } });
                    var alle = SpielMitAllenOrten(mit);
                    seite.SpielSetzen(alle);
                    await seite.Flaeche.NaechstesBildAsync();
                    Pruefe("16 Huegel im Netz", alle.Szene.Gelaende?.Huegel.Count == Gelaende.HuegelHoechstens,
                        (alle.Szene.Gelaende?.Huegel.Count ?? 0).ToString(inv));
                    Messwerte.Notiere("Spiel: Nahansicht mit allen Orten und 16 Huegeln (Weihnachten, Winter, nachts)");
                    funde += await NahansichtAsync(seite);
                }
                finally
                {
                    Tageslicht.FesteUhr = null;
                    Jahreszeiten.ProbeEnde();
                    Festzeit.ProbeEnde();
                }
            }
            else
            {
                Messwerte.Notiere("Spiel: Nahansicht mit Gelaende nicht gemessen (ohne 3D)");
            }
        }
        finally
        {
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Layout: die Figur oben auf dem Huegel.</summary>
    private static async IAsyncEnumerable<string> GelaendeAnsichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel?.Stadt.Fassung is not { } fassung)
        {
            P7W1AnsichtFehler.Add("spiel/huegel: Spielseite nicht gebaut");
            yield break;
        }
        var vorher = seite.Spiel;
        var (gelaende, hx, hz, _) = GelaendeProbe(vorher.Szene, fassung);
        var spiel = MitGelaende(seite, gelaende);
        spiel.GeheZu(hx, hz);
        spiel.SofortAnkommen();
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/huegel"));
        yield return "spiel/huegel";
        seite.SpielSetzen(vorher);
        await ansicht.BildFertigAsync();
    }
}

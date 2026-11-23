using System.Numerics;
using System.Text.Json.Nodes;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.97.0: Umsehen mit dem rechten Stick (Sammelliste 393, Weisung P12-313) und die Schalter „Umsehen umkehren“
/// (Sammelliste 394, Weisung P12-314). Abnahmesaetze: „Wer den rechten Stick nach rechts schiebt, sieht danach, was vorher
/// rechts von der Blickrichtung lag.“ und „Wer einen Schalter einschaltet, bekommt genau diese Achse umgekehrt, die andere
/// bleibt; aus = Richtung wie P12-313.“ Die Tests nehmen dieselbe Rechnung wie die Spielflaeche (<see cref="Sticks.Drehen"/>,
/// <see cref="Kamerabedienung"/>) und pruefen die Blickrichtung der Kamera selbst, nicht nur, ob sich etwas dreht.
/// </summary>
public sealed class SpielUmsehenTests
{
    private static readonly Sticks.Bereich Handy = new(384, 832, 80, 300);

    /// <summary>Kamera hinter der Figur (fest) oder in der Ich-Sicht.</summary>
    private static Kamera NeueKamera(bool ich, float drehung)
    {
        var k = SpielKamera.Draussen(5, 5, 20, 20);
        k.Drehung = drehung;
        if (ich)
        {
            k.IchZiel = 1f;
            k.IchAnteil = 1f;
        }
        return k;
    }

    /// <summary>Waagrechte Blickrichtung (x, z) der Kamera: ohne Ich-Sicht vom Auge zum Ziel, mit Ich-Sicht <see cref="Kamera.IchBlick"/>.</summary>
    private static Vector2 Blick(Kamera k)
    {
        var v = SpielKamera.ImIch(k) ? k.IchBlick : k.Ziel - k.Auge;
        return Vector2.Normalize(new Vector2(v.X, v.Z));
    }

    /// <summary>Rechts von der Blickrichtung am Boden (Bildschirm rechts, y nach oben).</summary>
    private static Vector2 Rechts(Vector2 blick) => new(-blick.Y, blick.X);

    /// <summary>Senkrechter Anteil der Blickrichtung (groesser = hoeher).</summary>
    private static float Hoehe(Kamera k) => SpielKamera.ImIch(k) ? k.IchBlick.Y : Vector3.Normalize(k.Ziel - k.Auge).Y;

    /// <summary>Rechten Stick um (dx, dy) Punkte aus der Mitte ziehen und ein Bild von 0,2 s lang wirken lassen.</summary>
    private static void StickZiehen(Kamera k, double dx, double dy, bool altesVorzeichen = false)
    {
        var sticks = new Sticks();
        Assert.True(sticks.Druecken(7, 300, 450, Handy));
        sticks.Bewegen(7, 300 + dx, 450 + dy);
        var (wx, wy) = sticks.Drehen(0.2);
        if (altesVorzeichen)
        {
            wx = -wx;     // so rechnete A 0.96.0 (wie die Maus)
        }
        // dieselbe Rechnung wie StadtFlaeche.SticksTakt
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: wy, Grad: (float)wx * Kameragriff.GradJePunkt));
    }

    /// <summary>Stick waagrecht: nach rechts = was vorher rechts lag, liegt jetzt mehr vorn (Skalarprodukt mit dem neuen Blick steigt).</summary>
    private static (bool NachRechts, bool NachLinks) Waagrecht(bool ich, float drehung, bool altesVorzeichen = false)
    {
        var k = NeueKamera(ich, drehung);
        var vorher = Blick(k);
        var rechts = Rechts(vorher);
        StickZiehen(k, Sticks.Radius, 0, altesVorzeichen);
        var nachRechts = Vector2.Dot(Blick(k), rechts) > 0.05f;
        k = NeueKamera(ich, drehung);
        StickZiehen(k, -Sticks.Radius, 0, altesVorzeichen);
        var nachLinks = Vector2.Dot(Blick(k), rechts) < -0.05f;
        return (nachRechts, nachLinks);
    }

    [Theory]
    [InlineData(false, 0f)]
    [InlineData(false, 137f)]
    [InlineData(true, 0f)]
    [InlineData(true, 250f)]
    public void RechterStick_NachRechts_BlickNachRechts_NachLinks_BlickNachLinks(bool ich, float drehung)
    {
        var (r, l) = Waagrecht(ich, drehung);
        Assert.True(r, "Stick nach rechts dreht den Blick nicht nach rechts");
        Assert.True(l, "Stick nach links dreht den Blick nicht nach links");
        // Gegenprobe: das Vorzeichen von A 0.96.0 dreht genau falsch herum (Fund Nico 19.11.2026)
        var (rAlt, lAlt) = Waagrecht(ich, drehung, altesVorzeichen: true);
        Assert.False(rAlt || lAlt, "Gegenprobe greift nicht: altes Vorzeichen waere auch richtig");
    }

    [Fact]
    public void RechterStick_ObenUnten_Unveraendert_UndPunktRechtsKommtInDieMitte()
    {
        // oben/unten passt laut Nico: Stick nach oben = Blick hoch (Ich-Sicht) wie in A 0.96.0, nach unten = tiefer
        var k = NeueKamera(true, 0f);
        var h0 = Hoehe(k);
        StickZiehen(k, 0, -Sticks.Radius);
        Assert.True(Hoehe(k) > h0 + 0.01f, "Ich-Sicht: Stick nach oben hebt den Blick nicht");
        k = NeueKamera(true, 0f);
        StickZiehen(k, 0, Sticks.Radius);
        Assert.True(Hoehe(k) < h0 - 0.01f, "Ich-Sicht: Stick nach unten senkt den Blick nicht");
        // ein Punkt 30 Grad rechts vom Blick liegt nach genug Drehen nach rechts in der Mitte (Abstand der Winkel sinkt)
        k = NeueKamera(true, 40f);
        var b0 = Blick(k);
        var ziel = Vector2.Normalize(b0 + (Rechts(b0) * MathF.Tan(30f * MathF.PI / 180f)));
        var winkel0 = MathF.Acos(Math.Clamp(Vector2.Dot(b0, ziel), -1f, 1f));
        StickZiehen(k, Sticks.Radius, 0);
        var winkel1 = MathF.Acos(Math.Clamp(Vector2.Dot(Blick(k), ziel), -1f, 1f));
        Assert.True(winkel1 < winkel0 - 0.05f, $"Punkt rechts kommt nicht naeher zur Mitte ({winkel0} -> {winkel1})");
    }

    [Fact]
    public void LinkerStick_LinksRechts_Stimmt_Unveraendert()
    {
        // Gehen: Stick nach rechts = Figur geht nach rechts im Bild (Pruefung nach P12-313 Punkt 3, keine Aenderung noetig)
        foreach (var drehung in new[] { 0f, 90f, 213f })
        {
            var k = NeueKamera(false, drehung);
            var rechts = Rechts(Blick(k));
            var (vor, seit) = Steuerung.AusStick(Sticks.Radius, 0, Sticks.Radius);
            var (x, z) = Steuerung.Richtung(vor, seit, k.Drehung);
            Assert.True(Vector2.Dot(new Vector2((float)x, (float)z), rechts) > 0.9f, "linker Stick nach rechts geht nicht nach rechts, Drehung " + drehung);
            (vor, seit) = Steuerung.AusStick(-Sticks.Radius, 0, Sticks.Radius);
            (x, z) = Steuerung.Richtung(vor, seit, k.Drehung);
            Assert.True(Vector2.Dot(new Vector2((float)x, (float)z), rechts) < -0.9f, "linker Stick nach links geht nicht nach links, Drehung " + drehung);
        }
    }

    /// <summary>Maus mit echter Taste ueber den <see cref="Kameragriff"/> um dx Punkte waagrecht ziehen (dieselbe Rechnung wie StadtFlaeche.MausSchritt).</summary>
    private static void MausZiehen(Kamera k, bool fest, bool rechteTaste, double dx)
    {
        var griff = new Kameragriff(8);
        griff.Druecken(rechteTaste, 400, 300);
        // in kleinen Schritten wie echte Mausereignisse (die linke Taste braucht erst die Schwelle zum Ziehen)
        for (var i = 1; i <= 20; i++)
        {
            Kamerabedienung.Maus(k, fest, griff.Bewegen(400 + (dx * i / 20), 300), 800);
        }
        griff.Loslassen(rechteTaste);
    }

    /// <summary>
    /// Welle A 1.6.0 (Sammelliste 441, Weisung P12-359, Fund Nico 23.11.2026: „am PC auch die Seiten links und rechts vertauscht
    /// bei der Maus“). Abnahmesatz: Maus nach rechts dreht den Blick nach rechts, nach links nach links, wie der rechte Stick.
    /// Draussen (feste Kamera), drinnen (frei) und in der Ich-Sicht (rechte und linke Taste); Gegenprobe A 1.5.0 dreht falsch.
    /// </summary>
    [Theory]
    [InlineData(false, true, true, 0f)]
    [InlineData(false, true, true, 137f)]
    [InlineData(false, false, true, 0f)]
    [InlineData(false, false, true, 250f)]
    [InlineData(true, true, true, 0f)]
    [InlineData(true, true, false, 0f)]
    [InlineData(true, true, false, 300f)]
    public void Maus_NachRechts_BlickNachRechts_NachLinks_BlickNachLinks_GegenprobeA150(bool ich, bool fest, bool rechteTaste, float drehung)
    {
        (bool R, bool L) Messen()
        {
            var k = NeueKamera(ich, drehung);
            var rechts = Rechts(Blick(k));
            MausZiehen(k, fest, rechteTaste, 100);
            var r = Vector2.Dot(Blick(k), rechts) > 0.05f;
            k = NeueKamera(ich, drehung);
            MausZiehen(k, fest, rechteTaste, -100);
            var l = Vector2.Dot(Blick(k), rechts) < -0.05f;
            return (r, l);
        }
        var (nachRechts, nachLinks) = Messen();
        Assert.True(nachRechts, "Maus nach rechts dreht den Blick nicht nach rechts");
        Assert.True(nachLinks, "Maus nach links dreht den Blick nicht nach links");
        Kamerabedienung.MausAltFuerTests = true;
        try
        {
            var (rAlt, lAlt) = Messen();
            Assert.False(rAlt || lAlt, "Gegenprobe greift nicht: die Maus von A 1.5.0 waere auch richtig");
        }
        finally
        {
            Kamerabedienung.MausAltFuerTests = false;
        }
    }

    /// <summary>Welle A 1.6.0 (441): Maus und rechter Stick drehen gleich herum; oben/unten der Maus bleibt (nach oben = steiler).</summary>
    [Fact]
    public void Maus_GleichWieRechterStick_ObenUntenUnveraendert()
    {
        var maus = NeueKamera(false, 30f);
        MausZiehen(maus, true, true, 50);
        var stick = NeueKamera(false, 30f);
        StickZiehen(stick, Sticks.Radius, 0);
        Assert.True(Vector2.Dot(Blick(maus), Rechts(Blick(NeueKamera(false, 30f)))) > 0.05f);
        Assert.Equal(MathF.Sign(((stick.Drehung - 30f + 540f) % 360f) - 180f), MathF.Sign(((maus.Drehung - 30f + 540f) % 360f) - 180f));
        var k = NeueKamera(false, 0f);
        var n0 = k.Neigung;
        var griff = new Kameragriff(8);
        griff.Druecken(true, 400, 300);
        Kamerabedienung.Maus(k, true, griff.Bewegen(400, 260), 800);
        Assert.True(k.Neigung > n0, "Maus nach oben macht nicht mehr steiler");
    }

    [Fact]
    public void PC_MausUndTasten_OhneSchalter_WieBisher()
    {
        // Gegenprobe PC: rechte Maustaste 100 Punkte nach rechts = +40 Grad wie bis A 0.96.0, E = +15, Q = -15, Maus nach oben = steiler
        var k = NeueKamera(false, 0f);
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Grad: 100 * Kameragriff.GradJePunkt));
        Assert.Equal(100 * Kameragriff.GradJePunkt, k.Drehung, 3);
        k = NeueKamera(false, 0f);
        Assert.True(Kamerabedienung.Taste(k, true, Tastenbelegung.DrehRechts, 800));
        Assert.Equal(15f, k.Drehung, 3);
        k = NeueKamera(false, 0f);
        Kamerabedienung.Taste(k, true, Tastenbelegung.DrehLinks, 800);
        Assert.Equal(345f, (k.Drehung + 360f) % 360f, 3);
        k = NeueKamera(false, 0f);
        var n0 = k.Neigung;
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: -20));
        Assert.True(k.Neigung > n0, "Maus nach oben macht nicht steiler");
    }

    /// <summary>Wirkung von Stick, Maus, Q/E und Neigung in einem Zustand der Schalter: (Stick rechts nach rechts, Maus-Drehung, E-Drehung, Ich-Sicht Stick oben hebt).</summary>
    private static (bool StickRechts, float Maus, float TasteE, float TasteQ, bool StickObenHebt, bool MausObenSteiler) Wirkung()
    {
        var (r, _) = Waagrecht(true, 0f);
        var k = NeueKamera(false, 0f);
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Grad: 10f));
        var maus = (k.Drehung + 540f) % 360f - 180f;
        k = NeueKamera(false, 0f);
        Kamerabedienung.Taste(k, true, Tastenbelegung.DrehRechts, 800);
        var e = (k.Drehung + 540f) % 360f - 180f;
        k = NeueKamera(false, 0f);
        Kamerabedienung.Taste(k, true, Tastenbelegung.DrehLinks, 800);
        var q = (k.Drehung + 540f) % 360f - 180f;
        k = NeueKamera(true, 0f);
        var h0 = Hoehe(k);
        StickZiehen(k, 0, -Sticks.Radius);
        var hebt = Hoehe(k) > h0;
        k = NeueKamera(false, 0f);
        var n0 = k.Neigung;
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: -20));
        return (r, maus, e, q, hebt, k.Neigung > n0);
    }

    [Fact]
    public void Schalter_KehrenGenauIhreAchseUm_AndereBleibt()
    {
        var aus = Wirkung();
        Assert.Equal((true, 10f, 15f, -15f, true, true), aus);
        using (Umsehbedienung.FuerTests(linksRechts: true, obenUnten: false))
        {
            // links/rechts an: Stick, Maus, Q und E umgekehrt, oben/unten wie aus
            Assert.Equal((false, -10f, -15f, 15f, true, true), Wirkung());
        }
        using (Umsehbedienung.FuerTests(linksRechts: false, obenUnten: true))
        {
            // oben/unten an: nur die Neigung umgekehrt, links/rechts wie aus
            Assert.Equal((true, 10f, 15f, -15f, false, false), Wirkung());
        }
        using (Umsehbedienung.FuerTests(linksRechts: true, obenUnten: true))
        {
            Assert.Equal((false, -10f, -15f, 15f, false, false), Wirkung());
        }
        // Gegenprobe: nach dem Block gilt wieder aus (AsyncLocal, nichts bleibt haengen)
        Assert.Equal(aus, Wirkung());
    }

    [Fact]
    public void Schalter_GehenUndZweiFingerBleibenUnberuehrt()
    {
        using (Umsehbedienung.FuerTests(linksRechts: true, obenUnten: true))
        {
            var (x, _) = Steuerung.Richtung(0, 1, 0);
            Assert.True(x > 0.9, "Gehen nach rechts umgekehrt");
            var k = NeueKamera(false, 0f);
            Kamerabedienung.ZweiFinger(k, true, 1, 20, 0);
            Assert.Equal(20f, k.Drehung, 3);
        }
    }

    [Fact]
    public void Einstellung_Speichern_AlteDateiOhneFelder_KaputterWert()
    {
        Assert.Equal("umsehen_lr_umkehren", Umsehbedienung.SchluesselLinksRechts);
        Assert.Equal("umsehen_ob_umkehren", Umsehbedienung.SchluesselObenUnten);
        // alte Einstellungsdatei ohne die Felder = aus
        var alt = JsonNode.Parse("""{"theme":"dark","antippen_gehen":false}""")!.AsObject();
        Assert.False(Umsehbedienung.Lesen(alt, Umsehbedienung.SchluesselLinksRechts));
        Assert.False(Umsehbedienung.Lesen(alt, Umsehbedienung.SchluesselObenUnten));
        // an speichern, nach "Neustart" (Text schreiben und neu lesen) gilt es weiter; nur diese Achse
        Umsehbedienung.Wert(alt, Umsehbedienung.SchluesselLinksRechts, true);
        var neu = JsonNode.Parse(alt.ToJsonString())!.AsObject();
        Assert.True(Umsehbedienung.Lesen(neu, Umsehbedienung.SchluesselLinksRechts));
        Assert.False(Umsehbedienung.Lesen(neu, Umsehbedienung.SchluesselObenUnten));
        Assert.False((bool)neu["antippen_gehen"]!);
        // aus = Standard: Eintrag entfernt (nie ein falsches false geschrieben)
        Umsehbedienung.Wert(neu, Umsehbedienung.SchluesselLinksRechts, false);
        Assert.False(neu.ContainsKey(Umsehbedienung.SchluesselLinksRechts));
        // kaputter Wert gilt als aus (nie still an)
        var kaputt = JsonNode.Parse("""{"umsehen_lr_umkehren":"ja","umsehen_ob_umkehren":1}""")!.AsObject();
        Assert.False(Umsehbedienung.Lesen(kaputt, Umsehbedienung.SchluesselLinksRechts));
        Assert.False(Umsehbedienung.Lesen(kaputt, Umsehbedienung.SchluesselObenUnten));
    }
}

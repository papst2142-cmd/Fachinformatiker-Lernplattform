using System.Numerics;
using System.Text.Json.Nodes;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.92.0 „Ich-Sicht“ (Weisung P12-301, Kurzplan P12-181, Nico 1a 2b 3c 4a). Abnahmesaetze und ihr Beleg hier:
/// (1) Heranzoomen fuehrt weich ueber den Schulterblick bis zur Ich-Sicht (Mausrad, Tasten, zwei Finger), Herauszoomen denselben
/// Weg zurueck bis 9 und nicht weiter; die Taste I und der Menue-Eintrag schalten (dieselbe Regel <see cref="SpielKamera.IchUmschalten"/>).
/// (2) In der Ich-Sicht sitzt das Auge im Kopf der Figur, die Figur geht dieselben Wege; zwischen Auge und Wand liegt nie die
/// nahe Ebene (Innenraeume: jede begehbare Zelle; draussen: jede Tuer); Gegenprobe: ein Auge in der Wand wird gefunden.
/// (3) Eigene Figur: ohne Kopf, mit Beinen und Koerper, Hals oben zu. (4) Drinnen: Waende 2,5 m, Decke auf dieser Hoehe.
/// Gegenproben: eine Kamera ohne Ich-Sicht (Werkzeug) kommt nie hinein; die Neigung der normalen Kamera bleibt 35 bis 70.
/// </summary>
public sealed class IchSichtTests
{
    private static Kamera Draussen() => SpielKamera.Draussen(20, 20, 64, 64);

    private static void Bis(Kamera k, int bilder = 120)
    {
        for (var i = 0; i < bilder; i++)
        {
            k.IchTakt(1f / 60);
        }
    }

    [Fact]
    public void Heranzoomen_WeichBisIchSicht_HerausBisNeunUndNichtWeiter()
    {
        var k = Draussen();
        k.IchAuge = new Vector3(20, 0.5f, 20);
        var anteil = new List<float>();
        var weite = new List<float>();
        for (var i = 0; i < 30 && !k.Ich; i++)
        {
            Kamerabedienung.Mausrad(k, 1);
            Bis(k, 10);
            anteil.Add(k.IchAnteil);
            weite.Add(Vector3.Distance(k.Auge, k.IchAuge));
            Assert.InRange(k.Abstand, SpielKamera.AbstandMin, SpielKamera.AbstandMax);
            Assert.InRange(k.Neigung, SpielKamera.NeigungMin, SpielKamera.NeigungMax);
        }
        Bis(k);
        Assert.True(k.Ich, $"nicht in der Ich-Sicht nach {anteil.Count} Rasten");
        Assert.True(anteil.Count <= 12, $"{anteil.Count} Rasten bis zur Ich-Sicht");
        // weich: der Anteil waechst nie zurueck, das Auge kommt immer naeher, und es gibt Zwischenstufen (Schulterblick)
        Assert.True(anteil.Zip(anteil.Skip(1)).All(p => p.Second >= p.First), string.Join(" ", anteil));
        Assert.True(weite.Zip(weite.Skip(1)).All(p => p.Second <= p.First + 1e-4f), string.Join(" ", weite));
        Assert.True(anteil.Count(a => a > 0 && a < 1) >= 3, string.Join(" ", anteil));
        Assert.Equal(k.IchAuge, k.Auge);
        Assert.Equal(Kamera.IchNah, k.Nah);
        // denselben Weg zurueck: Anteil faellt, dann der Abstand bis 9 und nie weiter
        var zurueck = new List<float>();
        for (var i = 0; i < 40; i++)
        {
            Kamerabedienung.Mausrad(k, -1);
            Bis(k, 10);
            zurueck.Add(k.IchAnteil);
            Assert.True(k.Abstand <= SpielKamera.AbstandMax);
        }
        Assert.True(zurueck.Zip(zurueck.Skip(1)).All(p => p.Second <= p.First), string.Join(" ", zurueck));
        Assert.Equal(0f, k.IchAnteil);
        Assert.Equal(SpielKamera.AbstandMax, k.Abstand);
        Kamerabedienung.Mausrad(k, -50);
        Assert.Equal(SpielKamera.AbstandMax, k.Abstand);
    }

    [Fact]
    public void Tasten_UndZweiFinger_FuehrenHineinUndHeraus()
    {
        var k = Draussen();
        for (var i = 0; i < 20; i++)
        {
            Assert.True(Kamerabedienung.Taste(k, true, Tastenbelegung.Naeher, 800));
            Bis(k, 5);
        }
        Bis(k);
        Assert.True(k.Ich);
        for (var i = 0; i < 30; i++)
        {
            Kamerabedienung.Taste(k, true, Tastenbelegung.Weiter, 800);
            Bis(k, 5);
        }
        Assert.Equal(0f, k.IchAnteil);
        Assert.Equal(SpielKamera.AbstandMax, k.Abstand);
        for (var i = 0; i < 20; i++)
        {
            Kamerabedienung.ZweiFinger(k, true, 1.3, 0, 0);
            Bis(k, 5);
        }
        Bis(k);
        Assert.True(k.Ich);
        Kamerabedienung.ZweiFinger(k, true, 0.01, 0, 0);
        Bis(k);
        Assert.True(k.IchAnteil < 1f);
    }

    [Fact]
    public void TasteI_UndUmschalten_AnUndAus()
    {
        var tasten = new Tastenbelegung();
        Assert.Equal(Tastenbelegung.IchSicht, tasten.AufgabeFuer("I"));
        Assert.Contains(Tastenbelegung.IchSicht, Tastenbelegung.NeuAb080);
        var k = Draussen();
        Assert.True(Kamerabedienung.Taste(k, true, Tastenbelegung.IchSicht, 800));
        Bis(k);
        Assert.True(k.Ich);
        Assert.True(SpielKamera.IchUmschalten(k));
        Bis(k);
        Assert.Equal(0f, k.IchAnteil);
        Assert.Equal(SpielKamera.Abstand, k.Abstand);
        // eine frueher selbst gewaehlte Taste I bleibt bei ihrer Aufgabe, die Ich-Sicht hat dann keine Taste (nie still verloren)
        var alt = Tastenbelegung.Lesen(JsonNode.Parse("{\"tasten\":{\"auftraege\":\"I\"}}")!.AsObject());
        Assert.Equal(Tastenbelegung.Auftraege, alt.AufgabeFuer("I"));
        Assert.Equal("keine Taste", alt.AnzeigeFuer(Tastenbelegung.IchSicht));
        Assert.DoesNotContain("oder keine Taste", SpielProbeTexte.HinweisFuer(alt), StringComparison.Ordinal);
    }

    [Fact]
    public void Umsehen_InDerIchSicht_DrehtUndNeigtImBereich()
    {
        var k = Draussen();
        SpielKamera.IchUmschalten(k);
        Bis(k);
        var vorher = (k.Abstand, k.Neigung, k.ZielX, k.ZielZ);
        // ein Finger / linke Maus sieht sich um (draussen sonst ohne Wirkung)
        Assert.True(Kamerabedienung.Ziehen(k, true, 100, 0, 800));
        Assert.Equal(100 * Kameragriff.GradJePunkt, k.Drehung, 3);
        Kamerabedienung.Ziehen(k, true, 0, -100000, 800);
        Assert.Equal(SpielKamera.IchNeigungMax, k.IchNeigung);
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: 100000));
        Assert.Equal(SpielKamera.IchNeigungMin, k.IchNeigung);
        Assert.True(Kamerabedienung.Taste(k, true, Tastenbelegung.Steiler, 800));
        Assert.Equal(SpielKamera.IchNeigungMin + SpielKamera.NeigenJeTaste, k.IchNeigung);
        // die normale Kamera bleibt unberuehrt (Abstand, Neigung, Ziel)
        Assert.Equal(vorher, (k.Abstand, k.Neigung, k.ZielX, k.ZielZ));
        // drinnen (ohne fest) ebenso
        var innen = new Kamera(5, 5, 14) { Neigung = 58f, IchErlaubt = true, IchNeigungBereich = (SpielKamera.IchNeigungMin, SpielKamera.IchNeigungMax) };
        SpielKamera.IchUmschalten(innen);
        Bis(innen);
        Assert.True(Kamerabedienung.Ziehen(innen, false, 50, 0, 800));
        Assert.Equal((5f, 5f), (innen.ZielX, innen.ZielZ));
        Assert.True(Kamerabedienung.Taste(innen, false, Tastenbelegung.Flacher, 800));
    }

    [Fact]
    public void Drinnen_ZoomFuehrtWeichHineinUndZurueckZurInnenkamera()
    {
        var k = new Kamera(5, 5, 14) { Neigung = 58f, Grenzen = (0, 0, 10, 10), IchErlaubt = true };
        Kamerabedienung.Mausrad(k, 50);
        Assert.Equal(Kamera.AbstandMin, k.Abstand);
        Assert.Equal(0f, k.IchAnteil);
        Kamerabedienung.Mausrad(k, 1);
        Assert.Equal(1f, k.IchZiel);
        Bis(k);
        Assert.True(k.Ich);
        Kamerabedienung.Mausrad(k, -1);
        Bis(k);
        Assert.Equal(0f, k.IchAnteil);
        Assert.Equal(Kamera.AbstandMin, k.Abstand);
        Assert.Equal(58f, k.Neigung);
    }

    [Fact]
    public void Gegenprobe_KameraOhneIchSicht_KommtNieHinein()
    {
        // Werkzeug und Kameras bis A 0.91.0: kein Ich (Mausrad, Tasten, Finger, Umschalten)
        var k = new Kamera(20, 20, 8);
        Kamerabedienung.Mausrad(k, 80);
        Kamerabedienung.Taste(k, false, Tastenbelegung.IchSicht, 800);
        Kamerabedienung.ZweiFinger(k, false, 50, 0, 0);
        Bis(k);
        Assert.Equal(0f, k.IchAnteil);
        Assert.False(SpielKamera.IchUmschalten(k));
        Assert.Equal(Kamera.AbstandMin, k.Abstand);
        Assert.Equal(Math.Max(0.3f, k.Abstand * 0.3f), k.Nah);
    }

    [Fact]
    public void Sicht_InDerIchSicht_SchautVomAugeInDieBlickrichtung()
    {
        var k = Draussen();
        k.IchAuge = new Vector3(10, 0.3f, 12);
        k.IchZiel = 1;
        k.IchAnteil = 1;
        k.IchNeigung = 0;
        foreach (var dreh in new[] { 0f, 90f, 180f, 270f })
        {
            k.Drehung = dreh;
            var vorn = k.IchAuge + (k.IchBlick * 5f);
            var p = k.Bildpunkt(vorn, 800, 600);
            Assert.NotNull(p);
            Assert.Equal(400, p!.Value.X, 1);
            Assert.Equal(300, p.Value.Y, 1);
            Assert.Null(k.Bildpunkt(k.IchAuge - (k.IchBlick * 5f), 800, 600));
        }
        Assert.Equal("Norden", IchSicht.Richtung(0));
        Assert.Equal("Westen", IchSicht.Richtung(90));
        Assert.Equal("Süden", IchSicht.Richtung(180));
        Assert.Equal("Osten", IchSicht.Richtung(270));
        Assert.Equal("Nordosten", IchSicht.Richtung(315));
        Assert.Equal(0f, IchSicht.DrehungZu(180f));
    }

    [Fact]
    public void EigeneFigur_OhneKopf_MitKoerperUndBeinen_HalsZu()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        foreach (var (bew, bild) in new[] { ((string?)null, -1), (Figur.Gehen, 0), (Figur.Gehen, 4), (Figur.Rennen, 2) })
        {
            var ganz = Figur.Netz(g, null, 1f, null, bew, bild);
            var ohne = Figur.OhneKopf(g, null, null, bew, bild, IchSicht.KoerperBis);
            var benutzt = ohne.Indizes.Distinct().Select(i => ohne.Ort((int)i)).ToList();
            Assert.True(benutzt.Max(p => p.Y) <= IchSicht.KoerperBis + 0.01f);
            Assert.True(benutzt.Min(p => p.Y) < 0.02f, "keine Fuesse");
            Assert.True(ohne.Indizes.Count < ganz.Indizes.Count);
            // Koerper unter dem Kopf vollstaendig (Weisung P12-302: bestimmt an der stehenden Figur): jedes Dreieck der stehenden
            // ganzen Figur unterhalb der Grenze ist in jedem Bild noch da, dazu der Deckel (12 Dreiecke)
            var stehend = Figur.Netz(g, null, 1f, null, null, -1);
            var unten = Enumerable.Range(0, stehend.Indizes.Count / 3).Count(d => Enumerable.Range(0, 3).All(e => stehend.Ort((int)stehend.Indizes[(d * 3) + e]).Y <= IchSicht.KoerperBis));
            Assert.Equal(unten + 12, ohne.Indizes.Count / 3);
            // Gegenprobe: die ganze Figur reicht mit dem Kopf ueber das Auge
            Assert.True(Enumerable.Range(0, ganz.AnzahlEcken).Max(e => ganz.Ort(e).Y) > IchSicht.AugeAnteil);
        }
        // das Auge liegt ueber dem Koerper und vor der Mitte der Figur, nie hinter ihr
        Assert.True(IchSicht.AugeAnteil > IchSicht.KoerperBis);
        Assert.True(IchSicht.AugeVor > 0f);
    }

    [Fact]
    public void Drinnen_Waende25m_DeckeDarauf()
    {
        Assert.Equal(2.5f * Innenraum.Meter, Innenraum.IchWandHoehe, 4);
        Assert.Equal(Innenraum.IchWandHoehe, Innenraum.WandHoehe * Innenraum.IchWandStreckung, 4);
        var raum = SpielWandTests.AlleRaeume(SpielInnenraumTests.Innenraeume.Value).First();
        var decke = raum.DeckeNetz();
        var ys = Enumerable.Range(0, decke.AnzahlEcken).Select(e => decke.Ort(e)).ToList();
        Assert.Equal(Innenraum.IchWandHoehe, ys.Min(p => p.Y), 4);
        Assert.True(ys.Min(p => p.X) <= 0 && ys.Max(p => p.X) >= raum.Breite && ys.Min(p => p.Z) <= 0 && ys.Max(p => p.Z) >= raum.Tiefe);
        // die Figur passt darunter (Auge unter der Decke)
        Assert.True(Innenraum.FigurHoehe < Innenraum.IchWandHoehe);
        // Huelle in einem Netz: Decke plus jedes Wandstueck gestreckt, alle Raeume; die Waende reichen genau bis an die Decke
        foreach (var r in SpielWandTests.AlleRaeume(SpielInnenraumTests.Innenraeume.Value))
        {
            var huelle = r.IchHuelle();
            var waende = r.Waende.Sum(w => r.WandNetzFertig(w).Indizes.Count);
            Assert.True(r.Waende.Count > 0);
            Assert.Equal(r.DeckeNetz().Indizes.Count + waende, huelle.Indizes.Count);
            var hoechste = r.Waende.Max(w => { var n = r.WandNetzFertig(w); return Enumerable.Range(0, n.AnzahlEcken).Max(e => n.Ort(e).Y); });
            Assert.Equal(Innenraum.IchWandHoehe, hoechste * Innenraum.IchWandStreckung, 3);
        }
    }

    /// <summary>
    /// Halbe Diagonale der nahen Ebene (Sichtfeld 40 Grad, breites Bild 2,5:1): naeher als so darf keine Wand am Auge sein, sonst
    /// schnitte die nahe Ebene sie an (man saehe hindurch).
    /// </summary>
    private static readonly float NahRadius = Kamera.IchNah * MathF.Sqrt(1 + (MathF.Pow(MathF.Tan(20f * MathF.PI / 180f), 2) * (1 + (2.5f * 2.5f))));

    private static float AbstandZumKasten(Vector2 p, Vector3 min, Vector3 max)
    {
        var dx = MathF.Max(MathF.Max(min.X - p.X, 0), p.X - max.X);
        var dz = MathF.Max(MathF.Max(min.Z - p.Y, 0), p.Y - max.Z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>Kleinster Abstand des Auges zu einer Wand, ueber jede begehbare Zelle und vier Blickrichtungen der Figur.</summary>
    internal static (float Abstand, string Wo) NaechsteWand(Innenraum raum, Func<Vector2, Vector2>? verschieben = null)
    {
        var bester = (float.MaxValue, "");
        foreach (var (spalte, zeile) in raum.Lauf.Basis)
        {
            var mitte = new Vector2((float)((spalte + 0.5) * Wege.Schritt), (float)((zeile + 0.5) * Wege.Schritt));
            foreach (var r in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                var echt = IchSicht.AugeDrinnen(raum, mitte.X, mitte.Y, r);
                var auge = (verschieben ?? (p => p))(echt);
                foreach (var w in raum.Waende)
                {
                    var a = AbstandZumKasten(auge, w.Min, w.Max);
                    if (a < bester.Item1)
                    {
                        bester = (a, $"{raum.Ansicht} Zelle {spalte}/{zeile} Richtung {r}");
                    }
                }
            }
        }
        return bester;
    }

    [Fact]
    public void Drinnen_NieEineWandAnDerNahenEbene_JedeBegehbareZelle()
    {
        var funde = new List<string>();
        var geprueft = 0;
        foreach (var (name, innen) in SpielWandTests.Fassungen())
        {
            foreach (var raum in SpielWandTests.AlleRaeume(innen).DistinctBy(r => r.Ansicht))
            {
                var (abstand, wo) = NaechsteWand(raum);
                geprueft++;
                if (abstand <= NahRadius)
                {
                    funde.Add($"{name} {wo}: {abstand}");
                }
            }
        }
        Assert.True(geprueft > 10);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(IchSicht.WandAbstand > NahRadius);
        // Gegenprobe: Figur dicht vor einer Wand (halber Augenversatz), Blick auf die Wand: ohne Zurueckruecken laege das Auge in
        // oder hinter der Wand, mit Zurueckruecken davor und weiter als WandAbstand
        var wandRaum = SpielWandTests.AlleRaeume(SpielInnenraumTests.Innenraeume.Value).First();
        var vorWeit = IchSicht.AugeVor * Innenraum.FigurHoehe;
        var w = wandRaum.Waende.First(x => x.Max.X - x.Min.X < x.Max.Z - x.Min.Z);
        var stand = new Vector2(w.Max.X + (vorWeit / 2), (w.Min.Z + w.Max.Z) / 2);
        var roh = stand + new Vector2(-vorWeit, 0);
        var echt = IchSicht.AugeDrinnen(wandRaum, stand.X, stand.Y, 270f);
        Assert.True(roh.X < w.Max.X);
        Assert.True(echt.X > w.Max.X && wandRaum.Waende.All(x => IchSicht.AbstandZurWand(echt, x) > IchSicht.WandAbstand));
    }

    /// <summary>
    /// Abnahmesatz (2): die Figur geht in der Ich-Sicht dieselben Wege wie ohne (Wegeregeln 342/347 unveraendert): zwei gleiche
    /// Spiele gehen zu denselben Orten, eines mit Ich-Sicht (umgesehen, Blick nach unten), Bild fuer Bild dieselbe Stelle, Richtung,
    /// Kopfzeile. Gegenprobe: ein drittes Spiel zu einem anderen Ort weicht ab (der Vergleich sieht Unterschiede).
    /// </summary>
    [Fact]
    public void Gehen_InDerIchSicht_DieselbenWege()
    {
        // je Spiel ein eigener Ablauf (ein gemeinsamer Ablauf haette einen gemeinsamen Spielstand)
        var (ohne, mit, anders) = (SpielMitfahrtTests.StadtNachFassung(2), SpielMitfahrtTests.StadtNachFassung(2), SpielMitfahrtTests.StadtNachFassung(2));
        Assert.False(ReferenceEquals(ohne.Ablauf, mit.Ablauf));
        var k = SpielKamera.Draussen(mit.FigurX, mit.FigurZ, mit.Szene.Breite, mit.Szene.Tiefe);
        var regeln = new IchSicht();
        Assert.True(SpielKamera.IchUmschalten(k));
        var ziele = mit.Szene.Orte.OrderByDescending(o => MathF.Abs(o.StehX - mit.FigurX) + MathF.Abs(o.StehZ - mit.FigurZ)).Select(o => o.Id).Take(4).ToList();
        var bilder = 0;
        foreach (var ziel in ziele)
        {
            Assert.Equal(ohne.GeheZu(ziel), mit.GeheZu(ziel));
            Assert.True(mit.Geht, ziel);
            anders.GeheZu(ziele[(ziele.IndexOf(ziel) + 1) % ziele.Count]);
            var abweichung = false;
            for (var i = 0; i < 60 * 120 && (ohne.Geht || mit.Geht); i++)
            {
                ohne.Fortschreiten(SpielMitfahrtTests.Dt);
                mit.Fortschreiten(SpielMitfahrtTests.Dt);
                anders.Fortschreiten(SpielMitfahrtTests.Dt);
                regeln.Takt(k, mit, (float)SpielMitfahrtTests.Dt);
                if (i % 50 == 0)
                {
                    SpielKamera.Neigen(k, 10f);
                    k.Drehen(17f);
                }
                Assert.Equal((ohne.FigurX, ohne.FigurZ, ohne.FigurRichtung, ohne.Geht, ohne.Kopfzeile), (mit.FigurX, mit.FigurZ, mit.FigurRichtung, mit.Geht, mit.Kopfzeile));
                abweichung |= (anders.FigurX, anders.FigurZ) != (mit.FigurX, mit.FigurZ);
                bilder++;
            }
            Assert.False(mit.Geht);
            Assert.True(abweichung);
            anders.SofortAnkommen();
            anders.GeheZu(ziel);
            anders.SofortAnkommen();
        }
        Assert.True(k.Ich && bilder > 100);
    }

    /// <summary>
    /// Lauf 1069 (Windows): nach einer Busfahrt in der Ich-Sicht nahm ein neues Spiel auf derselben Flaeche die Pause mit und
    /// begann in der Ich-Sicht. Ein neues Spiel (<see cref="IchSicht.Neu"/>) beginnt immer ohne; Gegenprobe ohne Neu: die
    /// Ich-Sicht kommt mit.
    /// </summary>
    [Fact]
    public void NeuesSpiel_BeginntOhneIchSicht_AuchNachPauseImBus()
    {
        float Anteil(bool neu)
        {
            var s = SpielMitfahrtTests.StadtNachFassung(2);
            var regeln = new IchSicht();
            var alt = SpielKamera.Draussen(s.FigurX, s.FigurZ, s.Szene.Breite, s.Szene.Tiefe);
            Assert.True(SpielKamera.IchUmschalten(alt));
            for (var i = 0; i < 120; i++)
            {
                regeln.Takt(alt, s, 1f / 60);
            }
            Assert.Equal(1f, alt.IchAnteil);
            regeln.Takt(alt, s, 1f / 60, busFaehrtFigur: true);
            Assert.True(regeln.Pausiert);
            if (neu)
            {
                regeln.Neu();
            }
            var frisch = SpielMitfahrtTests.StadtNachFassung(2);
            var k = SpielKamera.Draussen(frisch.FigurX, frisch.FigurZ, frisch.Szene.Breite, frisch.Szene.Tiefe);
            for (var i = 0; i < 120; i++)
            {
                regeln.Takt(k, frisch, 1f / 60);
            }
            return k.IchAnteil;
        }
        Assert.Equal(0f, Anteil(neu: true));
        Assert.True(Anteil(neu: false) > 0.5f);
    }

    [Fact]
    public void Gegenprobe_AugeInDerWand_WirdGefunden()
    {
        var raum = SpielWandTests.AlleRaeume(SpielInnenraumTests.Innenraeume.Value).First();
        var w = raum.Waende[0];
        var mitte = new Vector2((w.Min.X + w.Max.X) / 2, (w.Min.Z + w.Max.Z) / 2);
        var (abstand, _) = NaechsteWand(raum, _ => mitte);
        Assert.True(abstand <= NahRadius);
    }

    [Fact]
    public void Draussen_AnJederTuer_KeinHausAnDerNahenEbene()
    {
        var (spiel, _) = (SpielMitfahrtTests.Stadt1(), 0);
        var s = new StadtSpiel(spiel.Stadt, spiel.Ablauf, SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
        var funde = new List<string>();
        foreach (var ort in s.Szene.Orte)
        {
            s.GeheZu(ort.Id);
            s.SofortAnkommen();
            var auge = IchSicht.Auge(s);
            Assert.Equal(new Vector2(s.FigurX, s.FigurZ), new Vector2(auge.X, auge.Z) - (new Vector2(MathF.Sin(s.FigurRichtung * MathF.PI / 180f), MathF.Cos(s.FigurRichtung * MathF.PI / 180f)) * (IchSicht.AugeVor * Figur.StadtHoehe)), new Vector2Vergleich());
            foreach (var anderer in s.Szene.Orte)
            {
                var a = AbstandZumKasten(new Vector2(auge.X, auge.Z), anderer.KastenMin, anderer.KastenMax);
                if (a <= NahRadius && auge.Y < anderer.KastenMax.Y)
                {
                    funde.Add($"an {ort.Id}: Haus {anderer.Id} {a}");
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    private sealed class Vector2Vergleich : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 1e-4f;

        public int GetHashCode(Vector2 v) => 0;
    }
}

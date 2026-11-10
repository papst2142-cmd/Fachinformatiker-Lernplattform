using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 276 (Weisung P12-164, Kurzplan P12-99 Punkt 2, Nico: „die Markierung ist nicht dort, wo sie sein sollte“): je
/// Tuer des Modells genau ein Ziel, die Tuergruppe (<see cref="StadtTueren.Tuergruppen"/>: Glastuer bzw. Tuerblatt bis zum Boden
/// oder dunkle Tuer, ohne Laibung, Rahmen und Schaufenster). Marke und Tuer liegen auf ihrer Mitte, gleich ob links oder rechts
/// neben die Tuer getippt wird, und das Spiel zeichnet genau dort die Tuer des Modells. Geprueft fuer alle Katalog-Gebaeude, die
/// „Als Gebäude übernehmen“ erlaubt, und alle Orte der Stadt 1. Gegenprobe: alte Ziele (jede dunkle Gruppe) treffen bei Nicos
/// Faellen (Glastuer im Rahmen der Bitweiche, Wohnung mit Laden) die Laibung statt der Tuer.
/// </summary>
public class TuerZielTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Marke auf der Tuermitte bis so weit (Kurzplan P12-99).</summary>
    private const float Genau = 0.05f;

    private static AussenStelle Ort(string id) => new(AussenArt.Haus, -1, 0, id);

    /// <summary>Die Gebaeude des Katalogs (Haeuser, Geschaefte, Hallen, Tuerme der fuenf Pakete mit Gebaeuden).</summary>
    public static IEnumerable<KatalogEintrag> KatalogGebaeude() => Katalog.Aktiv!.Eintraege.Where(e => e.Art == KatalogArt.Modell && e.Gruppe == "Häuser und Bauteile"
        && (e.Name.StartsWith("building", StringComparison.Ordinal) || e.Name.StartsWith("low-detail-building", StringComparison.Ordinal) || e.Name.StartsWith("house_type", StringComparison.Ordinal)));

    private static (Projekt P, AussenStelle G) AlsGebaeude(string id)
    {
        var p = Projekt.Neu("Ziel", D, Daten.NeueAblage(), leer: (40, 40));
        p.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 2.0, 30.0 }, new List<object?> { 38.0, 30.0 } } }, { "gehweg", 0.35 },
        }));
        var s = p.AussenAendern("Stueck", a => AussenBearbeitung.Setzen(a, AussenArt.Katalog, 20, 20, raster: false, modell: id)!);
        var g = p.AussenAendern("Als Gebaeude", a => AussenBearbeitung.AlsGebaeude(a, s, out _, out _));
        Assert.NotNull(g);
        return (p, g!);
    }

    /// <summary>Jedes Ziel einer Fassade: Tipp links, mittig und rechts daneben (mit und ohne Einrasten) gibt genau das Ziel.</summary>
    private static int ZielePruefen(TuerFassade f, string wer, bool streng = true)
    {
        var zahl = 0;
        for (var w = 0; w < f.Waende.Count; w++)
        {
            var wand = f.Waende[w];
            var ziele = f.TuerZiele(w);
            foreach (var (t, _) in ziele)
            {
                zahl++;
                // daneben tippen: bis 0,2, hoechstens knapp vor die Haelfte zum naechsten Ziel (zwei Tuerteile nebeneinander)
                var naechstes = ziele.Where(z => z.T != t).Select(z => MathF.Abs(z.T - t)).DefaultIfEmpty(1f).Min();
                var weit = MathF.Min(0.2f, naechstes * 0.45f);
                foreach (var tipp in new[] { -weit, 0f, weit })
                {
                    foreach (var einrasten in new[] { true, false })
                    {
                        if (!einrasten && tipp != 0)
                        {
                            continue;
                        }
                        var p = wand.Bei(Math.Clamp(t + tipp, 0, wand.Laenge)) + (wand.Aussen * 0.1f);
                        var platz = f.Finden(p, w, einrasten);
                        var tt = Vector2.Dot(platz.Punkt - wand.A, wand.Richtung);
                        if (!streng && !einrasten && platz.Art != TuerFassade.Stelle.Modelltuer)
                        {
                            // Tuer wie in der Ausgangsstadt (kein Tuerteil des Modells): ohne Einrasten bleibt die glatte Stelle
                            continue;
                        }
                        Assert.True(platz.Wand == w && MathF.Abs(tt - t) <= Genau && platz.Art == TuerFassade.Stelle.Modelltuer,
                            $"{wer} Wand {w}: Ziel {t:F3}, Tipp {tipp} (Einrasten {einrasten}) gibt {platz}");
                    }
                }
            }
        }
        return zahl;
    }

    [Fact]
    public void KatalogGebaeude_JedeTuer_EinZiel_MarkeAufDerMitte_SpielZeichntDort()
    {
        var gebaeude = KatalogGebaeude().ToList();
        Assert.True(gebaeude.Count >= 90, $"nur {gebaeude.Count} Katalog-Gebaeude");
        var mitZiel = 0;
        foreach (var e in gebaeude)
        {
            var (p, g) = AlsGebaeude(e.Id);
            var f = TuerFassade.Rechnen(p, p.AussenStand, g);
            Assert.NotNull(f);
            if (ZielePruefen(f!, e.Id) == 0)
            {
                continue;
            }
            mitZiel++;
            // das erste Ziel setzen: das Spiel zeichnet die Tuer des Modells genau an der Marke (keine zweite Tuer)
            var w = Enumerable.Range(0, f!.Waende.Count).First(i => f.TuerZiele(i).Count > 0);
            var (t, gez) = f.TuerZiele(w)[0];
            var platz = f.Finden(f.Waende[w].Bei(t), w, einrasten: true);
            p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, g, platz));
            var szene = Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene;
            var modell = Py.Str(Objekt(Aussen.Landschaft(p.AussenStand)[g.Nummer])["modell"]);
            Assert.Contains(szene.Tueren, d => d.Ort == modell && d.Modell && Vector2.Distance(d.Mitte, gez) <= Genau);
        }
        Assert.True(mitZiel >= 30, $"nur {mitZiel} Katalog-Gebaeude mit Tuer des Modells");
    }

    [Fact]
    public void Stadt1_JedeTuerDesModells_EinZiel()
    {
        var p = Projekt.Neu("Ziele Stadt 1", D, Daten.NeueAblage());
        var orte = (Get(p.Stand().Karte, "orte") as List<object?> ?? []).OfType<PyObjekt>().Select(o => Py.Str(o["id"])).ToList();
        var zahl = 0;
        foreach (var id in orte)
        {
            if (TuerSetzen.Nicht(p.AussenStand, Ort(id)) is not null || TuerFassade.Rechnen(p, p.AussenStand, Ort(id)) is not { } f)
            {
                continue;
            }
            zahl += ZielePruefen(f, id, streng: false);
        }
        Assert.True(zahl >= 5, $"nur {zahl} Ziele in Stadt 1");
    }

    /// <summary>
    /// Nicos Fall (Bild 3, „Kaufhaus“ = Bitweiche-Modell mit Glas-Doppeltuer zwischen zwei dunklen Laibungen): die Marke liegt
    /// auf der Mitte der Glastuer, nicht auf einer Laibung. Gegenprobe alte Ziele: Tipp links bzw. rechts landet auf der Laibung.
    /// </summary>
    [Fact]
    public void Bitweiche_GlastuerImRahmen_ZielAufDerGlastuer_GegenprobeAltAufDerLaibung()
    {
        var p = Projekt.Neu("Bitweiche", D, Daten.NeueAblage());
        var f = TuerFassade.Rechnen(p, p.AussenStand, Ort("bitweiche"))!;
        var szene = p.SzeneOhneLaternen(TuerFassade.StandOhneVersatz(p.AussenStand, Ort("bitweiche")), mitGebaeuden: true)!;
        var haus = szene.Haeuser.First(h => h.Id == "bitweiche");
        Func<int, GrafikModell?> an = i => szene.Grafik!.Modell(szene.Stuecke[i].Name);
        var gefunden = false;
        for (var w = 0; w < f.Waende.Count; w++)
        {
            var wand = f.Waende[w];
            var mitte = wand.Bei(wand.Laenge / 2);
            var quer = new Vector2(-wand.Aussen.Y, wand.Aussen.X);
            var vz = Vector2.Dot(quer, wand.Richtung);
            var gruppen = StadtTueren.ModellTueren(szene.Stuecke, haus.StueckeVon, haus.StueckeBis, an, mitte, wand.Aussen, weite: wand.Laenge / 2, tief: 0.6f, glas: true);
            foreach (var glas in gruppen.Where(g => an(g.Stueck) is { } m && StadtTueren.IstGlas(m.Farbe(g.Dreiecke[0]))))
            {
                var laibungen = gruppen.Where(d => d != glas && MathF.Max(d.A1 - glas.A2, glas.A1 - d.A2) <= StadtTueren.RahmenAbstand && d.Tiefe < glas.Tiefe).ToList();
                if (laibungen.Count < 2)
                {
                    continue;
                }
                gefunden = true;
                var soll = (wand.Laenge / 2) + (vz * glas.Mitte);
                foreach (var l in laibungen)
                {
                    var lt = (wand.Laenge / 2) + (vz * l.Mitte);
                    var platz = f.Finden(wand.Bei(lt) + (wand.Aussen * 0.1f), w, einrasten: true);
                    Assert.Equal(soll, Vector2.Dot(platz.Punkt - wand.A, wand.Richtung), 2);
                    TuerFassade.GruppenAltFuerTests = true;
                    try
                    {
                        var alt = TuerFassade.Rechnen(p, p.AussenStand, Ort("bitweiche"))!;
                        var platzAlt = alt.Finden(wand.Bei(lt) + (wand.Aussen * 0.1f), w, einrasten: true);
                        Assert.True(MathF.Abs(Vector2.Dot(platzAlt.Punkt - wand.A, wand.Richtung) - soll) > 0.2f, $"Gegenprobe: alte Marke {platzAlt.Punkt} liegt auf der Tuer");
                    }
                    finally
                    {
                        TuerFassade.GruppenAltFuerTests = false;
                    }
                }
            }
        }
        Assert.True(gefunden, "Bitweiche ohne Glastuer zwischen zwei Laibungen");
    }

    /// <summary>
    /// Schaufenster bis zum Boden ohne Rahmen (Kaufhaus-Galerie) sind keine Tueren; Laibung und Rahmen einer Glastuer auch nicht.
    /// </summary>
    [Fact]
    public void Tuergruppen_OhneSchaufensterUndLaibung()
    {
        var p = Projekt.Neu("Gruppen", D, Daten.NeueAblage());
        var szene = p.SzeneOhneLaternen(p.AussenStand, mitGebaeuden: true)!;
        Func<int, GrafikModell?> an = i => szene.Grafik!.Modell(szene.Stuecke[i].Name);
        foreach (var haus in szene.Haeuser.Where(h => h.StueckeBis > h.StueckeVon))
        {
            var c = new Vector2((haus.Min.X + haus.Max.X) / 2, (haus.Min.Z + haus.Max.Z) / 2);
            foreach (var aussen in new Vector2[] { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) })
            {
                var tief = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.X - haus.Min.X) / 2 : (haus.Max.Z - haus.Min.Z) / 2;
                var lang = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.Z - haus.Min.Z) / 2 : (haus.Max.X - haus.Min.X) / 2;
                var wand = c + (aussen * tief);
                var alle = StadtTueren.ModellTueren(szene.Stuecke, haus.StueckeVon, haus.StueckeBis, an, wand, aussen, weite: lang, tief: 0.6f, glas: true);
                var tueren = StadtTueren.Tuergruppen(szene.Stuecke, haus.StueckeVon, haus.StueckeBis, an, wand, aussen, weite: lang, glas: false);
                foreach (var (t, glas) in tueren)
                {
                    if (glas)
                    {
                        // Glastuer nur mit Rahmen daneben
                        Assert.Contains(alle, d => an(d.Stueck) is { } m && !StadtTueren.IstGlas(m.Farbe(d.Dreiecke[0])) && MathF.Max(d.A1 - t.A2, t.A1 - d.A2) <= StadtTueren.RahmenAbstand);
                    }
                    else
                    {
                        // keine dunkle Tuer neben einer Glastuer (das waere ihr Rahmen)
                        Assert.DoesNotContain(tueren, g => g.Glas && MathF.Max(g.Tuer.A1 - t.A2, t.A1 - g.Tuer.A2) <= StadtTueren.RahmenAbstand);
                    }
                }
                if (haus.Id == "kaufhaus")
                {
                    Assert.DoesNotContain(tueren, t => t.Glas);
                }
            }
        }
    }

    /// <summary>Fassung 0 bleibt: an keiner Tuer der Stadt 1 erkennt das Spiel jetzt eine andere eigene Tuer (Glastuer nur zusaetzlich).</summary>
    [Fact]
    public void Fassung0_TuerenUnveraendert_GegenprobeGlastuerAlt()
    {
        var p = Projekt.Neu("Fassung 0", D, Daten.NeueAblage());
        var neu = p.SzeneOhneLaternen(p.AussenStand, mitGebaeuden: true)!.Tueren.Select(t => (t.Ort, t.Mitte, t.Modell)).ToList();
        StadtTueren.GlastuerAltFuerTests = true;
        try
        {
            var alt = Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene.Tueren.Select(t => (t.Ort, t.Mitte, t.Modell)).ToList();
            var neu2 = neu;
            Assert.Equal(alt.Count, neu2.Count);
            for (var i = 0; i < alt.Count; i++)
            {
                Assert.Equal(alt[i].Ort, neu2[i].Ort);
                Assert.Equal(alt[i].Modell, neu2[i].Modell);
                Assert.True(Vector2.Distance(alt[i].Mitte, neu2[i].Mitte) < 1e-5f, $"{alt[i]} {neu2[i]}");
            }
        }
        finally
        {
            StadtTueren.GlastuerAltFuerTests = false;
        }
    }
}

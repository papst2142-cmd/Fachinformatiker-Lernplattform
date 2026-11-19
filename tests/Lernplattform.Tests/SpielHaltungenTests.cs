using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.93.0 „Haltungen“ (Weisung P12-303, Sammelliste 352, Nico 5a). Abnahmesaetze und ihre Belege:
/// <list type="number">
/// <item>Springen: die Figur springt kurz in die Hoehe, an der Stelle und im Gehen; ihr Weg ist Bild fuer Bild derselbe wie ohne
/// Sprung, sie kommt also nie ueber Hindernisse, Waende oder Fahrbahn; drinnen nur, wo ueber dem Kopf Platz ist.</item>
/// <item>Ducken und Kriechen: nur an der Stelle, Gehen beendet sie; die Figur in der Haltung beruehrt nichts (Einrichtung,
/// Fest-Deko, Personen; 0 Ecken bei 0,02 Spiel) und reicht nicht ueber die Mitte einer Wand (Gegenprobe ohne Pruefung findet Funde).</item>
/// <item>Zurueckgelehnt sitzen: auf Sofa, Sessel und Liegestuhl „Zurücklehnen“, zurueck mit „Aufrichten“ (Platzregeln: Test
/// <c>SpielHaltungTests</c> prueft jeden Platz, auch die zurueckgelehnten).</item>
/// <item>Nur Darstellung: kein Ereignis, keine Spielzeit; Modelle nur angehaengt und ueber die Tabelle <see cref="Figur.HaltungBild"/> gewaehlt.</item>
/// </list>
/// </summary>
public class SpielHaltungenTests
{
    private static List<Vector3> Ecken(Netz n) => [.. Enumerable.Range(0, n.AnzahlEcken).Select(n.Ort)];

    /// <summary>Kleidung, die am Koerper haengt (Rock, Mantel mit Kragen, Tasche) und die Kopfbedeckungen.</summary>
    private static IReadOnlyList<IReadOnlyList<Figur.Stueck>?> AlleKleider()
    {
        var stuecke = Kleidung.Alle(SpielGeschaefteTests.Daten.Value).GroupBy(k => (k.Slot, k.Form)).Select(g => new Figur.Stueck(g.Key.Slot, g.Key.Form, g.First().Farbe)).ToList();
        return [null, .. stuecke.Select(s => (IReadOnlyList<Figur.Stueck>?)[s])];
    }

    /// <summary>Wie weit eine Ecke ausserhalb der Quader liegt (0 = in einem Quader).</summary>
    private static float Ausserhalb(Vector3 p, IReadOnlyList<(float Halb, float Y1, float Y2, float Z1, float Z2)> quader) =>
        quader.Min(q => MathF.Max(MathF.Max(MathF.Abs(p.X) - q.Halb, 0f), MathF.Max(MathF.Max(q.Y1 - p.Y, p.Y - q.Y2), MathF.Max(q.Z1 - p.Z, p.Z - q.Z2))));

    [Fact]
    public void KoerperQuader_UmfassenJedeEckeDerHaltung_JedeKleidung_GegenprobeKleidungFolgtNicht()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var kleider = AlleKleider();
        Assert.True(kleider.Count > 20, "Kleidung fehlt");
        var funde = new List<string>();
        foreach (var (haltung, modell) in new[] { (Koerperhaltung.Ducken, Figur.Ducken), (Koerperhaltung.Kriechen, Figur.Kriechen), (Koerperhaltung.Stehen, Figur.Springen) })
        {
            var quader = Innenraum.KoerperQuader(haltung);
            foreach (var k in kleider)
            {
                var weit = Ecken(Figur.Netz(g, null, 1f, k, modell, 0)).Max(p => Ausserhalb(p, quader));
                if (weit > 0.001f)
                {
                    var e = Ecken(Figur.Netz(g, null, 1f, k, modell, 0)).MaxBy(p => Ausserhalb(p, quader));
                    funde.Add($"{modell} {k?[0].Form ?? "ohne"}: {weit:0.000} bei {e}");
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // Gegenprobe: Rock, Mantel, Tasche und Muetze in Stehhoehe (Stand vor A 0.93.0) ragen geduckt aus dem Quader
        Figur.FolgenAltFuerTests = true;
        try
        {
            var muetze = kleider.First(k => k?[0].Form == "muetze");
            Assert.True(Ecken(Figur.Netz(g, null, 1f, muetze, Figur.Ducken, 0)).Max(p => Ausserhalb(p, Innenraum.KoerperQuader(Koerperhaltung.Ducken))) > 0.05f);
        }
        finally
        {
            Figur.FolgenAltFuerTests = false;
        }
    }

    [Fact]
    public void Modelle_NurAngehaengt_WahlUeberDieTabelle_KopfSinkt()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var steht = g.Modell(Figur.Modell)!;
        foreach (var name in Figur.NeueHaltungen)
        {
            var m = g.Modell(Figur.BildName(name, 0));
            Assert.NotNull(m);
            Assert.Equal(steht.Dreiecke, m!.Dreiecke);
        }
        Assert.Equal(Figur.Springen, Figur.HaltungBild(Koerperhaltung.Stehen, true));
        Assert.Equal(Figur.Ducken, Figur.HaltungBild(Koerperhaltung.Ducken, false));
        Assert.Equal(Figur.Kriechen, Figur.HaltungBild(Koerperhaltung.Kriechen, false));
        Assert.Null(Figur.HaltungBild(Koerperhaltung.Stehen, false));
        Assert.Equal(Figur.SitzenZurueck, Figur.SitzBild(SitzArt.Zurueck));
        Assert.True(Figur.KopfSenkung(g, Figur.Kriechen) > 0.15f && Figur.KopfSenkung(g, Figur.Ducken) > 0.15f);
    }

    /// <summary>Alle Raeume, je Raum ein Raster von Stellen, an denen die Figur steht (Laufraster), Blick in 8 Richtungen.</summary>
    private static IEnumerable<(string Wo, Innenraum Innen, Vector2 Ort, float Blick)> Stellen()
    {
        var gesehen = new HashSet<string>();
        foreach (var (name, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume).Where(i => i.Fest is null))
            {
                if (!gesehen.Add(innen.Ansicht + "|" + innen.Breite + "|" + innen.Tiefe + "|" + innen.Stuecke.Count + "|" + innen.Personen.Count))
                {
                    continue;
                }
                for (var x = 0.4f; x < innen.Breite; x += 1.2f)
                {
                    for (var z = 0.4f; z < innen.Tiefe; z += 1.2f)
                    {
                        for (var r = 0; r < 8; r++)
                        {
                            yield return ($"{name} {innen.Ansicht} ({x:0.0}, {z:0.0}) {r * 45}°", innen, new Vector2(x, z), r * MathF.PI / 4f);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void DuckenUndKriechen_WoErlaubt_BeruehrtNichtsUndNieDurchDieWand_GegenprobeOhnePruefung()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var figuren = new Dictionary<(Koerperhaltung, int), List<SpielSitzenTests.Dreieck>>();
        List<SpielSitzenTests.Dreieck> Figur1(Koerperhaltung h, int kleid)
        {
            if (!figuren.TryGetValue((h, kleid), out var d))
            {
                d = SpielSitzenTests.Dreiecke(Figur.Netz(g, null, 1f, SpielHaltungTests.Kleider[kleid], h == Koerperhaltung.Ducken ? Figur.Ducken : Figur.Kriechen, 0), Matrix4x4.Identity);
                figuren[(h, kleid)] = d;
            }
            return d;
        }
        var hindernisse = new Dictionary<Innenraum, List<(string Name, List<SpielSitzenTests.Dreieck> D)>>();
        var funde = new List<string>();
        int erlaubt = 0, verboten = 0, gegenFunde = 0;
        var zaehler = 0;
        foreach (var (wo, innen, ort, blick) in Stellen())
        {
            foreach (var haltung in new[] { Koerperhaltung.Ducken, Koerperhaltung.Kriechen })
            {
                var frei = innen.KoerperFrei(ort, blick, haltung);
                // jede 7. verbotene Stelle als Gegenprobe pruefen (die Figur dort zeigt Funde), alle erlaubten ganz
                if (!frei && ++zaehler % 7 != 0)
                {
                    verboten++;
                    continue;
                }
                var hin = hindernisse.TryGetValue(innen, out var h) ? h : hindernisse[innen] = SpielHaltungTests.Hindernisse(innen);
                var waende = SpielWandTests.WandQuader(innen).ToList();
                var gefunden = new List<string>();
                // ohne Kopfbedeckung und mit dem Spitzhut (breiteste Krempe; die Muetze liegt innerhalb, Test KoerperQuader)
                foreach (var kleid in new[] { 0, 2 })
                {
                    if (gefunden.Count > 0)
                    {
                        break;
                    }
                    var lage = SpielHaltungTests.Lage(new Vector3(ort.X, 0, ort.Y), blick, Innenraum.FigurHoehe, Vector2.Zero);
                    var figur = Figur1(haltung, kleid).Select(d => new SpielSitzenTests.Dreieck(Vector3.Transform(d.A, lage), Vector3.Transform(d.B, lage), Vector3.Transform(d.C, lage))).ToList();
                    foreach (var (name, d) in hin.Where(x => !ProbeWelt.BegehbareTypen.Any(b => x.Name.StartsWith(b + " ", StringComparison.Ordinal))))
                    {
                        if (SpielHaltungTests.Durchdringung(figur, d) is > 0 and var n)
                        {
                            gefunden.Add($"{haltung} mit {name}: {n} Ecken");
                        }
                    }
                    if (waende.Any(w => figur.Any(d => SpielWandTests.KanteDurch(d.A, d.B, w, ort) || SpielWandTests.KanteDurch(d.B, d.C, w, ort) || SpielWandTests.KanteDurch(d.C, d.A, w, ort))))
                    {
                        gefunden.Add($"{haltung} durch die Wand");
                    }
                    var huelle = SpielSitzenTests.Huelle(figur);
                    if (huelle.Min.X < 0 || huelle.Min.Z < 0 || huelle.Max.X > innen.Breite || huelle.Max.Z > innen.Tiefe)
                    {
                        gefunden.Add($"{haltung} aus dem Raum");
                    }
                }
                if (frei)
                {
                    erlaubt++;
                    funde.AddRange(gefunden.Select(f => $"{wo}: {f}"));
                }
                else
                {
                    verboten++;
                    gegenFunde += gefunden.Count > 0 ? 1 : 0;
                }
            }
        }
        Assert.True(funde.Count == 0, funde.Count + " Funde:\n" + string.Join("\n", funde.Take(30)));
        Assert.True(erlaubt > 1000, "erlaubt: " + erlaubt);
        Assert.True(verboten > 300, "verboten: " + verboten);
        // Gegenprobe: ohne Pruefung (jede Stelle erlaubt) gaebe es Beruehrungen
        Assert.True(gegenFunde > 20, "Gegenprobe: " + gegenFunde);
    }

    [Fact]
    public void Springen_DrinnenNurMitPlatzUeberDemKopf_UnterDemTischNicht()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var funde = new List<string>();
        var erlaubt = 0;
        var gesperrt = 0;
        foreach (var (wo, innen, ort, blick) in Stellen().Where((_, i) => i % 3 == 0))
        {
            if (!innen.KoerperFrei(ort, blick, Koerperhaltung.Ducken) || !innen.SprungFrei(ort, blick))
            {
                gesperrt++;
                continue;
            }
            erlaubt++;
            // hoechster Punkt: die Figur im Sprung beruehrt nichts
            var lage = Matrix4x4.CreateScale(Innenraum.FigurHoehe) * Matrix4x4.CreateRotationY(blick)
                * Matrix4x4.CreateTranslation(ort.X, Innenraum.SprungHoch * Innenraum.FigurHoehe, ort.Y);
            var figur = SpielSitzenTests.Dreiecke(Figur.Netz(g, null, 1f, SpielHaltungTests.Kleider[2], Figur.Springen, 0), lage);
            if (SpielSitzenTests.Huelle(figur).Max.Y > Innenraum.IchWandHoehe)
            {
                funde.Add($"{wo}: an der Decke");
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde.Take(20)));
        Assert.True(erlaubt > 500 && gesperrt > 50, $"erlaubt {erlaubt}, gesperrt {gesperrt}");
    }

    private static Spielablauf Spiel() => SpielHaltungBedienungTests.NeuesSpiel();

    [Fact]
    public void Springen_WegBildFuerBildGleich_KeinEreignis_ImGehenUndAnDerStelle()
    {
        // zweimal derselbe Weg in der Stadt, einmal mit Sprung: jede Stelle gleich, nur die Hoehe im Sprung anders
        var mit = SpielHaltungBedienungTests.ZuHause(Spiel());
        var ohne = SpielHaltungBedienungTests.ZuHause(Spiel());
        foreach (var s in new[] { mit, ohne })
        {
            s.Verlassen();
            Assert.False(s.ImInnenraum);
        }
        var zeilen = mit.Ablauf.Stand.Verlauf.Count;
        var ziel = mit.Szene.Orte.First(o => o.Id != mit.Jetzt.Ort);
        Assert.Equal(StadtGrund.Ok, mit.GeheZu(ziel.Id));
        Assert.Equal(StadtGrund.Ok, ohne.GeheZu(ziel.Id));
        var zeilenNachWeg = mit.Ablauf.Stand.Verlauf.Count;
        var hoch = 0f;
        for (var i = 0; i < 4000 && (mit.Geht || ohne.Geht); i++)
        {
            if (i % 40 == 5)
            {
                Assert.True(mit.Springen());
            }
            mit.HaltungTakt(0.05f);
            hoch = MathF.Max(hoch, mit.SprungHoehe);
            Assert.Equal(mit.Fortschreiten(0.05), ohne.Fortschreiten(0.05));
            Assert.Equal((ohne.FigurX, ohne.FigurZ), (mit.FigurX, mit.FigurZ));
        }
        Assert.InRange(hoch, Innenraum.SprungHoch * 0.95f, Innenraum.SprungHoch);
        // nur Darstellung: das Gehen schreibt wie ohne Sprung, der Sprung selbst nichts
        Assert.Equal(zeilenNachWeg, ohne.Ablauf.Stand.Verlauf.Count + (mit.Ablauf.Stand.Verlauf.Count - zeilenNachWeg));
        Assert.Equal(ohne.Ablauf.Stand.Verlauf.Count, mit.Ablauf.Stand.Verlauf.Count);
        Assert.True(zeilenNachWeg >= zeilen);
        // an der Stelle: Sprung endet nach SprungDauer, Modell nur im Sprung
        Assert.True(mit.Springen());
        Assert.Equal(Figur.Springen, mit.HaltungModell);
        for (var t = 0f; t < StadtSpiel.SprungDauer + 0.1f; t += 0.05f)
        {
            mit.HaltungTakt(0.05f);
        }
        Assert.False(mit.Springt);
        Assert.Null(mit.HaltungModell);
        Assert.Equal(0f, mit.SprungHoehe);
    }

    [Fact]
    public void DuckenKriechen_NurAnDerStelle_GehenBeendet_KeinEreignis_GegenprobeOhne3D()
    {
        var spiel = Spiel();
        var s = SpielHaltungBedienungTests.ZuHause(spiel);
        var zeilen = spiel.Speicher.Ereignisse().Count;
        // freie Stelle mitten im Raum suchen
        var innen = s.Innen!;
        var stelle = Enumerable.Range(0, 400).Select(i => new Vector2(0.5f + (i % 20 * 0.4f), 0.5f + (i / 20 * 0.4f)))
            .First(p => p.X < innen.Breite && p.Y < innen.Tiefe && innen.KoerperFrei(p, 0f, Koerperhaltung.Kriechen) && s.GeheZu(p.X, p.Y));
        SpielHaltungBedienungTests.Ankommen(s);
        Assert.True(s.Ducken(), s.HaltungHinweis);
        Assert.Equal(Koerperhaltung.Ducken, s.Koerper);
        Assert.Equal(Figur.Ducken, s.HaltungModell);
        Assert.Equal(SpielProbeTexte.DuDuckst, s.HaltungHinweis);
        // von Ducken direkt in Kriechen, dann noch einmal „Kriechen“ = aufstehen
        Assert.True(s.Kriechen());
        Assert.Equal(Koerperhaltung.Kriechen, s.Koerper);
        Assert.True(s.Kriechen());
        Assert.Equal(Koerperhaltung.Stehen, s.Koerper);
        Assert.Equal(SpielProbeTexte.DuStehst, s.HaltungHinweis);
        // Gehen beendet die Haltung, und waehrend des Gehens geht sie nicht
        Assert.True(s.Kriechen());
        Assert.True(s.GeheZu(stelle.X + 1.2, stelle.Y));
        Assert.Equal(Koerperhaltung.Stehen, s.Koerper);
        Assert.False(s.Ducken());
        Assert.Equal(SpielProbeTexte.ErstAnkommenHaltung, s.HaltungHinweis);
        Assert.StartsWith(SpielProbeTexte.ErstAnkommenHaltung, s.Zeile, StringComparison.Ordinal);
        SpielHaltungBedienungTests.Ankommen(s);
        Assert.Equal(Koerperhaltung.Stehen, s.Koerper);
        // nur Darstellung
        Assert.Equal(zeilen, spiel.Speicher.Ereignisse().Count);
        // Gegenprobe: ohne 3D gibt es keine Haltung
        s.OhneDreiDSetzen(true);
        Assert.False(s.KoerperMoeglich);
        Assert.False(s.Ducken());
        Assert.Equal(Koerperhaltung.Stehen, s.Koerper);
    }

    [Fact]
    public void KeinPlatz_HinweisUndDrehung_GegenprobeOhnePruefung()
    {
        var s = SpielHaltungBedienungTests.ZuHause(Spiel());
        var innen = s.Innen!;
        // eine Stelle, an der man stehen, aber in keiner Richtung kriechen kann
        (float X, float Z)? eng = null;
        for (var x = 0.3f; x < innen.Breite && eng is null; x += 0.1f)
        {
            for (var z = 0.3f; z < innen.Tiefe && eng is null; z += 0.1f)
            {
                if (innen.KoerperFrei(new Vector2(x, z), 0, Koerperhaltung.Stehen)
                    && StadtSpiel.KoerperRichtungen.All(r => !innen.KoerperFrei(new Vector2(x, z), r * MathF.PI / 180f, Koerperhaltung.Kriechen))
                    && s.GeheZu(x, z))
                {
                    SpielHaltungBedienungTests.Ankommen(s);
                    if (StadtSpiel.KoerperRichtungen.All(r => !innen.KoerperFrei(new Vector2(s.FigurX, s.FigurZ), (s.FigurRichtung + r) * MathF.PI / 180f, Koerperhaltung.Kriechen)))
                    {
                        eng = (s.FigurX, s.FigurZ);
                    }
                }
            }
        }
        Assert.NotNull(eng);
        Assert.False(s.Kriechen());
        Assert.Equal(SpielProbeTexte.KeinPlatzKriechen, s.HaltungHinweis);
        Assert.StartsWith(SpielProbeTexte.KeinPlatzKriechen, s.Zeile, StringComparison.Ordinal);
        Assert.False(s.Kriechen());
        // der Hinweis steht nur einmal vor der Zeile
        Assert.Equal(1, s.Zeile.Split(SpielProbeTexte.KeinPlatzKriechen).Length - 1);
        StadtSpiel.KoerperFreiAltFuerTests(true);
        try
        {
            Assert.True(s.Kriechen());
        }
        finally
        {
            StadtSpiel.KoerperFreiAltFuerTests(false);
        }
    }

    [Theory]
    [InlineData("sofa")]
    [InlineData("sessel")]
    [InlineData("liegestuhl")]
    public void Zuruecklehnen_SofaSesselLiegestuhl_UndAufrichten_KeinEreignis(string moebel)
    {
        var spiel = Spiel();
        SpielHaltungBedienungTests.KaufenUndAufstellen(spiel, moebel);
        var s = SpielHaltungBedienungTests.ZuHause(spiel);
        var zeilen = spiel.Speicher.Ereignisse().Count;
        var platz = SpielHaltungBedienungTests.PlatzVon(s, moebel);
        var zurueck = s.Innen!.ZurueckZu(platz);
        Assert.NotNull(zurueck);
        Assert.Equal(SitzArt.Zurueck, zurueck!.Art);
        Assert.Same(platz, s.Innen.SitzZu(zurueck));
        var vor = platz.Ort + (new Vector3(MathF.Sin(platz.Blick), 0, MathF.Cos(platz.Blick)) * 0.8f);
        s.GeheZu(vor.X, vor.Z);
        SpielHaltungBedienungTests.Ankommen(s);
        // stehend kein „Zurücklehnen“; „Hinsetzen“ setzt gerade hin
        Assert.False(SpielHaltungBedienungTests.Hat(s, StadtSpiel.AktionZuruecklehnen));
        Assert.True(s.Knopf(StadtSpiel.AktionHinsetzen));
        Assert.Equal(SitzArt.Sitzen, s.MeinPlatz?.Art);
        Assert.True(SpielHaltungBedienungTests.Hat(s, StadtSpiel.AktionZuruecklehnen), string.Join(",", s.Knoepfe));
        Assert.True(s.Knopf(StadtSpiel.AktionZuruecklehnen));
        Assert.Equal(SitzArt.Zurueck, s.MeinPlatz?.Art);
        Assert.Equal(SitzArt.Zurueck, s.HaltungArt);
        Assert.StartsWith(SpielProbeTexte.DuSitztZurueck, s.Zeile, StringComparison.Ordinal);
        Assert.True(SpielHaltungBedienungTests.Hat(s, StadtSpiel.AktionAufrichten) && SpielHaltungBedienungTests.Hat(s, StadtSpiel.AktionAufstehen));
        Assert.False(SpielHaltungBedienungTests.Hat(s, StadtSpiel.AktionZuruecklehnen));
        Assert.True(s.Knopf(StadtSpiel.AktionAufrichten));
        Assert.Equal(SitzArt.Sitzen, s.MeinPlatz?.Art);
        Assert.True(s.Knopf(StadtSpiel.AktionZuruecklehnen));
        Assert.True(s.Knopf(StadtSpiel.AktionAufstehen));
        Assert.Null(s.MeinPlatz);
        // im Sitzen keine Haltungen
        Assert.True(s.Knopf(StadtSpiel.AktionHinsetzen));
        Assert.False(s.KoerperMoeglich);
        Assert.False(s.Ducken());
        Assert.Equal(SpielProbeTexte.ErstAufstehen, s.HaltungHinweis);
        Assert.Equal(zeilen, spiel.Speicher.Ereignisse().Count);
    }

    [Fact]
    public void IchSicht_AugeSinktGeducktUndSteigtImSprung_GegenprobeStehhoehe()
    {
        var s = SpielHaltungBedienungTests.ZuHause(Spiel());
        var innen = s.Innen!;
        var stelle = Enumerable.Range(0, 400).Select(i => new Vector2(0.5f + (i % 20 * 0.4f), 0.5f + (i / 20 * 0.4f)))
            .First(p => p.X < innen.Breite && p.Y < innen.Tiefe && innen.KoerperFrei(p, 0f, Koerperhaltung.Ducken) && innen.SprungFrei(p, 0f) && s.GeheZu(p.X, p.Y));
        SpielHaltungBedienungTests.Ankommen(s);
        var stehend = IchSicht.Auge(s).Y;
        Assert.True(s.Ducken());
        var geduckt = IchSicht.Auge(s).Y;
        Assert.Equal(stehend - (Figur.KopfSenkung(s.Grafik, Figur.Ducken) * Innenraum.FigurHoehe), geduckt, 4);
        IchSicht.AugeStehendFuerTests(true);
        try
        {
            Assert.Equal(stehend, IchSicht.Auge(s).Y, 5);
        }
        finally
        {
            IchSicht.AugeStehendFuerTests(false);
        }
        Assert.True(s.Ducken());
        Assert.True(s.Springen());
        s.HaltungTakt(StadtSpiel.SprungDauer / 2);
        Assert.True(IchSicht.Auge(s).Y > stehend + (0.15f * Innenraum.FigurHoehe));
        // Umsehen geduckt: der Koerper dreht nur, wo Platz ist (sonst bleibt er, und IchSicht.Takt stellt die Kamera zurueck)
        s.HaltungTakt(StadtSpiel.SprungDauer);
        Assert.True(s.Ducken());
        for (var r = 0f; r < 360f; r += 15f)
        {
            var vorher = s.FigurRichtung;
            var frei = s.KoerperFreiBei(s.FigurX, s.FigurZ, r, Koerperhaltung.Ducken);
            Assert.Equal(frei, s.KoerperDrehen(r));
            Assert.Equal(frei ? r : vorher, s.FigurRichtung, 3);
        }
    }
}

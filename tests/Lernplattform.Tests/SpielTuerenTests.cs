using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 443/444, Weisungen P12-359/P12-360, Nico 1c 2a 3a). Abnahmesaetze: (1) „Jeder Raum ausser den
/// offenen Bereichen und breiten Durchgaengen hat eine Tuer, in jedem Grundriss der Stadt (Fassung 0 und Nicos Stadt 1, Spiel und
/// Werkzeug bauen dieselben Innenraeume).“ (2) „Eine geschlossene Tuer ist ein Hindernis: frei gehen und Antippen kommen nicht
/// hindurch; nach ‚Tür öffnen‘ (F/Knopf) kommt die Figur durch, ohne haengen zu bleiben; geht sie weiter als 2,2 von der Tuer
/// weg, ist die Tuer wieder zu.“ (3) „Am Ausgang fuehrt die Handlung hinaus (‚Zur Stadt‘).“ (4) „F/Knopf loest die erste Handlung
/// der Liste aus, die Liste enthaelt alles, was das Menue an der Stelle zeigte.“ Gegenprobe: ohne Tueren
/// (<see cref="Innenraum.OhneTuerenFuerTests"/>) geht die Figur ohne Oeffnen durch.
/// </summary>
public sealed class SpielTuerenTests
{
    private const double Dt = 1.0 / 60;

    /// <summary>Alle Raeume mit Tuer im Grundriss eines Innenraums: (Kennung, Breite, offen).</summary>
    private static IEnumerable<(string Id, float Breite, bool Offen)> RaeumeMitTuer(Innenraum innen)
    {
        foreach (var raum in SpielDaten.Liste(innen.Gebaeude["raeume"]).Select(SpielDaten.Objekt))
        {
            if (SpielDaten.Get(raum, "tuer") is not PyObjekt tuer || tuer.Count == 0)
            {
                continue;
            }
            var breite = (float)(Py.F(tuer["bis"]) - Py.F(tuer["von"]));
            yield return (Py.Str(raum["id"]), breite, Py.Wahr(SpielDaten.Get(tuer, "offen", false)));
        }
    }

    [Fact]
    public void JederRaum_HatEineTuer_AusserOffenenBereichenUndBreitenDurchgaengen_AlleGrundrisse()
    {
        var funde = new List<string>();
        var (tueren, offen, breit, ausgaenge) = (0, 0, 0, 0);
        foreach (var (name, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume).DistinctBy(i => i.Ansicht))
            {
                foreach (var (id, b, o) in RaeumeMitTuer(innen))
                {
                    var zahl = innen.RaumTueren.Count(t => t.Raum == id);
                    var soll = o || b > Innenraum.TuerHoechstens ? 0 : 1;
                    if (zahl != soll)
                    {
                        funde.Add($"{name} {innen.Ansicht} Raum {id} (Breite {b}, offen {o}): {zahl} Tueren statt {soll}");
                    }
                    offen += o ? 1 : 0;
                    breit += !o && b > Innenraum.TuerHoechstens ? 1 : 0;
                }
                foreach (var t in innen.RaumTueren)
                {
                    tueren++;
                    // jede Tuer kann aufgehen, ohne ein Einrichtungsstueck zu beruehren
                    if (innen.OffenLage(t) is null)
                    {
                        funde.Add($"{name} {innen.Ansicht} Tuer {t.Nr} ({t.Raum}): kein Platz zum Aufgehen");
                    }
                    // die Zarge steht als Wandstueck in der Liste der Waende (verdeckt wie eine Wand)
                    if (!innen.Waende.Any(w => w.Tuer == t))
                    {
                        funde.Add($"{name} {innen.Ansicht} Tuer {t.Nr}: Zarge fehlt");
                    }
                }
                if (Lernplattform.Core.Stadtbau.Etagen.Teilen(innen.Ansicht).Etage == 0)
                {
                    ausgaenge++;
                    // vor der Ausgangstuer (0,9 von der Westwand, mittig) gilt „am Ausgang“, weiter drinnen (2,5) nicht
                    if (innen.Ausgang is not var (von, bis) || !innen.AmAusgang(0.9f, (von + bis) / 2, StadtSpiel.AusgangReichweite)
                        || innen.AmAusgang(2.5f, (von + bis) / 2, StadtSpiel.AusgangReichweite))
                    {
                        funde.Add($"{name} {innen.Ansicht}: Ausgang {innen.Ausgang} nicht erkannt");
                    }
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde.Take(30)));
        // es gibt alle drei Faelle (Tuer, offener Bereich, breiter Durchgang) und viele Ausgaenge
        Assert.True(tueren > 30, tueren.ToString());
        Assert.True(offen > 0 && breit > 0 && ausgaenge > 10, $"{offen} {breit} {ausgaenge}");
        // Gegenprobe: ohne Tueren gibt es keine
        Innenraum.OhneTuerenFuerTests = true;
        try
        {
            Assert.All(SpielWandTests.AlleRaeume(SpielInnenraumTests.Innenraeume.Value).DistinctBy(i => i.Ansicht), i => Assert.Empty(i.RaumTueren));
        }
        finally
        {
            Innenraum.OhneTuerenFuerTests = false;
        }
    }

    private static StadtSpiel NeuesSpiel()
    {
        var spiel = SpielHaltungBedienungTests.NeuesSpiel();
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        return new StadtSpiel(stadt, spiel, SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value, SpielGeschaefteTests.Daten.Value,
            null, () => null, () => Jahreszeit.Sommer);
    }

    /// <summary>Seite der Figur zur Tuer: negativ ausserhalb des Raums, positiv im Raum; dazu der Abstand quer zur Wand.</summary>
    private static float Quer(RaumTuer t, StadtSpiel s) => (t.Lokal(s.FigurX, s.FigurZ).Quer - t.Linie) * t.Innen;

    private static bool InDerOeffnung(RaumTuer t, StadtSpiel s)
    {
        var l = t.Lokal(s.FigurX, s.FigurZ).Laengs;
        return l > t.Von && l < t.Bis;
    }

    /// <summary>Frei auf die Tuer zu gehen (Richtung in den Raum); Rueckgabe: weitester Stand quer zur Wand und ob die Figur je in der Sperre war.</summary>
    private static (float Weitest, bool InSperre) Zugehen(RaumTuer t, StadtSpiel s, double sekunden)
    {
        var (rx, rz) = t.Waagerecht ? (0.0, (double)t.Innen) : (t.Innen, 0.0);
        var weitest = float.MinValue;
        var inSperre = false;
        for (var i = 0; i < sekunden / Dt; i++)
        {
            s.FreiGehen(rx, rz, Dt);
            var q = Quer(t, s);
            weitest = MathF.Max(weitest, q);
            inSperre |= InDerOeffnung(t, s) && MathF.Abs(q) < Innenraum.TuerSperreQuer - 0.02f;
        }
        s.FreiGehen(0, 0, 1);
        return (weitest, inSperre);
    }

    /// <summary>
    /// Abnahmesatz (2) und (4) frei gehen, an jeder Raumtuer jedes Orts von Nicos Stadt: vor der Tuer steht „Tür öffnen“ in den
    /// Handlungen; zu kommt die Figur nicht hindurch (bleibt vor dem Blatt), nach dem Oeffnen geht sie in den Raum; weit weg geht
    /// die Tuer wieder zu. Kein Ereignis, der Spielstand bleibt gleich.
    /// </summary>
    [Fact]
    public void FreiGehen_ZuSperrt_OffenDurch_WeitWegWiederZu_JedeTuer()
    {
        var s = NeuesSpiel();
        Assert.True(s.TuerenAn);
        var stand = s.Ablauf.Stand;
        var zeilen = s.Ablauf.Speicher.Ereignisse().Count;
        var funde = new List<string>();
        var (geprueft, uebersprungen) = (0, 0);
        foreach (var innen in SpielWandTests.AlleRaeume(StadtbauFassung2Tests.Fassung2.Value.Innenraeume).DistinctBy(i => i.Ansicht))
        {
            foreach (var t in innen.RaumTueren)
            {
                var mitte = (t.Von + t.Bis) / 2;
                var vor = t.Punkt(mitte, t.Linie - (t.Innen * 0.8f));
                // erst offen probieren: kommt die Figur von dort ueberhaupt in den Raum (Moebel vor der Tuer)? Sonst uebersprungen
                s.Ansehen(innen, vor.X, vor.Y, 0f);
                Assert.True(s.Handeln(StadtSpiel.AktionTuerAuf), $"{innen.Ansicht} Tuer {t.Nr}: kein „Tür öffnen“ vor der Tuer");
                if (Zugehen(t, s, 3).Weitest < 0.5f)
                {
                    uebersprungen++;
                    continue;
                }
                geprueft++;
                // zu: wieder vor die Tuer stellen und schliessen
                s.Ansehen(innen, vor.X, vor.Y, 0f);
                if (!s.TuerZu(t))
                {
                    Assert.True(s.Handeln(StadtSpiel.AktionTuerZu));
                }
                Assert.True(s.TuerZu(t), $"{innen.Ansicht} Tuer {t.Nr}: geht nicht zu");
                var haupt = s.Handlungen.FirstOrDefault(h => h.Aktion == StadtSpiel.AktionTuerAuf);
                if (haupt.Name != SpielProbeTexte.KnopfTuerOeffnen || haupt.Art != HandlungArt.Tuer)
                {
                    funde.Add($"{innen.Ansicht} Tuer {t.Nr}: keine Handlung „Tür öffnen“");
                }
                var (weitest, inSperre) = Zugehen(t, s, 3);
                if (weitest > -Innenraum.TuerSperreQuer + 0.03f || inSperre)
                {
                    funde.Add($"{innen.Ansicht} Tuer {t.Nr}: zu und doch hindurch (quer {weitest:0.00})");
                }
                Assert.True(s.Handeln(StadtSpiel.AktionTuerAuf));
                Assert.False(s.TuerZu(t));
                Assert.Contains(s.Handlungen, h => h.Aktion == StadtSpiel.AktionTuerZu);
                if (Zugehen(t, s, 3).Weitest < 0.5f)
                {
                    funde.Add($"{innen.Ansicht} Tuer {t.Nr}: offen und haengt fest (quer {Quer(t, s):0.00})");
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde.Take(30)));
        Assert.True(geprueft >= 3 * uebersprungen && geprueft > 20, $"{geprueft} geprueft, {uebersprungen} uebersprungen");
        Assert.Equal(zeilen, s.Ablauf.Speicher.Ereignisse().Count);
        Assert.Same(stand, s.Ablauf.Stand);
    }

    /// <summary>Weit weg (weiter als <see cref="StadtSpiel.TuerZuAb"/>) geht eine offene Tuer von selbst zu, nah bleibt sie offen.</summary>
    [Fact]
    public void OffeneTuer_GehtZu_SobaldDieFigurWegIst()
    {
        var s = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel());
        s.TuerenOeffnenSelbst = false;
        var innen = s.Innen!;
        var start = new Vector2(innen.StartX, innen.StartZ);
        var t = innen.RaumTueren.OrderByDescending(r => Vector2.Distance(r.Mitte, start)).First();
        var vor = t.Punkt((t.Von + t.Bis) / 2, t.Linie - (t.Innen * 0.8f));
        s.Ansehen(innen, vor.X, vor.Y, 0f);
        Assert.True(s.Handeln(StadtSpiel.AktionTuerAuf));
        Assert.False(s.TuerZu(t));
        var nah = t.Punkt((t.Von + t.Bis) / 2, t.Linie - (t.Innen * (StadtSpiel.TuerZuAb - 0.1f)));
        s.Ansehen(innen, nah.X, nah.Y, 0f);
        Assert.False(s.TuerZu(t));
        var weit = start;
        Assert.True(Vector2.Distance(weit, t.Mitte) > StadtSpiel.TuerZuAb);
        s.Ansehen(innen, weit.X, weit.Y, 0f);
        Assert.True(s.TuerZu(t));
    }

    /// <summary>
    /// Abnahmesatz (2) Antippen: in der Wohnung in jeden Raum mit Tuer tippen; zu bleibt die Figur vor der Tuer stehen (Hinweis
    /// in der Zeile), nach „Tür öffnen“ kommt sie an. Gegenprobe ohne Tueren: sie kommt gleich an.
    /// </summary>
    [Fact]
    public void Antippen_BleibtVorDerTuer_NachOeffnenKommtSieAn_OhneTuerenGleich()
    {
        var geprueft = 0;
        var tueren = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel()).Innen!.RaumTueren;
        Assert.NotEmpty(tueren);
        foreach (var ohne in new[] { false, true })
        {
            Innenraum.OhneTuerenFuerTests = ohne;
            try
            {
                var s = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel());
                s.TuerenOeffnenSelbst = false;
                var innen = s.Innen!;
                Assert.Equal(ohne, innen.RaumTueren.Count == 0);
                foreach (var t in tueren)
                {
                    var ziel = t.Punkt((t.Von + t.Bis) / 2, t.Linie + (t.Innen * 0.9f));
                    var vor = t.Punkt((t.Von + t.Bis) / 2, t.Linie - (t.Innen * 0.8f));
                    s.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
                    s.Ansehen(innen, vor.X, vor.Y, 0f);
                    if (!s.GeheZu(ziel.X, ziel.Y))
                    {
                        continue;
                    }
                    SpielHaltungBedienungTests.Ankommen(s);
                    var da = Vector2.Distance(new Vector2(s.FigurX, s.FigurZ), ziel) < 0.3f;
                    if (ohne)
                    {
                        Assert.True(da, $"ohne Tueren Tuer {t.Nr} nicht angekommen");
                        geprueft++;
                        continue;
                    }
                    Assert.False(da, $"Tuer {t.Nr}: zu und doch angekommen");
                    Assert.True(Quer(t, s) < -Innenraum.TuerSperreQuer + 0.03f, $"Tuer {t.Nr}: quer {Quer(t, s)}");
                    Assert.Contains(SpielProbeTexte.TuerIstZu, s.Zeile);
                    Assert.Equal(StadtSpiel.AktionTuerAuf, s.Handlungen.First(h => h.Art == HandlungArt.Tuer).Aktion);
                    Assert.True(s.Handeln(StadtSpiel.AktionTuerAuf));
                    Assert.True(s.GeheZu(ziel.X, ziel.Y));
                    SpielHaltungBedienungTests.Ankommen(s);
                    Assert.True(Vector2.Distance(new Vector2(s.FigurX, s.FigurZ), ziel) < 0.3f, $"Tuer {t.Nr}: offen nicht angekommen");
                    Assert.DoesNotContain(SpielProbeTexte.TuerIstZu, s.Zeile);
                    geprueft++;
                }
            }
            finally
            {
                Innenraum.OhneTuerenFuerTests = false;
            }
        }
        Assert.True(geprueft >= 4, geprueft.ToString());
    }

    /// <summary>Abnahmesatz (3): am Ausgang ist „Zur Stadt“ die Haupthandlung und fuehrt hinaus; weiter drinnen gibt es sie nicht.</summary>
    [Fact]
    public void Ausgang_ZurStadt_IstHaupthandlung_UndFuehrtHinaus()
    {
        var s = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel());
        var innen = s.Innen!;
        s.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
        Assert.Equal(StadtSpiel.HandlungZurStadt, s.Haupthandlung?.Aktion);
        Assert.Equal(HandlungArt.Tuer, s.Haupthandlung?.Art);
        Assert.Equal(SpielProbeTexte.ZurStadt, s.Haupthandlung?.Name);
        // Gegenprobe: weit vom Ausgang keine Handlung „Zur Stadt“ (dort fuehrt Esc oder das Handy hinaus)
        var t = innen.RaumTueren.OrderByDescending(r => Vector2.Distance(r.Mitte, new Vector2(innen.StartX, innen.StartZ))).First();
        var drin = t.Punkt((t.Von + t.Bis) / 2, t.Linie - (t.Innen * 0.8f));
        s.Ansehen(innen, drin.X, drin.Y, 0f);
        Assert.False(innen.AmAusgang(drin.X, drin.Y, StadtSpiel.AusgangReichweite));
        Assert.DoesNotContain(s.Handlungen, h => h.Aktion == StadtSpiel.HandlungZurStadt);
        Assert.False(s.Handeln(StadtSpiel.HandlungZurStadt));
        Assert.True(s.ImInnenraum);
        s.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
        Assert.True(s.Handeln(StadtSpiel.HandlungZurStadt));
        Assert.False(s.ImInnenraum);
        // draussen vor der Wohnung: „Hineingehen“ ist die Haupthandlung
        Assert.Equal(StadtSpiel.HandlungBetreten, s.Haupthandlung?.Aktion);
        Assert.True(s.Handeln(StadtSpiel.HandlungBetreten));
        Assert.True(s.ImInnenraum);
    }

    /// <summary>
    /// Abnahmesatz (4): die Handlungen fassen genau zusammen, was das Menue an der Stelle zeigte (alle Knoepfe des Orts, dazu
    /// Hineingehen, Zur Stadt, Warten, Einrichten); beim Einrichten gibt es keine (dort bleibt das Menue). Ohne 3D keine Tueren.
    /// </summary>
    [Fact]
    public void Handlungen_EnthaltenAlleKnoepfeDesOrts_BeimEinrichtenKeine()
    {
        var s = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel());
        var innen = s.Innen!;
        var gesehen = 0;
        // jede Stelle im Abstand 0,75 ueber die ganze Wohnung (Schritt des Laufrasters 0,25)
        for (var zeile = 0; (zeile + 0.5) * Wege.Schritt < innen.Tiefe; zeile += 3)
        {
            for (var spalte = 0; (spalte + 0.5) * Wege.Schritt < innen.Breite; spalte += 3)
            {
                var (x, z) = ((float)((spalte + 0.5) * Wege.Schritt), (float)((zeile + 0.5) * Wege.Schritt));
                s.Ansehen(innen, x, z, 0f);
                var aktionen = s.Handlungen.Select(h => h.Aktion).ToList();
                foreach (var (aktion, _) in s.Knoepfe)
                {
                    Assert.Contains(aktion, aktionen);
                    gesehen++;
                }
                Assert.Equal(aktionen.Count, aktionen.Distinct().Count());
                Assert.Equal(s.KannEinrichten, aktionen.Contains(StadtSpiel.HandlungEinrichten));
            }
        }
        Assert.True(gesehen > 20, gesehen.ToString());
        s.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
        Assert.True(s.Handeln(StadtSpiel.HandlungEinrichten));
        Assert.True(s.Einrichten);
        Assert.Empty(s.Handlungen);
        Assert.Null(s.Haupthandlung);
    }
}

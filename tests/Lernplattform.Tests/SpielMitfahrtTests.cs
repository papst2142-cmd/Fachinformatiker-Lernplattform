using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.73.0 (Weisung P12-239, Sammelliste 335): selbst mit dem Bus fahren, in Nicos Stadt 1 (Fassung 1). Geprueft wird
/// Nicos Ziel: zu Fuss zur Haltestelle, warten, „Einsteigen“ tippen, „Aussteigen“ tippen und an der naechsten Haltestelle neben
/// ihr stehen; Gegenprobe: ohne Druecken faehrt man an der naechsten vorbei. Jede Haltestelle, an den Spieltagen 1 bis 3, je
/// hoechstens eine ganze Runde des Busses plus Grenze (Lehre 329).
/// </summary>
public sealed class SpielMitfahrtTests
{
    private static byte[]? Lesen(string pfad)
    {
        var datei = Path.Combine(Pfade.Wurzel, pfad);
        return File.Exists(datei) ? File.ReadAllBytes(datei) : null;
    }

    private static readonly Lazy<StadtFassung> Fassung1 = new(() => AktiveFassung.LadenNummer(Lesen, 1));

    /// <summary>Schritt der Bildschleife in den Proben (60 Bilder je Sekunde).</summary>
    internal const double Dt = 1.0 / 60;

    internal static StadtSpiel Stadt1(Spielablauf? ablauf = null)
    {
        var f = Fassung1.Value;
        var inhalt = f.MitKarte(SpielInhaltTests.Inhalt);
        return new StadtSpiel(f.StadtFuer(inhalt), ablauf ?? SpielQuestTests.AllesErledigt(inhalt), null, SpielInnenraumTests.Innenraeume.Value);
    }

    /// <summary>Ein Bild: Verkehr weiter (sitzt die Figur im Bus, haelt kein Auto fuer sie), Figur weiter, Fahrt weiter.</summary>
    internal static void Bild(StadtSpiel spiel, Verkehr v)
    {
        var (fx, fz) = spiel.SitztImBus ? (-1000f, -1000f) : (spiel.FigurX, spiel.FigurZ);
        v.Weiter(Dt, fx, fz);
        spiel.Fortschreiten(Dt);
        spiel.MitfahrtTakt(v, Dt);
    }

    /// <summary>Eine ganze Runde des langsamsten Busses mit allen Halten (mit Figur) plus 20 s Grenze.</summary>
    internal static double Runde(Verkehr v) =>
        v.Autos.Where(a => a.Nummer >= Buslinie.ErsteNummer).Select(a => (a.Laenge / a.Tempo) + (a.Halte.Count * StadtSpiel.WartetMitFigur)).Max() + 20;

    /// <summary>Bilder, bis <paramref name="bis"/> gilt; false nach <paramref name="sekunden"/>.</summary>
    internal static bool Bis(StadtSpiel spiel, Verkehr v, Func<bool> bis, double sekunden)
    {
        for (var t = 0.0; t < sekunden; t += Dt)
        {
            if (bis())
            {
                return true;
            }
            Bild(spiel, v);
        }
        return bis();
    }

    /// <summary>Zu Fuss zur Haltestelle: auf ihr Haeuschen tippen und ankommen lassen.</summary>
    internal static void Hingehen(StadtSpiel spiel, Verkehr v, Haltestelle h)
    {
        var haeuschen = h.Haeuschen;
        Assert.True(spiel.GeheZu(haeuschen.X, haeuschen.Y) || spiel.HaltestelleBei(spiel.FigurX, spiel.FigurZ) == h.Nr, $"Haltestelle {h.Nr}: kein Weg");
        Assert.True(Bis(spiel, v, () => !spiel.Geht, 120), $"Haltestelle {h.Nr}: kommt nicht an");
        Bild(spiel, v);
    }

    /// <summary>
    /// Die naechste Haltestelle auf der Linie nach der Stelle, an der der Bus <paramref name="bus"/> gerade steht (eine
    /// Haltestelle kann auf einer Runde mehrmals vorkommen; gezaehlt wird nach dem Weg der Front).
    /// </summary>
    internal static int Naechste(Verkehr.Auto bus)
    {
        var front = (bus.Weg + bus.Halb) % bus.Laenge;
        var halte = bus.Halte.OrderBy(h => h.Front).ToList();
        return (halte.FirstOrDefault(h => h.Front > front + 0.1f) is { Haltestelle: > 0 } n ? n : halte[0]).Haltestelle;
    }

    private static string Text(Vector2 p) => string.Create(CultureInfo.InvariantCulture, $"({p.X:0.00},{p.Y:0.00})");

    [Fact]
    public void Stadt1_HatHaltestellenUndLinie_WartplatzErreichbar()
    {
        var spiel = Stadt1();
        Assert.True(spiel.Szene.Haltestellen.Count >= 2, $"{spiel.Szene.Haltestellen.Count} Haltestellen");
        var linie = Assert.IsType<Buslinie.Linie>(spiel.Linie);
        Assert.Empty(linie.NichtErreicht);
        foreach (var h in spiel.Szene.Haltestellen)
        {
            var w = StadtSpiel.Wartplatz(h);
            Assert.True(spiel.Szene.Laufraster.KannStehen(w.X, w.Y), $"Haltestelle {h.Nr}: Wartplatz {Text(w)} nicht begehbar");
            Assert.Equal(h.Nr, spiel.HaltestelleBei(w.X, w.Y));
            Assert.Equal(h.Nr, spiel.HaltestelleBei(h.Haeuschen.X, h.Haeuschen.Y));
            // auf der Fahrbahn steht man an keiner Haltestelle
            Assert.Null(spiel.HaltestelleBei(h.Halt.X, h.Halt.Y));
        }
    }

    /// <summary>
    /// Nicos Ziel an jeder Haltestelle und jedem der Tage 1 bis 3: hingehen, warten, einsteigen (der Bus faehrt waehrenddessen
    /// nicht ab), „Aussteigen“ druecken, an der naechsten Haltestelle neben ihr stehen. Die Fahrt ist ein Ereignis
    /// <c>stadt_gelaufen</c> mit <c>"mit": "bus"</c>, 0 Schritten und den Minuten der Strecke; die Uhr laeuft um genau diese.
    /// </summary>
    [Theory]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(3L)]
    public void Hingehen_Warten_Einsteigen_Aussteigen_NaechsteHaltestelle(long tag)
    {
        var spiel = Stadt1();
        var linie = spiel.Linie!;
        var funde = new List<string>();
        foreach (var h in spiel.Szene.Haltestellen.Where(h => linie.Halte.Any(l => l.Haltestelle == h.Nr)))
        {
            var v = Verkehr.FuerSpieltag(spiel.Szene, tag);
            var runde = Runde(v);
            Hingehen(spiel, v, h);
            if (spiel.WartetAn != h.Nr)
            {
                funde.Add($"Tag {tag}, Haltestelle {h.Nr}: wartet nicht (an {spiel.WartetAn})");
                continue;
            }
            if (!Bis(spiel, v, () => spiel.BusZumEinsteigen is not null, runde))
            {
                funde.Add($"Tag {tag}, Haltestelle {h.Nr}: kein Bus in einer Runde ({runde:F0} s)");
                continue;
            }
            Assert.Contains(spiel.Knoepfe, k => k.Aktion == StadtSpiel.AktionEinsteigen && k.Name == SpielProbeTexte.Einsteigen);
            var bus = v.Autos.Single(a => a.Nummer == spiel.BusZumEinsteigen);
            Assert.True(spiel.Knopf(StadtSpiel.AktionEinsteigen));
            var weg = bus.Weg;
            var soll = Naechste(bus);
            var bilder = 0;
            while (spiel.Mitfahrt == Mitfahrt.SteigtEin && bilder++ < 600)
            {
                Bild(spiel, v);
                if (spiel.Mitfahrt == Mitfahrt.SteigtEin && bus.Weg != weg)
                {
                    funde.Add($"Tag {tag}, Haltestelle {h.Nr}: Bus faehrt beim Einsteigen ab");
                    break;
                }
            }
            Assert.Equal(Mitfahrt.ImBus, spiel.Mitfahrt);
            Assert.Contains(spiel.Knoepfe, k => k.Aktion == StadtSpiel.AktionAussteigen && k.Name == SpielProbeTexte.Aussteigen);
            // abfahren lassen, dann „Aussteigen“
            Assert.True(Bis(spiel, v, () => bus.HaeltAn is null, 30), "Bus faehrt nicht ab");
            Assert.True(Vector2.Distance(new Vector2(spiel.FigurX, spiel.FigurZ), bus.Lage().Ort) < 0.01f, "Figur nicht beim Bus");
            var uhr = spiel.Jetzt.Uhr;
            var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
            Assert.True(spiel.Knopf(StadtSpiel.AktionAussteigen));
            Assert.Contains(spiel.Knoepfe, k => k.Aktion == StadtSpiel.AktionHaeltGleich && k.Name == SpielProbeTexte.HaeltGleich);
            if (!Bis(spiel, v, () => spiel.Mitfahrt == Mitfahrt.Keine, runde))
            {
                funde.Add($"Tag {tag}, Haltestelle {h.Nr}: steigt nicht aus");
                continue;
            }
            var (strecke, minuten, ein, aus) = spiel.LetzteMitfahrt!.Value;
            var ziel = spiel.Szene.Haltestellen.Single(x => x.Nr == soll);
            if (aus != soll || ein != h.Nr)
            {
                funde.Add($"Tag {tag}: von {h.Nr} ausgestiegen an {aus}, soll {soll}");
            }
            if (Vector2.Distance(new Vector2(spiel.FigurX, spiel.FigurZ), StadtSpiel.Wartplatz(ziel)) > 0.01f || spiel.WartetAn != soll)
            {
                funde.Add($"Tag {tag}, Haltestelle {h.Nr}: steht nicht neben Haltestelle {soll}");
            }
            Assert.Equal(zeilen + 1, spiel.Ablauf.Stand.Verlauf.Count);
            var e = spiel.Ablauf.Stand.Verlauf[^1];
            Assert.Equal(Stadt.Gelaufen, e.Art);
            Assert.Equal(Stadt.MitBus, e.Daten[Stadt.Mit]);
            Assert.Equal(0L, Py.Int(e.Daten["schritte"]));
            Assert.Equal((long)Math.Max(1, Math.Ceiling((strecke / spiel.Stadt.Zelle * Busfahrt.MinutenJeZelleBus) - 1e-9)), minuten);
            // ab 23:00 steht die Uhr (gekappt wie jeder Weg)
            var gezaehlt = Math.Max(0, Math.Min(minuten, Stadt.UhrSteht - uhr));
            Assert.Equal(gezaehlt, Py.Int(e.Daten["minuten"]));
            Assert.Equal(uhr + gezaehlt, spiel.Jetzt.Uhr);
            TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture,
                $"Tag {tag}: {ein} -> {aus}, {strecke:F2} Feld, {minuten} Min., Uhr {Stadt.Uhrzeit(uhr)}"));
            Assert.True(spiel.ImFreien);
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>Gegenprobe: ohne „Aussteigen“ faehrt man an der naechsten (und der uebernaechsten) Haltestelle vorbei.</summary>
    [Fact]
    public void OhneDruecken_FaehrtManWeiter()
    {
        var spiel = Stadt1();
        var linie = spiel.Linie!;
        var h = spiel.Szene.Haltestellen.First(x => linie.Halte.Any(l => l.Haltestelle == x.Nr));
        var v = Verkehr.FuerSpieltag(spiel.Szene, 1);
        Hingehen(spiel, v, h);
        Assert.True(Bis(spiel, v, () => spiel.BusZumEinsteigen is not null, Runde(v)));
        var bus = v.Autos.Single(a => a.Nummer == spiel.BusZumEinsteigen);
        var soll = Naechste(bus);
        Assert.True(spiel.Einsteigen());
        Assert.True(Bis(spiel, v, () => spiel.SitztImBus && bus.HaeltAn is null, 30));
        var gesehen = new List<int>();
        int? vorher = null;
        Assert.False(Bis(spiel, v, () =>
        {
            if (bus.HaeltAn is { } nr && nr != vorher)
            {
                gesehen.Add(nr);
            }
            vorher = bus.HaeltAn;
            return gesehen.Count >= Math.Min(2, linie.Halte.Count - 1) && bus.HaeltAn is null || !spiel.SitztImBus;
        }, Runde(v)) && !spiel.SitztImBus);
        Assert.True(spiel.SitztImBus, "ausgestiegen ohne Druecken");
        Assert.Equal(soll, gesehen[0]);
        Assert.Contains(spiel.Knoepfe, k => k.Aktion == StadtSpiel.AktionAussteigen);
    }

    /// <summary>E1: Weggehen vor dem Einsteigen nimmt den Knopf weg; Antippen der Stadt und die Ortsliste im Bus aendern nichts.</summary>
    [Fact]
    public void Weggehen_KnopfWeg_AntippenUndOrtslisteImBus_Nichts()
    {
        var spiel = Stadt1();
        var h = spiel.Szene.Haltestellen[0];
        var v = Verkehr.FuerSpieltag(spiel.Szene, 1);
        Hingehen(spiel, v, h);
        Assert.True(Bis(spiel, v, () => spiel.BusZumEinsteigen is not null, Runde(v)));
        // weg: 3 Zellen vom Wartplatz in die Gegenrichtung des Busses auf dem Gehweg (oder irgendwohin, das begehbar ist)
        var w = StadtSpiel.Wartplatz(h);
        var weg = w - (h.Fahrt * 2.5f);
        Assert.True(spiel.GeheZu(weg.X, weg.Y));
        Assert.DoesNotContain(spiel.Knoepfe, k => k.Aktion == StadtSpiel.AktionEinsteigen);
        Assert.True(Bis(spiel, v, () => !spiel.Geht, 30));
        Bild(spiel, v);
        Assert.Null(spiel.BusZumEinsteigen);
        Assert.False(spiel.Knopf(StadtSpiel.AktionEinsteigen));
        // zurueck und mitfahren
        Hingehen(spiel, v, h);
        Assert.True(Bis(spiel, v, () => spiel.BusZumEinsteigen is not null, Runde(v)));
        Assert.True(spiel.Einsteigen());
        Assert.True(Bis(spiel, v, () => spiel.SitztImBus, 10));
        var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
        Assert.False(spiel.GeheZu(w.X + 3, w.Y));
        Assert.False(spiel.Geht);
        Assert.Equal(SpielProbeTexte.ErstAussteigen, spiel.Zeile);
        var ort = spiel.Szene.Orte.First(o => o.Id != spiel.Jetzt.Ort);
        Assert.NotEqual(StadtGrund.Ok, spiel.GeheZu(ort.Id));
        Assert.NotEqual(StadtGrund.Ok, spiel.MitBusZu(ort.Id));
        Assert.Equal(zeilen, spiel.Ablauf.Stand.Verlauf.Count);
        Assert.True(spiel.SitztImBus);
    }

    /// <summary>Ohne Haltestellen (Fassung 0) aendert sich nichts: nie wartend, nie ein Knopf zum Einsteigen.</summary>
    [Fact]
    public void Fassung0_OhneHaltestellen_Nichts()
    {
        var spiel = SpielBusfahrtTests.Spiel(mitHalte: false);
        Assert.Empty(spiel.Szene.Haltestellen);
        var v = Verkehr.Fuer(spiel.Szene);
        Assert.DoesNotContain(v.Autos, a => a.Nummer >= Buslinie.ErsteNummer);
        for (var i = 0; i < 600; i++)
        {
            Bild(spiel, v);
            Assert.Null(spiel.WartetAn);
            Assert.Null(spiel.BusZumEinsteigen);
            Assert.Equal(Mitfahrt.Keine, spiel.Mitfahrt);
        }
        Assert.Null(spiel.HaltestelleBei(spiel.FigurX, spiel.FigurZ));
    }
}

/// <summary>
/// Welle A 0.73.0 (Weisung P12-239 Abschnitt 5): Belastungsprobe der Busfahrt im Spiel. Jede Aktion der Liste
/// <see cref="Aktionen"/> (darunter „Einsteigen“ und „Aussteigen“) an jeder Haltestelle von Stadt 1, an den Spieltagen 1 bis 3:
/// kein Absturz, und nach jeder Aktion stimmt der Zustand. Die Belastungsproben des Werkzeugs (<c>BelastungsprobenTests</c>)
/// pruefen Bau-Aktionen mit Rueckgaengig; Ein- und Aussteigen sind Aktionen des Spiels und stehen darum hier.
/// Gegenprobe: mit abgeschalteter Sperre im Bus (Fehler mit Absicht) meldet die Probe einen Fund.
/// </summary>
public sealed class SpielMitfahrtBelastungTests
{
    /// <summary>Die Aktionen der Probe in ihrer Reihenfolge; jede gibt einen Fund zurueck oder null.</summary>
    internal static readonly (string Name, Func<StadtSpiel, Verkehr, Haltestelle, string?> Tun)[] Aktionen =
    [
        ("Hingehen", (s, v, h) =>
        {
            SpielMitfahrtTests.Hingehen(s, v, h);
            return s.WartetAn == h.Nr ? null : $"wartet an {s.WartetAn}";
        }),
        ("Warten", (s, v, h) => SpielMitfahrtTests.Bis(s, v, () => s.BusZumEinsteigen is not null, SpielMitfahrtTests.Runde(v))
            && s.Knoepfe.Any(k => k.Aktion == StadtSpiel.AktionEinsteigen) ? null : "kein Bus, kein Knopf Einsteigen"),
        ("Einsteigen", (s, v, h) =>
        {
            var bus = v.Autos.Single(a => a.Nummer == s.BusZumEinsteigen);
            var weg = bus.Weg;
            if (!s.Knopf(StadtSpiel.AktionEinsteigen))
            {
                return "Knopf wirkt nicht";
            }
            for (var i = 0; i < 600 && s.Mitfahrt == Mitfahrt.SteigtEin; i++)
            {
                SpielMitfahrtTests.Bild(s, v);
                if (s.Mitfahrt == Mitfahrt.SteigtEin && bus.Weg != weg)
                {
                    return "Bus faehrt beim Einsteigen ab";
                }
            }
            return s.SitztImBus && s.Knoepfe.Any(k => k.Aktion == StadtSpiel.AktionAussteigen) ? null : $"nicht im Bus ({s.Mitfahrt})";
        }),
        ("Antippen im Bus", (s, v, h) =>
        {
            var zeilen = s.Ablauf.Stand.Verlauf.Count;
            s.GeheZu(h.Haeuschen.X + 2, h.Haeuschen.Y + 2);
            return s.Geht || s.Ablauf.Stand.Verlauf.Count != zeilen ? "Antippen im Bus: Figur geht los" : null;
        }),
        ("Ortsliste im Bus", (s, v, h) =>
        {
            var zeilen = s.Ablauf.Stand.Verlauf.Count;
            var ort = s.Szene.Orte[0].Id;
            var a = s.GeheZu(ort);
            var b = s.MitBusZu(ort);
            return a == StadtGrund.Ok || b == StadtGrund.Ok || s.Ablauf.Stand.Verlauf.Count != zeilen ? "Ortsliste im Bus: Weg geschrieben" : null;
        }),
        ("Aussteigen", (s, v, h) =>
        {
            if (!SpielMitfahrtTests.Bis(s, v, () => s.SitztImBus && v.Autos.Any(a => a.Nummer >= Buslinie.ErsteNummer && a.HaeltAn is null
                && Vector2.Distance(a.Lage().Ort, new Vector2(s.FigurX, s.FigurZ)) < 0.01f), 30))
            {
                return "Bus faehrt nicht ab";
            }
            if (!s.Knopf(StadtSpiel.AktionAussteigen) || !SpielMitfahrtTests.Bis(s, v, () => s.Mitfahrt == Mitfahrt.Keine, SpielMitfahrtTests.Runde(v)))
            {
                return "steigt nicht aus";
            }
            var aus = s.Szene.Haltestellen.Single(x => x.Nr == s.LetzteMitfahrt!.Value.Aus);
            return Vector2.Distance(new Vector2(s.FigurX, s.FigurZ), StadtSpiel.Wartplatz(aus)) < 0.01f && s.ImFreien ? null : "steht nicht neben der Haltestelle";
        }),
        ("Weggehen", (s, v, h) =>
        {
            var w = new Vector2(s.FigurX, s.FigurZ);
            var hier = s.Szene.Haltestellen.Single(x => x.Nr == s.WartetAn);
            var weg = w - (hier.Fahrt * 2.5f);
            s.GeheZu(weg.X, weg.Y);
            SpielMitfahrtTests.Bis(s, v, () => !s.Geht, 30);
            SpielMitfahrtTests.Bild(s, v);
            return s.Knoepfe.Any(k => k.Aktion == StadtSpiel.AktionEinsteigen) ? "Knopf Einsteigen nach dem Weggehen" : null;
        }),
    ];

    internal static List<string> Probe(long[] tage)
    {
        var funde = new List<string>();
        foreach (var tag in tage)
        {
            var spiel = SpielMitfahrtTests.Stadt1();
            foreach (var h in spiel.Szene.Haltestellen)
            {
                var v = Verkehr.FuerSpieltag(spiel.Szene, tag);
                foreach (var (name, tun) in Aktionen)
                {
                    try
                    {
                        if (tun(spiel, v, h) is { } fund)
                        {
                            funde.Add($"Tag {tag}, Haltestelle {h.Nr}, {name}: {fund}");
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        funde.Add($"Tag {tag}, Haltestelle {h.Nr}, {name}: {e.GetType().Name} {e.Message}");
                        break;
                    }
                }
            }
        }
        return funde;
    }

    [Fact]
    public void JedeAktion_JedeHaltestelle_DreiTage_OhneFund()
    {
        Assert.Contains(Aktionen, a => a.Name == "Einsteigen");
        Assert.Contains(Aktionen, a => a.Name == "Aussteigen");
        var funde = Probe([1, 2, 3]);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    [Fact]
    public void Gegenprobe_OhneSperreImBus_WirdGemeldet()
    {
        StadtSpiel.MitfahrtSperreAusFuerTests = true;
        try
        {
            Assert.Contains(Probe([1]), f => f.Contains("Antippen im Bus: Figur geht los", StringComparison.Ordinal));
        }
        finally
        {
            StadtSpiel.MitfahrtSperreAusFuerTests = false;
        }
    }
}

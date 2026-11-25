using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.79.0, Welle W3 "Inhalte SI" (Weisung P12-267 mit P12-262, Nicos Antwort 3c): 15 neue Pruefungssaetze, nur
/// angehaengt. Prueft Nicos Ziele (je Bereich fuenf neue Saetze, jeder mit eigener erfundener Firma, die Probesaetze
/// bleiben gleich), die Grenzen aus P12-265/P12-266 Abschnitt 2 (keine Aufgabenkette der Originale im gleichen
/// Zuschnitt) und den Vergleich mit der unabhaengigen Pruefung G52 (Sollwert-Aenderung Nr. 18). Jede Regel mit Gegenprobe.
/// </summary>
public class EchtInhalteW3Tests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtBestand Bestand => EchtSaetze.Laden(File.ReadAllBytes(Datei));

    /// <summary>Die 15 Saetze der Welle in der Reihenfolge der Datei (hinter den Probesaetzen alle_ap1_01 und si_b1_01).</summary>
    public static readonly string[] Neu =
    [
        "alle_ap1_02", "alle_ap1_03", "alle_ap1_04", "alle_ap1_05", "alle_ap1_06",
        "si_b1_02", "si_b1_03", "si_b1_04", "si_b1_05", "si_b1_06",
        "si_b2_01", "si_b2_02", "si_b2_03", "si_b2_04", "si_b2_05",
    ];

    /// <summary>
    /// Die eingebaute Datei nur mit den beiden Probesaetzen alle_ap1_01 und si_b1_01 (Grundlage der Regel-Tests der Wellen
    /// 1 und 2, die einen Satz verderben und genau einen Fehler erwarten; die 15 Saetze der W3 prueft diese Klasse).
    /// </summary>
    public static byte[] NurProbesaetze()
    {
        var wurzel = JsonNode.Parse(File.ReadAllBytes(Datei))!.AsObject();
        var liste = wurzel["saetze"]!.AsArray();
        for (var i = liste.Count - 1; i >= 0; i--)
        {
            if ((string?)liste[i]!["kennung"] is not ("alle_ap1_01" or "si_b1_01"))
            {
                liste.RemoveAt(i);
            }
        }
        Assert.Equal(2, liste.Count);
        return System.Text.Encoding.UTF8.GetBytes(wurzel.ToJsonString());
    }

    private static IEnumerable<EchtSatz> NeueSaetze => Bestand.Saetze.Where(s => Neu.Contains(s.Kennung));

    [Fact]
    public void W3_FuenfzehnNeueSaetze_JeBereichFuenf_ProbesaetzeBleiben()
    {
        var bestand = Bestand;
        Assert.Empty(bestand.Fehler);
        // ab A 0.84.0 folgen die AE-Saetze (EchtInhalteAeTests); die Saetze der W3 stehen unveraendert davor
        Assert.Equal(["alle_ap1_01", "si_b1_01", .. Neu], bestand.Saetze.Take(2 + Neu.Length).Select(s => s.Kennung));
        // Antwort 3c: 5 neue Saetze je Bereich (der Probesatz zaehlt nicht mit); 4a: AP1 fuer alle, AP2 der W3 nur SI
        foreach (var f in Fachrichtung.Alle)
        {
            Assert.Equal(6, bestand.Fuer(EchtSaetze.Ap1, f.Id).Count);
            Assert.Equal(f.Id == "si" ? 6 : 0, bestand.Fuer(EchtSaetze.B1, f.Id).Count(s => s.Kennung == "si_b1_01" || Neu.Contains(s.Kennung)));
            Assert.Equal(f.Id == "si" ? 5 : 0, bestand.Fuer(EchtSaetze.B2, f.Id).Count(s => Neu.Contains(s.Kennung)));
        }
        Assert.Equal(6, bestand.Fuer(EchtSaetze.B1, "si").Count);
        Assert.Equal(5, bestand.Fuer(EchtSaetze.B2, "si").Count);
        // nur angehaengt: die Probesaetze sind unveraendert (Kopf und Punkte wie vor der Welle)
        Assert.Equal("Velowerk Tannau GmbH", bestand.Satz("alle_ap1_01")!.Firma);
        Assert.Equal("Lindgrün Pflegedienst GmbH", bestand.Satz("si_b1_01")!.Firma);
    }

    [Fact]
    public void W3_JederSatzEigeneFirma_NieIhk_AP2NenntDieAufgaben()
    {
        var alle = Bestand.Saetze;
        EchtTestDatei.FirmaJeSatzOderPaar(alle);
        foreach (var s in NeueSaetze)
        {
            Assert.DoesNotContain("IHK", Alles(s), StringComparison.Ordinal);
            Assert.Contains(s.Ausgangssituation, z => z.Contains("Auszubildende", StringComparison.Ordinal));
            if (s.Bereich != EchtSaetze.Ap1)
            {
                for (var n = 0; n < 4; n++)
                {
                    Assert.Contains($"{n + 1}. {s.Aufgaben[n].Titel}", s.Ausgangssituation);
                }
            }
        }
    }

    /// <summary>P12-265/P12-266 Abschnitt 2: keine Kette der Originale im gleichen Zuschnitt (mit Gegenprobe am Probesatz).</summary>
    [Fact]
    public void W3_GrenzenDerHinweise_KeineKetteDerOriginale()
    {
        foreach (var s in NeueSaetze)
        {
            Assert.Empty(GrenzVerstoesse(s));
        }
        // Gegenprobe: der Probesatz AP2 Konzeption hat genau die Switchport-Tabelle und Engpass + RAID 6 in Aufgabe 1
        var probe = GrenzVerstoesse(Bestand.Satz("si_b1_01")!);
        Assert.Contains(probe, v => v.StartsWith("Switchport-Tabelle", StringComparison.Ordinal));
        Assert.Contains(probe, v => v.StartsWith("Aufgabe 1: mehrere nahe Teile", StringComparison.Ordinal));
    }

    [Fact]
    public void W3_ProgrammBewertetJeSatzMindestens20Punkte_UndZeichnenWoGeplant()
    {
        foreach (var s in NeueSaetze)
        {
            var programm = 100 - s.Aufgaben.SelectMany(a => a.Teile).Sum(t => t.SelbstPunkte);
            Assert.True(programm >= 20, s.Kennung + ": nur " + programm + " Punkte bewertet das Programm");
            Assert.True(s.Aufgaben.SelectMany(a => a.Teile).Select(t => t.Antwort.Form).Distinct().Count() >= 5, s.Kennung);
        }
        // gezeichnete Anlagen und Zeichenaufgaben, wo sie passen (Weisung P12-267 Abschnitt 2)
        Assert.True(NeueSaetze.Count(s => s.Aufgaben.SelectMany(a => a.Teile).Any(t => t.Antwort.Form == EchtSaetze.FormZeichnen)) >= 5);
        Assert.True(NeueSaetze.Count(s => s.Anlagen.Any(a => a.Netzplan is not null)) >= 8);
        Assert.All(NeueSaetze.Where(s => s.Bereich == EchtSaetze.B2), s => Assert.Contains(s.Anlagen, a => a.Netzplan is not null));
    }

    /// <summary>
    /// Die Oberflaeche zeigt neben einer Aufgabe nur die Anlagen, die ihre Teile nennen. Jede Anlage, die der Text einer
    /// Aufgabe erwaehnt, muss darum bei einem Teil dieser Aufgabe stehen (Fund A 0.79.0: si_b2_01 und si_b2_02 nannten den
    /// Netzplan in der Einleitung, gezeigt wurde er nicht). Gegenprobe: ohne die Verknuepfung waere der Test rot.
    /// </summary>
    [Fact]
    public void W3_JedeErwaehnteAnlageWirdBeiDerAufgabeGezeigt()
    {
        Assert.All(Bestand.Saetze, s => Assert.Empty(FehlendeAnlagen(s)));
        var satz = Bestand.Satz("si_b2_02")!;
        var a1 = satz.Aufgaben[0];
        var ohne = a1 with { Teile = [.. a1.Teile.Select(t => t with { Anlagen = [.. t.Anlagen.Where(n => n != "1")] })] };
        Assert.Equal(["si_b2_02 Aufgabe 1: Anlage 1"], FehlendeAnlagen(satz with { Aufgaben = [ohne, .. satz.Aufgaben.Skip(1)] }));
    }

    private static List<string> FehlendeAnlagen(EchtSatz s)
    {
        var fehlt = new List<string>();
        for (var n = 0; n < s.Aufgaben.Count; n++)
        {
            var a = s.Aufgaben[n];
            var text = string.Join("\n", a.Einleitung.Concat(a.Teile.SelectMany(t => t.Text)));
            var erwaehnt = Regex.Matches(text, @"Anlagen?\s+(\d+(?:\s*(?:,|und|bis|sowie)\s*\d+)*)")
                .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"\d+").Select(z => z.Value)).ToHashSet();
            var gezeigt = a.Teile.SelectMany(t => t.Anlagen).ToHashSet();
            fehlt.AddRange(erwaehnt.Where(e => !gezeigt.Contains(e)).Order().Select(e => $"{s.Kennung} Aufgabe {n + 1}: Anlage {e}"));
        }
        return fehlt;
    }

    /// <summary>Sollwert-Aenderung Nr. 18: der C#-Lader ergibt dasselbe wie die unabhaengige Pruefung in G52.</summary>
    [Fact]
    public void G52_Pruefungssaetze_GleichDerUnabhaengigenPruefung()
    {
        var soll = JsonNode.Parse(VergleichsHilfe.Lade("lerninhalte_pruefung.json").GetProperty("pruefungssaetze").GetRawText())!;
        // A 0.94.0: wie in der App mit den ausgeschlossenen Saetzen (ausschluss.json), die nicht mehr in den Sichten stehen
        var bestand = EchtTestDatei.App();
        var saetze = new JsonObject();
        foreach (var s in bestand.Saetze)
        {
            var teile = s.Aufgaben.SelectMany(a => a.Teile).ToList();
            var selbst = teile.Sum(t => t.SelbstPunkte);
            saetze[s.Kennung] = new JsonObject
            {
                ["fachrichtung"] = s.Fachrichtung,
                ["bereich"] = s.Bereich,
                ["firma"] = s.Firma,
                ["punkte"] = new JsonArray([.. s.Aufgaben.Select(a => (JsonNode)a.Punkte)]),
                ["teile"] = new JsonArray([.. s.Aufgaben.Select(a => (JsonNode)a.Teile.Count)]),
                ["anlagen"] = new JsonArray([.. s.Anlagen.Select(a => (JsonNode)a.Art)]),
                ["formen"] = Zaehlen(teile.Select(t => t.Antwort.Form)),
                ["operatoren"] = Zaehlen(teile.Select(t => t.Operator)),
                ["punkte_selbst"] = selbst,
                ["punkte_programm"] = 100 - selbst,
            };
            // A 1.8.0 (Sollwert-Aenderung Nr. 24): Saetze mit neuer Fassung nennen sie
            if (s.Fassung > 1)
            {
                saetze[s.Kennung]!["fassung"] = s.Fassung;
            }
            if (s.Aufbau is { } aufbau)
            {
                saetze[s.Kennung]!["aufbau"] = aufbau;
                saetze[s.Kennung]!["gebiete"] = new JsonObject(EchtSaetze.Gebiete.Select(g =>
                    KeyValuePair.Create(g, (JsonNode?)teile.Where(t => t.Gebiet == g).Sum(t => t.Punkte))));
            }
        }
        var sichten = new JsonObject();
        foreach (var f in Fachrichtung.Alle)
        {
            sichten[f.Id] = new JsonObject
            {
                ["ap1"] = Kennungen(bestand.Fuer(EchtSaetze.Ap1, f.Id)),
                ["b1"] = Kennungen(bestand.Fuer(EchtSaetze.B1, f.Id)),
                ["b2"] = Kennungen(bestand.Fuer(EchtSaetze.B2, f.Id)),
            };
        }
        var ist = new JsonObject
        {
            ["format"] = EchtSaetze.Format,
            ["fehler"] = Kennungen2(bestand.Fehler.Select(f => f.Kennung)),
            ["saetze"] = saetze,
            ["ausgeschlossen"] = Kennungen2(bestand.Ausgeschlossen.Order(StringComparer.Ordinal)),
            ["sichten"] = sichten,
        };
        // gleiche Reihenfolge der Saetze und gleicher Inhalt
        Assert.Equal(soll["saetze"]!.AsObject().Select(p => p.Key), saetze.Select(p => p.Key));
        Assert.True(JsonNode.DeepEquals(soll, ist), "G52 weicht ab:\nsoll " + soll.ToJsonString() + "\nist  " + ist.ToJsonString());
    }

    [Fact]
    public void G52_Gegenprobe_EinVerdorbenerSatzWaereAnders()
    {
        var wurzel = JsonNode.Parse(File.ReadAllBytes(Datei))!.AsObject();
        var liste = wurzel["saetze"]!.AsArray();
        // A 1.8.0 (Weisung P12-372): die letzten fuenf Eintraege sind neue Fassungen; der letzte Satz der ersten Fassung ist Nr. 57
        var stelle = liste.Select((s, i) => (s, i)).Last(x => x.s!["fassung"] is null).i;
        var letzter = (string)liste[stelle]!["kennung"]!;
        Assert.Equal(56, stelle);
        // A 0.94.0: der Satz gehoert zu einem Satzpaar (Aufgaben 20 bis 30 Punkte); faellt er, faellt sein Partner mit
        liste[stelle]!["aufgaben"]![0]!["punkte"] = 31;
        var bestand = EchtSaetze.Laden(System.Text.Encoding.UTF8.GetBytes(wurzel.ToJsonString()));
        Assert.Equal([letzter, EchtSaetze.Partner(letzter)], bestand.Fehler.Select(f => f.Kennung));
        // A 1.8.0: faellt eine neue Fassung, meldet die Ladepruefung sie, und die vorige Fassung bleibt (nie still, nie ohne Satz)
        var neu = JsonNode.Parse(File.ReadAllBytes(Datei))!.AsObject();
        var neuListe = neu["saetze"]!.AsArray();
        var fassung = (string)neuListe[^1]!["kennung"]!;
        neuListe[^1]!["aufgaben"]![0]!["punkte"] = 31;
        var mitFassung = EchtSaetze.Laden(System.Text.Encoding.UTF8.GetBytes(neu.ToJsonString()));
        Assert.Equal([fassung], mitFassung.Fehler.Select(f => f.Kennung));
        Assert.Equal(1, mitFassung.Satz(fassung)!.Fassung);
        var soll = VergleichsHilfe.Lade("lerninhalte_pruefung.json").GetProperty("pruefungssaetze").GetProperty("fehler");
        Assert.Equal(0, soll.GetArrayLength());
    }

    // -- Hilfen -------------------------------------------------------------------------------

    private static JsonArray Kennungen(IEnumerable<EchtSatz> saetze) => Kennungen2(saetze.Select(s => s.Kennung));

    private static JsonArray Kennungen2(IEnumerable<string> kennungen) => new([.. kennungen.Select(k => (JsonNode)k)]);

    private static JsonObject Zaehlen(IEnumerable<string> werte)
    {
        var o = new JsonObject();
        foreach (var g in werte.GroupBy(w => w).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            o[g.Key] = g.Count();
        }
        return o;
    }

    private static string Alles(EchtSatz s) => string.Join("\n", s.Ausgangssituation.Concat(s.Anlagen.SelectMany(a => a.Zeilen.Append(a.Titel)))
        .Concat(s.Aufgaben.SelectMany(a => a.Einleitung.Append(a.Titel).Concat(a.Teile.SelectMany(t => t.Text.Concat(t.Loesung))))));

    private static readonly (string Name, Regex Muster)[] Nahe =
    [
        ("Engpass", new Regex("Engpass", RegexOptions.CultureInvariant)),
        ("RAID 6 netto", new Regex("RAID 6.*(nutzbar|Kapazit)|(nutzbar|Kapazit).*RAID 6", RegexOptions.CultureInvariant)),
        ("Overhead", new Regex("Overhead|Protokollaufschlag", RegexOptions.CultureInvariant)),
    ];

    /// <summary>Verstoesse eines Satzes gegen die Hinweise P12-265/P12-266 Abschnitt 2 (leer = in Ordnung).</summary>
    private static List<string> GrenzVerstoesse(EchtSatz s)
    {
        var aus = new List<string>();
        for (var n = 0; n < s.Aufgaben.Count; n++)
        {
            var teile = s.Aufgaben[n].Teile;
            var texte = teile.Select(t => string.Join(" ", t.Text)).ToList();
            var alle = string.Join(" ", texte);
            if (alle.Contains("KiB", StringComparison.Ordinal) && alle.Contains("GiB", StringComparison.Ordinal)
                && alle.Contains("omprim", StringComparison.Ordinal))
            {
                aus.Add($"Aufgabe {n + 1}: Kette Speicherbedarf KiB, GiB, Kompression");
            }
            var nahe = Nahe.Where(m => texte.Any(t => m.Muster.IsMatch(t))).Select(m => m.Name).ToList();
            if (nahe.Count > 1)
            {
                aus.Add($"Aufgabe {n + 1}: mehrere nahe Teile ({string.Join(", ", nahe)})");
            }
            foreach (var t in teile)
            {
                if (string.Join(" ", t.Text).Contains("letzte mögliche", StringComparison.OrdinalIgnoreCase))
                {
                    aus.Add($"Aufgabe {n + 1} {t.Nr}): letzte mögliche Adresse");
                }
                if (t.Antwort.Form != EchtSaetze.FormTabelle)
                {
                    continue;
                }
                var spalten = string.Join(" ", t.Antwort.Spalten);
                if (spalten.Contains("port", StringComparison.OrdinalIgnoreCase) && (spalten.Contains("vlan", StringComparison.OrdinalIgnoreCase)
                    || spalten.Contains("tagged", StringComparison.OrdinalIgnoreCase) || spalten.Contains("rahmen", StringComparison.OrdinalIgnoreCase)))
                {
                    aus.Add($"Switchport-Tabelle in Aufgabe {n + 1} {t.Nr})");
                }
                var zellen = string.Join(" ", t.Antwort.Tabelle.SelectMany(z => z).Select(z => z.Fest ?? string.Join(" ", z.Loesung)));
                if (new[] { "fingerabdruck", "passwort", "chipkarte" }.All(w => zellen.Contains(w, StringComparison.OrdinalIgnoreCase)))
                {
                    aus.Add($"Aufgabe {n + 1} {t.Nr}): Tabelle Fingerabdruck/Passwort/Chipkarte");
                }
            }
        }
        return aus;
    }
}

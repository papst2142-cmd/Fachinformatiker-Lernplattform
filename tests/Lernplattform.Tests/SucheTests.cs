using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L8: globale Suche und Pythons str.lower() exakt wie Python v0.63.2
/// (Vergleichswerte G19, werkzeuge/vergleich/erzeuge_suche.py): Kleinschreibung jedes
/// Zeichens, Schluss-Sigma, Treffer, Reihenfolge, Ausschnitt, Ziel der Optionen-Treffer,
/// Texte. Kontrollfehler: ToLowerInvariant statt Nachbau faellt auf.
/// </summary>
public class SucheTests
{
    private static readonly JsonElement G19 = VergleichsHilfe.Lade("suche.json");
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);
    private static readonly Lazy<Suchtext> Text = new(() => new Suchtext(Inhalte));

    private static List<int> Bereiche(JsonElement liste) =>
        liste.EnumerateArray().SelectMany(b => new[] { b[0].GetInt32(), b[1].GetInt32() }).ToList();

    [Fact]
    public void KleinTabelleWiePython()
    {
        var erwartet = G19.GetProperty("klein").EnumerateArray().ToDictionary(e => e[0].GetInt32(), e => e[1].GetString()!);
        Assert.Equal(1433, erwartet.Count);
        Assert.Equal(erwartet.Count, PythonKlein.Tabelle.Count);
        foreach (var (zeichen, klein) in erwartet)
        {
            Assert.True(PythonKlein.Tabelle.TryGetValue(zeichen, out var ist) && ist == klein, $"U+{zeichen:X4}");
            // grosses Sigma rechnet die Regel fuer das Wortende; allein steht es nicht am Wortende
            Assert.Equal(klein, PythonKlein.Klein(char.ConvertFromUtf32(zeichen)));
        }
    }

    [Fact]
    public void JedesAndereZeichenBleibtGleich()
    {
        var geaendert = G19.GetProperty("klein").EnumerateArray().Select(e => e[0].GetInt32()).ToHashSet();
        for (var zeichen = 0; zeichen < 0x110000; zeichen++)
        {
            if (zeichen is >= 0xD800 and < 0xE000 || geaendert.Contains(zeichen))
            {
                continue;
            }
            var text = char.ConvertFromUtf32(zeichen);
            if (PythonKlein.Klein(text) != text)
            {
                Assert.Fail($"U+{zeichen:X4} wird veraendert");
            }
        }
    }

    [Fact]
    public void SigmaBereicheWiePython()
    {
        var (grossKlein, uebergehbar) = PythonKlein.SigmaBereiche;
        Assert.Equal(Bereiche(G19.GetProperty("sigma").GetProperty("gross_klein")), grossKlein);
        Assert.Equal(Bereiche(G19.GetProperty("sigma").GetProperty("uebergehbar")), uebergehbar);
    }

    [Fact]
    public void KleinFaelleWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G19.GetProperty("klein_faelle").EnumerateArray())
        {
            Assert.Equal(fall[1].GetString(), PythonKlein.Klein(fall[0].GetString()!));
            anzahl++;
        }
        Assert.Equal(30, anzahl);
    }

    [Fact]
    public void EinzelneErsatzzeichenBleibenStehen()
    {
        Assert.Equal("a\ud800b", PythonKlein.Klein("A\ud800B"));
        Assert.Equal("\udc00σ", PythonKlein.Klein("\udc00Σ"));
    }

    [Fact]
    public void TexteWiePython()
    {
        var anzahl = 0;
        foreach (var eintrag in G19.GetProperty("texte").EnumerateObject())
        {
            var feld = typeof(SucheTexte).GetField(eintrag.Name.Split('.')[1], BindingFlags.Public | BindingFlags.Static)!;
            Assert.True(eintrag.Value.GetString() == (string)feld.GetValue(null)!, eintrag.Name);
            anzahl++;
        }
        Assert.Equal(8, anzahl);
        var konstanten = G19.GetProperty("konstanten");
        Assert.True(konstanten.GetProperty("KIND_QUIZ").GetString() == SucheTexte.ArtQuiz);
        Assert.True(konstanten.GetProperty("SEARCH_KIND").GetString() == Optionen.SucheArt);
        Assert.True(konstanten.GetProperty("TEMPLATES_TITLE").GetString() == FarbenTexte.VorlagenTitel);
        Assert.Equal(konstanten.GetProperty("TEMPLATE_WORDS").EnumerateArray().Select(w => w.GetString()!), GlobaleSuche.VorlagenWoerter);
        Assert.True(G19.GetProperty("zeilen").GetInt32() == GlobaleSuche.Zeilen);
    }

    private static string Zeile(SuchTreffer t, string anfrage)
    {
        var zeile = string.Join(" | ", t.Art, t.Fachbereich ?? "None", t.Titel, t.Kopf, t.Ausschnitt);
        if (t.Art == Optionen.SucheArt)
        {
            var (bereich, vorlagen) = GlobaleSuche.Ziel(t.Titel, anfrage);
            zeile += " -> " + (bereich ?? "None") + (vorlagen ? " + Vorlagen" : "");
        }
        return zeile;
    }

    private static string Zeile(JsonElement z)
    {
        var fach = z.GetProperty("fachbereich");
        var zeile = string.Join(" | ", z.GetProperty("art").GetString(), fach.ValueKind == JsonValueKind.Null ? "None" : fach.GetString(),
            z.GetProperty("titel").GetString(), z.GetProperty("kopf").GetString(), z.GetProperty("ausschnitt").GetString());
        if (z.TryGetProperty("ziel", out var ziel))
        {
            zeile += " -> " + (ziel[0].ValueKind == JsonValueKind.Null ? "None" : ziel[0].GetString()) + (ziel[1].GetBoolean() ? " + Vorlagen" : "");
        }
        return zeile;
    }

    /// <summary>Alle Begriffe mit dem Suchtext <paramref name="text"/>: Liste der Abweichungen (leer = wie Python).</summary>
    private static List<string> Abweichungen(Suchtext text, Func<string, bool, List<SuchTreffer>>? optionen = null)
    {
        var abweichungen = new List<string>();
        foreach (var fall in G19.GetProperty("suche").EnumerateArray())
        {
            var begriff = fall.GetProperty("begriff").GetString()!;
            var anfrage = GlobaleSuche.Anfrage(begriff);
            var erwartetAnfrage = fall.GetProperty("anfrage").GetString()!;
            if ((anfrage ?? "") != erwartetAnfrage)
            {
                abweichungen.Add($"Anfrage zu {begriff}");
                continue;
            }
            foreach (var (geraet, pc) in new[] { ("pc", true), ("handy", false) })
            {
                var erwartet = fall.GetProperty(geraet);
                if (erwartet.ValueKind == JsonValueKind.String)
                {
                    erwartet = fall.GetProperty("pc");    // "wie_pc"
                }
                if (erwartet.ValueKind == JsonValueKind.Null)
                {
                    if (anfrage is not null)
                    {
                        abweichungen.Add($"{geraet} {begriff}: Python sucht nicht");
                    }
                    continue;
                }
                var ergebnis = GlobaleSuche.Suchen(text, anfrage!, pc, TestContext.Current.CancellationToken);
                if (optionen is not null)
                {
                    var treffer = optionen(anfrage!, pc);
                    treffer.AddRange(text.Suchen(anfrage!, TestContext.Current.CancellationToken));
                    ergebnis = new SuchErgebnis(anfrage!, treffer);
                }
                ergebnis = OhneAusbildung(ergebnis);
                if (ergebnis.Treffer.Count != erwartet.GetProperty("anzahl").GetInt32())
                {
                    abweichungen.Add($"{geraet} {begriff}: {ergebnis.Treffer.Count} statt {erwartet.GetProperty("anzahl").GetInt32()} Treffer");
                    continue;
                }
                if (ergebnis.Info != erwartet.GetProperty("info").GetString())
                {
                    abweichungen.Add($"{geraet} {begriff}: Info {ergebnis.Info}");
                }
                var weitere = erwartet.GetProperty("weitere");
                if (ergebnis.Weitere != (weitere.ValueKind == JsonValueKind.Null ? null : weitere.GetString()))
                {
                    abweichungen.Add($"{geraet} {begriff}: weitere {ergebnis.Weitere}");
                }
                var soll = erwartet.GetProperty("zeilen").EnumerateArray().Select(Zeile).ToList();
                var ist = ergebnis.Gezeigt.Select(t => Zeile(t, anfrage!)).ToList();
                if (!soll.SequenceEqual(ist))
                {
                    abweichungen.Add($"{geraet} {begriff}:\n" + VergleichsHilfe.Unterschied(soll, ist));
                }
            }
        }
        return abweichungen;
    }

    /// <summary>
    /// Phase 4, W2: den Optionen-Bereich "Ausbildung" gibt es nur in C# (Wortliste W2 Abschnitt 7,
    /// Weisung P4-6). Ohne seinen Treffer muss alles andere wie G19 sein; wo er steht, prueft
    /// <see cref="AusbildungNurWoErPasst"/>. Ab W4 ebenso die Hilfe-Treffer: G19 enthaelt sie nicht
    /// (erzeuge_suche.py 17), sie prueft G35 (HilfeTests), ihren Platz <see cref="ReihenfolgeOptionenKartenFragenAp2Ap1"/>.
    /// </summary>
    private static SuchErgebnis OhneAusbildung(SuchErgebnis ergebnis) =>
        new(ergebnis.Anfrage, [.. ergebnis.Treffer.Where(t => !IstAusbildung(t) && !IstW8(t) && t.Art != RundgangTexte.SucheArt)]);

    private static bool IstAusbildung(SuchTreffer t) => t.Art == Optionen.SucheArt && t.Titel == PruefungTexte.AusbildungReiter;

    /// <summary>Phase 8 W8: "Musik und Geräusche", "Vollbild" und "Tastenbelegung" gibt es nur in C# (wie "Ausbildung").</summary>
    private static readonly OptionenBereich[] W8Bereiche = [Optionen.TonBereich, Optionen.VollbildBereich, Optionen.TastenBereich];

    private static bool IstW8(SuchTreffer t) => t.Art == Optionen.SucheArt && W8Bereiche.Any(b => b.Titel == t.Titel);

    /// <summary>Phase 8 W8: die drei neuen Bereiche stehen genau dort, wo Titel oder Stichwort passen, in der Reihenfolge der Optionen.</summary>
    [Fact]
    public void W8BereicheNurWoSiePassen()
    {
        var anfragen = G19.GetProperty("suche").EnumerateArray().Select(f => f.GetProperty("begriff").GetString()!)
            .Concat(["Musik", "Ton", "stumm", "Vollbild", "F11", "Tastatur", "Tasten", "Geräusche"]);
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var begriff in anfragen)
        {
            if (GlobaleSuche.Anfrage(begriff) is not { } anfrage)
            {
                continue;
            }
            foreach (var pc in new[] { true, false })
            {
                var treffer = GlobaleSuche.Suchen(Text.Value, anfrage, pc, TestContext.Current.CancellationToken).Treffer;
                var nadel = PythonKlein.Klein(Lernregeln.Strip(anfrage));
                foreach (var bereich in W8Bereiche)
                {
                    var passt = nadel.Length > 0 && (PythonKlein.Klein(bereich.Titel).Contains(nadel, StringComparison.Ordinal)
                        || bereich.StichwoerterFuer(pc).Any(w => PythonKlein.Klein(w).Contains(nadel, StringComparison.Ordinal)));
                    Assert.Equal(passt ? 1 : 0, treffer.Count(t => t.Art == Optionen.SucheArt && t.Titel == bereich.Titel));
                    if (passt)
                    {
                        gesehen.Add(bereich.Id);
                    }
                }
                var optionen = treffer.SkipWhile(t => t.Art == RundgangTexte.SucheArt).TakeWhile(t => t.Art == Optionen.SucheArt).Select(t => t.Titel).ToList();
                var reihenfolge = Optionen.AlleFuer(pc).Select(b => b.Titel).ToList();
                Assert.Equal(optionen.OrderBy(reihenfolge.IndexOf), optionen);
            }
        }
        Assert.Equal(W8Bereiche.Select(b => b.Id).Order(StringComparer.Ordinal), gesehen.Order(StringComparer.Ordinal));
        Assert.Equal((Optionen.Tasten, false), GlobaleSuche.Ziel(Optionen.TastenBereich.Titel, "Tastatur"));
        Assert.DoesNotContain(Optionen.Bereiche, b => W8Bereiche.Any(w => w.Id == b.Id));      // G9 bleibt wie Python
    }

    [Fact]
    public void AusbildungNurWoErPasst()
    {
        var gesehen = 0;
        foreach (var fall in G19.GetProperty("suche").EnumerateArray())
        {
            if (GlobaleSuche.Anfrage(fall.GetProperty("begriff").GetString()!) is not { } anfrage)
            {
                continue;
            }
            foreach (var pc in new[] { true, false })
            {
                var treffer = GlobaleSuche.Suchen(Text.Value, anfrage, pc, TestContext.Current.CancellationToken).Treffer;
                var nadel = PythonKlein.Klein(Lernregeln.Strip(anfrage));
                var passt = nadel.Length > 0 && (PythonKlein.Klein(PruefungTexte.AusbildungReiter).Contains(nadel, StringComparison.Ordinal)
                    || PruefungTexte.AusbildungSuchwoerter.Any(w => PythonKlein.Klein(w).Contains(nadel, StringComparison.Ordinal)));
                Assert.Equal(passt ? 1 : 0, treffer.Count(IstAusbildung));
                if (!passt)
                {
                    continue;
                }
                gesehen++;
                // Reihenfolge wie in den Optionen: nach den Bereichen vor "Lerninhalte", vor allen Inhalten
                var optionen = treffer.SkipWhile(t => t.Art == RundgangTexte.SucheArt).TakeWhile(t => t.Art == Optionen.SucheArt).Select(t => t.Titel).ToList();
                var reihenfolge = Optionen.AlleFuer(pc).Select(b => b.Titel).ToList();
                Assert.Equal(optionen.OrderBy(reihenfolge.IndexOf), optionen);
                var detail = treffer.Single(IstAusbildung).Detail;
                Assert.StartsWith(SucheTexte.OptionenDetail, detail, StringComparison.Ordinal);
            }
        }
        Assert.True(gesehen > 0, "kein Suchbegriff aus G19 trifft \"Ausbildung\"");
        Assert.Single(GlobaleSuche.OptionenTreffer("Fachrichtung", true));
        Assert.Equal((Optionen.Ausbildung, false), GlobaleSuche.Ziel(PruefungTexte.AusbildungReiter, "Fachrichtung"));
        Assert.Equal(PruefungTexte.AusbildungReiter, Optionen.Bereich(Optionen.Ausbildung).Titel);
        Assert.DoesNotContain(Optionen.Bereiche, b => b.Id == Optionen.Ausbildung);      // G9 bleibt wie Python
    }

    [Fact]
    public void SucheWiePython()
    {
        var abweichungen = Abweichungen(Text.Value);
        Assert.True(abweichungen.Count == 0, string.Join("\n", abweichungen.Take(5)));
        Assert.Equal(68, G19.GetProperty("suche").GetArrayLength());
    }

    /// <summary>
    /// Kontrollfehler L8 (Plan): ToLowerInvariant statt des Nachbaus faellt in den Faellen
    /// von G19 auf ("İ" und das Schluss-Sigma). Auf den heutigen Inhalten finden beide
    /// dieselben Treffer (keines der Zeichen kommt dort vor); die Suche bleibt trotzdem
    /// beim Nachbau, damit neue Inhalte und andere Geraete nichts aendern.
    /// </summary>
    [Fact]
    public void ToLowerInvariantWaereFalsch()
    {
        var falsch = G19.GetProperty("klein_faelle").EnumerateArray()
            .Where(f => f[0].GetString()!.ToLowerInvariant() != f[1].GetString()).Select(f => f[0].GetString()).ToList();
        Assert.Contains("İstanbul", falsch);
        Assert.Contains("ΟΔΟΣ ΚΑΙ ΟΔΟΣ", falsch);
        Assert.Contains("ΑΣ.", falsch);
        Assert.Empty(Abweichungen(new Suchtext(Inhalte, PythonKlein.Klein)));
    }

    [Fact]
    public void ReihenfolgeOptionenKartenFragenAp2Ap1()
    {
        var ergebnis = GlobaleSuche.Suchen(Text.Value, "e", true, TestContext.Current.CancellationToken);
        var reihenfolge = new[] { RundgangTexte.SucheArt, Optionen.SucheArt, SucheTexte.ArtKarte, SucheTexte.ArtQuiz, SucheTexte.ArtAp2, SucheTexte.ArtAp1 };
        var arten = ergebnis.Treffer.Select(t => Array.IndexOf(reihenfolge, t.Art)).ToList();
        Assert.DoesNotContain(-1, arten);
        Assert.Equal(arten.Order(), arten);
        Assert.Equal(6, arten.Distinct().Count());
        Assert.Equal(GlobaleSuche.Zeilen, ergebnis.Gezeigt.Count);
    }

    [Fact]
    public void LeererBegriffSuchtNicht()
    {
        Assert.Null(GlobaleSuche.Anfrage(""));
        Assert.Null(GlobaleSuche.Anfrage(" \t 　"));
        Assert.Null(GlobaleSuche.Anfrage(null));
        Assert.Equal("RAID", GlobaleSuche.Anfrage("  RAID\n"));
        Assert.Empty(GlobaleSuche.OptionenTreffer("   ", true));
    }

    [Fact]
    public void AbgebrocheneSucheEndetSofort()
    {
        using var abbruch = new CancellationTokenSource();
        abbruch.Cancel();
        Assert.Throws<OperationCanceledException>(() => GlobaleSuche.Suchen(Text.Value, "Subnetz", true, abbruch.Token));
        Assert.Throws<OperationCanceledException>(() => Text.Value.Suchen("e", abbruch.Token));
    }

    [Fact]
    public void SuchtextZaehltJedenAufbau()
    {
        var vorher = Suchtext.Aufbauten;
        var text = new Suchtext(Inhalte);
        Assert.Equal(vorher + 1, Suchtext.Aufbauten);
        _ = GlobaleSuche.Suchen(text, "RAID", true, TestContext.Current.CancellationToken);
        _ = GlobaleSuche.Suchen(text, "Subnetz", false, TestContext.Current.CancellationToken);
        Assert.Equal(vorher + 1, Suchtext.Aufbauten);
        Assert.Equal(Inhalte.Karteikarten.Count + Inhalte.Quizfragen.Count + Inhalte.Ap2Szenarien.Count + Inhalte.Ap1Szenarien.Count, text.Anzahl);
    }

    /// <summary>Messpunkt Plan 11: Suche "Subnetz" bis zu den Treffern (Grenze 400 ms, hier nur Rechenzeit).</summary>
    [Fact]
    public void SucheSubnetzUnterGrenze()
    {
        var text = Text.Value;
        _ = GlobaleSuche.Suchen(text, "Subnetz", true, TestContext.Current.CancellationToken);
        var uhr = Stopwatch.StartNew();
        var ergebnis = GlobaleSuche.Suchen(text, "Subnetz", true, TestContext.Current.CancellationToken);
        var ms = uhr.Elapsed.TotalMilliseconds;
        Assert.Equal(86, ergebnis.Treffer.Count(t => t.Art != RundgangTexte.SucheArt));
        Assert.Equal(1, ergebnis.Treffer.Count(t => t.Art == RundgangTexte.SucheArt));   // W4: Hilfe "Rechner"
        Assert.True(ms < 400, ms.ToString("F1", CultureInfo.InvariantCulture) + " ms");
    }
}

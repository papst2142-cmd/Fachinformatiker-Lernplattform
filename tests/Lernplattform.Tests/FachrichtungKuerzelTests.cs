using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.14.0 (Weisung P12-400 mit P12-402/403, Nico 26.11.2026 Karte b, Sammelliste 466): sichtbare Kuerzel der Fachrichtung
/// wie bei der IHK (FISI, FIAE, FIDP, FIDV); die Kennungen si/ae/dpa/dv bleiben intern. Abnahmesaetze K-1 bis K-3 der Meldung P12-259.
/// </summary>
public sealed partial class FachrichtungKuerzelTests
{
    /// <summary>Altes Kuerzel als eigenes Wort (auch „AE-Prüfungssatz“); „DPAPI“ oder „FISI“ zaehlen nicht.</summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}_])(SI|AE|DPA|DV)(?![\p{L}\p{N}_])")]
    private static partial Regex AltesKuerzel();

    /// <summary>Zeichenketten im Quelltext (auch $"…" und @"…"), ohne Kommentare.</summary>
    [GeneratedRegex(@"\$?@?""(?:[^""\\\n]|\\.)*""")]
    private static partial Regex Zeichenkette();

    /// <summary>
    /// Feste, begruendete Ausnahmen: sichtbare Texte, in denen das Wort KEINE Fachrichtung meint. Heute leer; ein neuer Eintrag
    /// braucht den Grund im Kommentar daneben.
    /// </summary>
    private static readonly HashSet<string> Ausnahmen = [];

    private static IEnumerable<string> Funde(string text) =>
        AltesKuerzel().Matches(text).Select(m => m.Value);

    // -- K-1: Kuerzel je Fachrichtung -----------------------------------------------------------------------------------------

    [Fact]
    public void K1_Kuerzel_JeFachrichtung_WieBeiDerIhk()
    {
        Assert.Equal(["FISI", "FIAE", "FIDP", "FIDV"], new[] { "si", "ae", "dpa", "dv" }.Select(FachrichtungTexte.KuerzelFuer));
        Assert.Equal(["FISI", "FIAE", "FIDP", "FIDV"], new[] { "si", "ae", "dpa", "dv" }.Select(PaketTexte.Fr));
        Assert.Equal(
            ["Systemintegration (FISI)", "Anwendungsentwicklung (FIAE)", "Daten- und Prozessanalyse (FIDP)", "Digitale Vernetzung (FIDV)"],
            PruefungTexte.Fachrichtungen.Select(f => f.Text));
        // die Werte (Kennungen) bleiben, Reihenfolge wie Pruefung.Ausbildung.Werte
        Assert.Equal(["si", "ae", "dpa", "dv"], PruefungTexte.Fachrichtungen.Select(f => f.Wert));
        Assert.Equal(["si", "ae", "dpa", "dv"], FachrichtungTexte.Kuerzel.Keys);
        Assert.Equal("Fachrichtungs-Paket FIDP", PaketTexte.Format(PaketTexte.FrName, PaketTexte.Fr("dpa")));
        Assert.Equal("Für FIDP fehlt das Fachrichtungs-Paket. Du siehst die gemeinsamen Inhalte. Dein Lernstand bleibt.",
            PaketTexte.Format(PaketTexte.FrFehltSicht, PaketTexte.Fr("dpa")));
        // eine unbekannte Kennung bleibt sichtbar (nie still leer)
        Assert.Equal("XY", FachrichtungTexte.KuerzelFuer("xy"));
        // Antwort 4a (P12-402): die Suche nach dem Kuerzel findet die Fachrichtungs-Wahl
        Assert.All(FachrichtungTexte.Kuerzel.Values, k => Assert.Contains(k, PruefungTexte.AusbildungSuchwoerter));
    }

    // -- K-2: kein altes Kuerzel in sichtbaren Texten ---------------------------------------------------------------------------

    [Fact]
    public void K2_SichtbareTexte_OhneAltesKuerzel()
    {
        var texte = SichtbareTexte();
        // Gegenprobe: die Sammlung greift (A 1.14.0: 2 299 Texte) und enthaelt die Stellen mit dem Kuerzel
        Assert.True(texte.Count > 2_000, "zu wenige Texte gesammelt: " + texte.Count);
        Assert.Contains("Daten- und Prozessanalyse (FIDP)", texte);
        Assert.Contains("Fachrichtungs-Paket FIDV", texte);
        Assert.Contains(HilfeTexteBeispiel(), texte);
        var funde = texte.Where(t => !Ausnahmen.Contains(t) && Funde(t).Any()).ToList();
        Assert.True(funde.Count == 0, "altes Kuerzel in sichtbaren Texten:\n" + string.Join("\n", funde));
    }

    [Fact]
    public void K2_Quelltext_OhneAltesKuerzel_InZeichenketten()
    {
        var funde = QuelltextFunde(RepoOrdner());
        Assert.True(funde.Count == 0, "altes Kuerzel in Zeichenketten:\n" + string.Join("\n", funde));
    }

    [Fact]
    public void K2_Gegenprobe_AlteTexteWaerenRot()
    {
        // die Texte bis A 1.13.0: jeder muss auffallen
        string[] alt =
        [
            "Daten- und Prozessanalyse (DPA)", "Systemintegration (SI)", "Anwendungsentwicklung (AE)", "Digitale Vernetzung (DV)",
            "Für DPA fehlt das Fachrichtungs-Paket.", "Du schreibst gerade einen AE-Prüfungssatz.",
            "\"aufbau\" kennt nur \"paar\" und gilt nur für AE Bereich 1 und 2",
            "Lernstand bereinigt: 3 alte Einträge aus Klausuren von AE, DPA oder DV",
        ];
        Assert.All(alt, t => Assert.NotEmpty(Funde(t)));
        // und was KEIN Fund ist
        string[] gut = ["FISI", "FIAE", "FIDP", "FIDV", "DPAPI", "Fachrichtungs-Paket FIDP", "AES-256", "DVD", "SIEM"];
        Assert.All(gut, t => Assert.Empty(Funde(t)));
        // die Quelltext-Pruefung findet einen alten Text in einer Zeichenkette, nie im Kommentar
        var tmp = Directory.CreateTempSubdirectory("kuerzel_gegenprobe_").FullName;
        try
        {
            var ordner = Path.Combine(tmp, "src", "Lernplattform.Core");
            Directory.CreateDirectory(ordner);
            File.WriteAllText(Path.Combine(ordner, "A.cs"),
                "// Kommentar mit DPA\nclass A { const string T = \"Für DPA fehlt\"; /// <summary>SI</summary>\n}\n");
            Assert.Equal(["src/Lernplattform.Core/A.cs:2: \"Für DPA fehlt\""], QuelltextFunde(tmp));
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }

    // -- K-3: gespeicherte Kennungen bleiben -----------------------------------------------------------------------------------

    [Fact]
    public void K3_Bereinigung_Texte_MitNeuenKuerzeln()
    {
        foreach (var text in new[] { Bereinigung.Texte.Berichtigt(3, "k"), Bereinigung.Texte.NichtGespeichert(3, "k"), Bereinigung.Texte.KopieFehlt(3) })
        {
            Assert.Contains("Klausuren von FIAE, FIDP oder FIDV", text, StringComparison.Ordinal);
            Assert.Empty(Funde(text));
        }
    }

    /// <summary>
    /// Weisung P12-400 Punkt 2 (Gegenprobe): ein Lernstand, in dem die Fachrichtung wie bis A 1.13.0 als „dpa“ steht, laedt
    /// ohne Uebernahme weiter als dpa und zeigt FIDP; gespeichert wird weiter die Kennung, nie das Kuerzel.
    /// </summary>
    [Fact]
    public void K3_AlterLernstandMitDpa_LaedtUndZeigtFidp()
    {
        using var ordner = new TempOrdner();
        var datei = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var jetzt = new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
        var alt = new LernstandSchreiber(new Lernstand(datei), SpeichernTests.FesteUhr(() => jetzt));
        Assert.True(alt.ProjektFeldSpeichern(Core.Pruefung.Ausbildung.Zeile, Core.Pruefung.Ausbildung.Feld, "dpa"));   // wie A 1.13.0
        var neu = new LernstandSchreiber(new Lernstand(datei), SpeichernTests.FesteUhr(() => jetzt));
        var wert = Core.Pruefung.Ausbildung.Aktuell(neu);
        Assert.Equal("dpa", wert);
        Assert.Equal("Daten- und Prozessanalyse (FIDP)", PruefungTexte.Fachrichtungen.Single(f => f.Wert == wert).Text);
        Assert.Equal("FIDP", PaketTexte.Fr(wert));
        // Waehlen speichert die Kennung, nie das Kuerzel
        Assert.False(Core.Pruefung.Ausbildung.Waehlen(neu, "FIDP"));
        jetzt = jetzt.AddMinutes(1);
        Assert.True(Core.Pruefung.Ausbildung.Waehlen(neu, "dv"));
        Assert.Equal("dv", neu.ProjektFelder()[Core.Pruefung.Ausbildung.Zeile][Core.Pruefung.Ausbildung.Feld]);
    }

    [Fact]
    public void K3_Paketkennungen_Bleiben()
    {
        Assert.Equal(["ae", "dpa", "dv"], FachrichtungsPaket.Fachrichtungen);
        Assert.Equal(["lernen_ae", "lernen_dpa", "lernen_dv"], FachrichtungsPaket.Fachrichtungen.Select(FachrichtungsPaket.Kennung));
    }

    /// <summary>Ein Text aus der Hilfe (Namensraum Hilfe), damit die Sammlung nachweislich auch dort liest.</summary>
    private static string HilfeTexteBeispiel() => Core.Hilfe.HilfeTexte.Abschnitte[0].Titel;

    // -- Sammeln ---------------------------------------------------------------------------------------------------------------

    /// <summary>Alle Werte der Texte-Klassen im Core (Namensraum Texte und Hilfe, jede Klasse „…Texte“, auch verschachtelt).</summary>
    internal static List<string> SichtbareTexte()
    {
        var ergebnis = new HashSet<string>(StringComparer.Ordinal);
        var gesehen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var typ in typeof(PaketTexte).Assembly.GetTypes())
        {
            var ns = typ.Namespace ?? "";
            if (!(ns == "Lernplattform.Core.Texte" || ns == "Lernplattform.Core.Hilfe" || typ.Name.EndsWith("Texte", StringComparison.Ordinal))
                || typ.IsGenericTypeDefinition || typ.Name.Contains('<'))
            {
                continue;
            }
            foreach (var feld in typ.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (feld.Name.Contains('<'))
                {
                    continue;
                }
                object? wert;
                try
                {
                    wert = feld.IsLiteral ? feld.GetRawConstantValue() : feld.GetValue(null);
                }
                catch (TargetInvocationException)
                {
                    continue;
                }
                Sammeln(wert, ergebnis, gesehen, 0);
            }
        }
        // zusammengesetzte Texte mit dem Kuerzel
        foreach (var fr in new[] { "si", "ae", "dpa", "dv" })
        {
            foreach (var vorlage in new[] { PaketTexte.FrName, PaketTexte.FrText, PaketTexte.FrFehltSicht, PaketTexte.FrFehltOhneNetz,
                PaketTexte.FrEntfernenGesperrt, PaketTexte.FrEntfernenFrage, PaketTexte.FrEntfernenFrageOhne, PaketTexte.FrSatzFehlt, PaketTexte.FrNachAbgleich })
            {
                ergebnis.Add(PaketTexte.Format(vorlage, PaketTexte.Fr(fr)));
            }
        }
        ergebnis.Add(Bereinigung.Texte.Berichtigt(1, "k"));
        return [.. ergebnis];
    }

    private static void Sammeln(object? wert, HashSet<string> ziel, HashSet<object> gesehen, int tiefe)
    {
        if (wert is null || tiefe > 6)
        {
            return;
        }
        if (wert is string s)
        {
            ziel.Add(s);
            return;
        }
        var typ = wert.GetType();
        if (typ.IsPrimitive || typ.IsEnum || wert is decimal || wert is Delegate || wert is Type || wert is Regex)
        {
            return;
        }
        if (!typ.IsValueType && !gesehen.Add(wert))
        {
            return;
        }
        if (wert is IDictionary woerterbuch)
        {
            foreach (DictionaryEntry e in woerterbuch)
            {
                Sammeln(e.Key, ziel, gesehen, tiefe + 1);
                Sammeln(e.Value, ziel, gesehen, tiefe + 1);
            }
            return;
        }
        if (wert is IEnumerable folge)
        {
            foreach (var x in folge)
            {
                Sammeln(x, ziel, gesehen, tiefe + 1);
            }
            return;
        }
        if (wert is ITuple tupel)
        {
            for (var i = 0; i < tupel.Length; i++)
            {
                Sammeln(tupel[i], ziel, gesehen, tiefe + 1);
            }
            return;
        }
        if (typ.Namespace?.StartsWith("Lernplattform", StringComparison.Ordinal) == true)
        {
            foreach (var p in typ.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
            {
                Sammeln(p.GetValue(wert), ziel, gesehen, tiefe + 1);
            }
        }
    }

    /// <summary>
    /// Zeichenketten in src/Lernplattform.Core und src/Lernplattform.App (auch Vorlesen-Namen), ohne Kommentare und ohne den
    /// Start-Test (src/Lernplattform.App/Messung: Messpunkt-Namen bleiben, Weisung P12-402 Antwort 1a, nur CI-Protokolle).
    /// </summary>
    internal static List<string> QuelltextFunde(string wurzel)
    {
        var funde = new List<string>();
        foreach (var projekt in new[] { "Lernplattform.Core", "Lernplattform.App", "Lernplattform.Engine", "Lernplattform.Desktop", "Lernplattform.Android" })
        {
            var ordner = Path.Combine(wurzel, "src", projekt);
            if (!Directory.Exists(ordner))
            {
                continue;
            }
            foreach (var datei in Directory.EnumerateFiles(ordner, "*.*", SearchOption.AllDirectories)
                .Where(d => d.EndsWith(".cs", StringComparison.Ordinal) || d.EndsWith(".axaml", StringComparison.Ordinal))
                .OrderBy(d => d, StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(wurzel, datei).Replace('\\', '/');
                if (rel.Contains("/obj/", StringComparison.Ordinal) || rel.Contains("/bin/", StringComparison.Ordinal)
                    || rel.StartsWith("src/Lernplattform.App/Messung/", StringComparison.Ordinal))
                {
                    continue;
                }
                var zeilen = File.ReadAllLines(datei);
                for (var i = 0; i < zeilen.Length; i++)
                {
                    var zeile = zeilen[i];
                    var ohneEinzug = zeile.TrimStart();
                    if (ohneEinzug.StartsWith("//", StringComparison.Ordinal) || ohneEinzug.StartsWith('*'))
                    {
                        continue;
                    }
                    foreach (Match m in Zeichenkette().Matches(zeile))
                    {
                        // Kommentar hinter dem Code: Zeichenketten danach zaehlen nicht
                        var kommentar = zeile.IndexOf("//", StringComparison.Ordinal);
                        if (kommentar >= 0 && kommentar < m.Index && !Zeichenkette().Matches(zeile).Any(z => z.Index < kommentar && z.Index + z.Length > kommentar))
                        {
                            break;
                        }
                        if (Funde(m.Value).Any() && !Ausnahmen.Contains(m.Value))
                        {
                            funde.Add(rel + ":" + (i + 1) + ": " + m.Value);
                        }
                    }
                }
            }
        }
        return funde;
    }

    private static string RepoOrdner()
    {
        var ordner = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(ordner, "Directory.Build.props")))
        {
            ordner = Path.GetDirectoryName(ordner) ?? throw new InvalidOperationException("Repo nicht gefunden");
        }
        return ordner;
    }
}

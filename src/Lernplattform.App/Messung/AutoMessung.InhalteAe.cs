using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Inhalte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.97.0 (Weisung P12-312 Punkt 5, Nico 3a): neuer Messpunkt „Inhalte laden AE“, einmalig und streng 400 ms wie
/// „Inhalte laden“ (SI, unveraendert). Gemessen wird ein eigener, kalter Programmstart mit der Fachrichtung AE im Lernstand: der
/// Start-Test startet dasselbe Programm noch einmal mit frischem Datenordner (Lernstand mit AE, wie nach der Wahl in den
/// Optionen) und dem Teil <see cref="TeilInhalteAe"/>; dieser misst nur „Inhalte laden“ (derselbe Weg wie beim echten Start)
/// und endet. So misst der Punkt den ersten Start eines AE-Nutzers, nicht ein zweites Laden im schon warmen Prozess (das waere
/// rund die Haelfte und damit eine Lockerung).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Wert von LERNPLATTFORM_MESSUNG_TEIL fuer den Programmstart, der nur „Inhalte laden“ mit AE misst.</summary>
    public const string TeilInhalteAe = "inhalte_ae";

    private const string InhalteAeZeile = "INHALTE_AE ";

    /// <summary>Hoechstens so lange wartet der Start-Test auf den zweiten Programmstart (der Wachhund des Laufs bleibt).</summary>
    private static readonly TimeSpan InhalteAeFrist = TimeSpan.FromMinutes(4);

    /// <summary>Im zweiten Programmstart: nur „Inhalte laden“ abwarten und Wert, Sicht und Teilzeiten notieren.</summary>
    private static async Task<int> InhalteAeTeilAsync()
    {
        var inhalte = await Zustand.Inhalte;
        var ms = Zustand.InhalteLadenMs;
        var inv = CultureInfo.InvariantCulture;
        Messwerte.Notiere(string.Create(inv, $"{InhalteAeZeile}{ms ?? -1:F3}|{Zustand.Sicht}|{Zustand.Eingebaut.MitTeilen}|{Zustand.InhalteLadenTeile}"));
        // Welle A 1.5.0: AE kommt aus dem Paket im Datenordner (die App selbst traegt nur Platzhalter)
        var gut = ms is not null && inhalte is not null && Zustand.Sicht == "ae" && !Zustand.Aktuell.Fehlt("ae");
        Messwerte.Notiere(gut ? "URTEIL: gruen" : "URTEIL: rot (Inhalte laden AE)");
        return gut ? 0 : 1;
    }

    /// <summary>Im Start-Test (Lernteil): den zweiten Programmstart mit AE ausfuehren und den Messpunkt werten.</summary>
    private static async Task<int> InhalteLadenAeAsync()
    {
        const string titel = "Inhalte laden AE (bisherige + AE, kalt, einmalig, eigener Programmstart)";
        var inv = CultureInfo.InvariantCulture;
        int Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: " + titel + ": " + text);
            return 1;
        }
        if (!Zustand.Eingebaut.MitTeilen)
        {
            return Fund("das Programm hat keine aufgeteilten Lern-Dateien (lernen_teile)");
        }
        var ordner = Directory.CreateTempSubdirectory("lernplattform_inhalte_ae_").FullName;
        var datei = Path.Combine(ordner, "messung_inhalte_ae.txt");
        // Lernstand mit Fachrichtung AE (gespeichert wie die Wahl in den Optionen)
        var gewaehlt = Core.Pruefung.Ausbildung.Waehlen(new LernstandSchreiber(new Lernstand(Path.Combine(ordner, Lernstand.DateiName))), "ae");
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (!gewaehlt)
        {
            return Fund("Fachrichtung AE im Lernstand nicht gespeichert");
        }
        // Welle A 1.5.0 (Weisung P12-358): AE kommt nur als Paket; der Datenordner traegt es wie bei einem Nutzer nach „Laden“
        if (EinsetzenAus(new Core.Pakete.PaketAblage(ordner, Zustand.Eingebaut), Core.Pakete.FachrichtungsPaket.Kennung("ae")) is { } grund)
        {
            return Fund("Fachrichtungs-Paket AE nicht eingesetzt: " + grund);
        }
        if (Environment.ProcessPath is not { } programm)
        {
            return Fund("Pfad des Programms unbekannt");
        }
        var start = new ProcessStartInfo(programm) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(programm), "dotnet", StringComparison.OrdinalIgnoreCase)
            && System.Reflection.Assembly.GetEntryAssembly()?.Location is { Length: > 0 } dll)
        {
            start.ArgumentList.Add(dll);
        }
        start.Environment["LERNPLATTFORM_DATEN"] = ordner;
        start.Environment["LERNPLATTFORM_MESSUNG_AUTO"] = "1";
        start.Environment["LERNPLATTFORM_MESSUNG_TEIL"] = TeilInhalteAe;
        start.Environment["LERNPLATTFORM_MESSUNG_DATEI"] = datei;
        var uhr = Stopwatch.StartNew();
        using var prozess = Process.Start(start);
        if (prozess is null)
        {
            return Fund("zweiter Programmstart nicht moeglich");
        }
        var ausgabe = prozess.StandardOutput.ReadToEndAsync();
        var fehler = prozess.StandardError.ReadToEndAsync();
        using var frist = new CancellationTokenSource(InhalteAeFrist);
        try
        {
            await prozess.WaitForExitAsync(frist.Token);
        }
        catch (OperationCanceledException)
        {
            prozess.Kill(true);
            return Fund(string.Create(inv, $"zweiter Programmstart nach {InhalteAeFrist.TotalMinutes:F0} Minuten nicht beendet"));
        }
        await Task.WhenAll(ausgabe, fehler);
        var zeilen = File.Exists(datei) ? File.ReadAllLines(datei) : [];
        var zeile = zeilen.FirstOrDefault(z => z.StartsWith(InhalteAeZeile, StringComparison.Ordinal));
        // ein Ladefehler (z. B. fehlende Teile, dann nur die bisherigen Inhalte) waere sonst ein falsches Gruen
        if (zeilen.FirstOrDefault(z => z.Contains("FEHLER", StringComparison.Ordinal)) is { } ladefehler)
        {
            return Fund("zweiter Programmstart meldet: " + ladefehler);
        }
        Messwerte.Notiere(string.Create(inv, $"Inhalte laden AE: zweiter Programmstart Rueckgabe {prozess.ExitCode} nach {uhr.Elapsed.TotalSeconds:F1} s"));
        if (zeile is null)
        {
            var rest = (await fehler).Trim();
            return Fund("kein Messwert (Rueckgabe " + prozess.ExitCode.ToString(inv) + (rest.Length > 0 ? ", " + rest[..Math.Min(rest.Length, 300)] : "") + ")");
        }
        var teile = zeile[InhalteAeZeile.Length..].Split('|', 4);
        if (prozess.ExitCode != 0 || teile.Length < 4 || teile[1] != "ae" || teile[2] != bool.TrueString
            || !double.TryParse(teile[0], NumberStyles.Float, inv, out var ms) || ms < 0)
        {
            return Fund("zweiter Programmstart ungueltig: " + zeile);
        }
        return Ergebnis(titel, Messwiederholung.Einmal(ms, Geraetegrenze), teile[3]);
    }
}

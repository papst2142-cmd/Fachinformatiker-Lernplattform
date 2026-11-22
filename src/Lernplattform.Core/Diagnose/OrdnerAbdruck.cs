using System.Security.Cryptography;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Welle „Ablauf stabil“ (Weisung P12-339, Sammelliste 413): Abdruck eines Datenordners fuer Proben „Datenordner wie vorher“.
/// Die Diagnose-Datei <see cref="Haenger.LogName"/> zaehlt im Abdruck nicht mit: macOS drosselt ein verdecktes Fenster
/// (App Nap), die Haenger-Diagnose schreibt dann eine Zeile mit <see cref="Haenger.VerdecktZusatz"/> nur in haenger.log
/// (Sammelliste 204). Im Start-Test der Paket-Probe (Laeufe 1053, 1081, 1082, 1121, 1147 am Mac) war genau das der
/// einzige Unterschied. Nichts wird versteckt: <see cref="NeueHaengerEintraege"/> liefert jeden neuen Eintrag, und
/// <see cref="NurVerdeckt"/> ist nur wahr, wenn jeder neue Eintrag den Zusatz „verdeckt“ traegt (eine echte Blockade im
/// sichtbaren Fenster bleibt rot).
/// </summary>
public static class OrdnerAbdruck
{
    /// <summary>Alle Dateien und Ordner (relativ, sortiert) mit SHA-256, ohne haenger.log im Wurzelordner.</summary>
    public static string Abdruck(string ordner) => !Directory.Exists(ordner) ? "" : string.Join("\n",
        Directory.GetFileSystemEntries(ordner, "*", SearchOption.AllDirectories)
            .Select(p => (Pfad: p, Rel: Path.GetRelativePath(ordner, p)))
            .Where(p => p.Rel != Haenger.LogName)
            .OrderBy(p => p.Rel, StringComparer.Ordinal)
            .Select(p => p.Rel + (File.Exists(p.Pfad) ? " " + Convert.ToHexStringLower(SHA256.HashData(Lesen(p.Pfad))) : "/")));

    /// <summary>Inhalt von haenger.log (leer, wenn es sie nicht gibt).</summary>
    public static string HaengerText(string ordner)
    {
        var pfad = Path.Combine(ordner, Haenger.LogName);
        return File.Exists(pfad) ? System.Text.Encoding.UTF8.GetString(Lesen(pfad)) : "";
    }

    /// <summary>Eintraege („=== …“), die in <paramref name="nachher"/> neu sind (haenger.log wird nur angehaengt).</summary>
    public static IReadOnlyList<string> NeueHaengerEintraege(string vorher, string nachher)
    {
        var neu = nachher.StartsWith(vorher, StringComparison.Ordinal) ? nachher[vorher.Length..] : nachher;
        return neu.Split('\n').Where(z => z.StartsWith("=== ", StringComparison.Ordinal)).ToList();
    }

    /// <summary>Wahr, wenn jeder neue Eintrag nur „Programm verdeckt“ meldet (keine, eine oder mehrere).</summary>
    public static bool NurVerdeckt(IReadOnlyList<string> eintraege) => eintraege.All(z => z.Contains(Haenger.VerdecktZusatz, StringComparison.Ordinal));

    /// <summary>
    /// Wahr fuer einen Eintrag „=== &lt;Zeit&gt; | Abruf (Signal …): … ===“ (<see cref="Haenger.AbrufAnfang"/>). Seit A 1.3.1
    /// (Sammelliste 425) zaehlt ein Abruf in Start-Test-Proben wieder auf jedem System als Fund (<see cref="NurVerdeckt"/>): am
    /// Mac hoert das Programm nicht mehr auf USR1, das die Laufzeit selbst nutzt; die Ausnahme aus 423 (A 1.3.0) ist entfallen.
    /// </summary>
    public static bool IstAbruf(string eintrag)
    {
        var trenner = eintrag.IndexOf(" | ", StringComparison.Ordinal);
        return eintrag.StartsWith("=== ", StringComparison.Ordinal) && trenner > 0
            && eintrag.AsSpan(trenner + 3).StartsWith(Haenger.AbrufAnfang, StringComparison.Ordinal);
    }

    private static byte[] Lesen(string pfad)
    {
        // Windows: haenger.log und andere offene Dateien nur mit Mitbenutzung lesen
        using var s = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }
}

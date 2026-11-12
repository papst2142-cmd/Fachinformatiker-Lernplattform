using System.Globalization;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.47.0 W6 (Weisung P12-226, Liste P12-119 Punkt W6): Wiederherstellen-Fenster. Die Sicherungen eines Projekts
/// (die 20 Plaetze <c>sicherung_01</c> … und die Kopie vor dem Loeschen <c>geloescht_…</c>) als Liste mit Zeit; eine davon
/// laesst sich als NEUES Projekt oeffnen. Das offene Projekt wird nie ueberschrieben, nichts wird geloescht, die Sicherung
/// bleibt, wie sie ist.
/// </summary>
public sealed partial class Projekt
{
    /// <summary>Eine Sicherung: Datei, Zeit (letzte Aenderung), Kopie vor dem Loeschen?</summary>
    public sealed record Fassung(string Datei, DateTime Zeit, bool VorLoeschen);

    /// <summary>Alle Sicherungen des Projekts <paramref name="name"/>, neueste zuerst (leer, wenn es keine gibt).</summary>
    public static List<Fassung> Fassungen(Werkzeugordner ablage, string name)
    {
        var ordner = new Sicherungen(ablage, name).Ordner;
        if (!Directory.Exists(ordner))
        {
            return [];
        }
        return [.. new DirectoryInfo(ordner).GetFiles("*" + Endung)
            .Where(f => f.Name.StartsWith("sicherung_", StringComparison.Ordinal) || f.Name.StartsWith("geloescht_", StringComparison.Ordinal))
            .OrderByDescending(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => new Fassung(f.FullName, f.LastWriteTime, f.Name.StartsWith("geloescht_", StringComparison.Ordinal)))];
    }

    /// <summary>
    /// Freier Name fuer die wiederhergestellte Fassung: „&lt;Name&gt; vom TT-MM HH-MM“ (gekuerzt auf 40 Zeichen), gibt es ihn
    /// schon: „… 2“, „… 3“ … (nie ein vorhandenes Projekt).
    /// </summary>
    public static string NameAusSicherung(string name, DateTime zeit, Werkzeugordner ablage)
    {
        var zusatz = " vom " + zeit.ToString("dd-MM HH-mm", CultureInfo.InvariantCulture);
        string Gekuerzt(string ende)
        {
            var stamm = name.Length + ende.Length > 40 ? name[..Math.Max(1, 40 - ende.Length)].TrimEnd() : name;
            return stamm + ende;
        }
        var kandidat = Gekuerzt(zusatz);
        for (var n = 2; File.Exists(Path.Combine(ablage.Projekte, DateiName(kandidat) + Endung)); n++)
        {
            kandidat = Gekuerzt(zusatz + " " + n.ToString(CultureInfo.InvariantCulture));
        }
        return kandidat;
    }

    /// <summary>
    /// Die Sicherung <paramref name="sicherung"/> als neues Projekt <paramref name="name"/> anlegen und oeffnen. Die Sicherung
    /// wird zuerst wie ein Projekt geprueft (kaputt oder fremde Stadt: <see cref="StadtbauFehler"/>, nichts geschrieben); gibt
    /// es den Namen schon: IOException (nichts ueberschrieben). In der neuen Datei aendert sich nur der Name (bei einer leeren
    /// Stadt dazu ihre Kennung, die aus dem Namen folgt); Verlauf, Aenderungen und Innenraeume bleiben.
    /// </summary>
    public static Projekt AusSicherung(string sicherung, Spieldaten daten, Werkzeugordner ablage, string name)
    {
        if (!NameGueltig(name))
        {
            throw new ArgumentException("Projektname ungültig", nameof(name));
        }
        var probe = Oeffnen(sicherung, daten, ablage);
        if (probe.NurLesen)
        {
            throw new StadtbauFehler([Texte.FassungNeuer]);
        }
        var ziel = Path.Combine(ablage.Anlegen().Projekte, DateiName(name) + Endung);
        if (File.Exists(ziel))
        {
            throw new IOException("Ein Projekt mit diesem Namen gibt es schon.");
        }
        var d = (PyObjekt)Py.Lesen(File.ReadAllBytes(sicherung))!;
        Py.Setzen(d, "name", name);
        if (probe.Leer is not null)
        {
            Py.Setzen(d, "stadt", LeerStadtId(name));
        }
        var text = new StringBuilder("{\n");
        for (var i = 0; i < d.Count; i++)
        {
            text.Append(' ').Append(PythonJson.Standard(d[i].Key)).Append(": ").Append(PythonJson.Standard(d[i].Value));
            text.Append(i + 1 < d.Count ? ",\n" : "\n");
        }
        Werkzeugordner.SicherSchreiben(ziel, PythonJson.Utf8(text.Append("}\n").ToString()));
        return Oeffnen(ziel, daten, ablage);
    }
}

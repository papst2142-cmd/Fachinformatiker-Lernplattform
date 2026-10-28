using Lernplattform.Core.Diagnose;

namespace Lernplattform.Core.Spiel.Ton;

/// <summary>
/// Phase 8 W8 (Weisungen P8-17 Nico 1, P8-17b): eigene Lieder im Datenordner unter <see cref="Ordnername"/>
/// (nie im Repo, nicht in der Sicherung). "Lied hinzufügen" kopiert die gewaehlte Datei dorthin, "Entfernen"
/// loescht nur diese Kopie. Abgespielt wird der Reihe nach; eine Datei, die sich nicht lesen laesst, wird
/// uebersprungen (eine Zeile in fehler.log), nie ein Absturz. Ohne Lieder gibt es keine Musik.
/// </summary>
public sealed class EigeneLieder(string datenordner)
{
    public const string Ordnername = "musik";

    public string Ordner { get; } = Path.Combine(datenordner, Ordnername);

    private string FehlerPfad => Path.Combine(Path.GetDirectoryName(Ordner)!, Fehlerprotokoll.DateiName);

    private int _naechstes;

    public static bool Erlaubt(string name) => Tondekoder.Endungen.Contains(Path.GetExtension(name).ToLowerInvariant());

    /// <summary>Lieder (Dateinamen) in fester Reihenfolge; fehlt der Ordner, ist die Liste leer.</summary>
    public IReadOnlyList<string> Liste()
    {
        try
        {
            if (!Directory.Exists(Ordner))
            {
                return [];
            }
            return [.. Directory.EnumerateFiles(Ordner).Select(Path.GetFileName).OfType<string>().Where(Erlaubt)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal)];
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Kopie von <paramref name="quelle"/> anlegen; gleicher Name = Nummer dahinter. Rueckgabe: neuer Name.</summary>
    public string Hinzufuegen(Stream quelle, string name)
    {
        name = Path.GetFileName(name);
        if (!Erlaubt(name))
        {
            throw new NotSupportedException("Tonformat " + Path.GetExtension(name));
        }
        Directory.CreateDirectory(Ordner);
        var stamm = Path.GetFileNameWithoutExtension(name);
        var endung = Path.GetExtension(name);
        var ziel = name;
        for (var n = 2; File.Exists(Path.Combine(Ordner, ziel)); n++)
        {
            ziel = $"{stamm} ({n}){endung}";
        }
        var pfad = Path.Combine(Ordner, ziel);
        var teil = pfad + ".teil";
        try
        {
            using (var aus = new FileStream(teil, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                quelle.CopyTo(aus);
            }
            File.Move(teil, pfad);
        }
        catch
        {
            if (File.Exists(teil))
            {
                File.Delete(teil);
            }
            throw;
        }
        return ziel;
    }

    /// <summary>Nur die Kopie im Ordner loeschen; false, wenn es sie nicht (mehr) gibt.</summary>
    public bool Entfernen(string name)
    {
        var pfad = Path.Combine(Ordner, Path.GetFileName(name));
        if (!File.Exists(pfad))
        {
            return false;
        }
        File.Delete(pfad);
        return true;
    }

    /// <summary>
    /// Naechstes lesbares Lied oeffnen (der Reihe nach, danach von vorn); jedes Lied hoechstens einmal
    /// versucht. null = keine Lieder oder keins lesbar.
    /// </summary>
    public (string Name, ITonStrom Strom)? Naechstes(Func<Stream, string, ITonStrom>? oeffnen = null) => Oeffnen(oeffnen, weiterzaehlen: true);

    /// <summary>
    /// Phase 8 W9 (Nico 4b): das erste lesbare Lied der Liste fuer die Hoerprobe am Musik-Regler; kaputte werden wie
    /// bei <see cref="Naechstes"/> uebersprungen (Zeile in fehler.log). Die Reihenfolge der Musik im Spiel bleibt.
    /// </summary>
    public (string Name, ITonStrom Strom)? Erstes(Func<Stream, string, ITonStrom>? oeffnen = null) => Oeffnen(oeffnen, weiterzaehlen: false);

    private (string Name, ITonStrom Strom)? Oeffnen(Func<Stream, string, ITonStrom>? oeffnen, bool weiterzaehlen)
    {
        var liste = Liste();
        var ab = weiterzaehlen ? _naechstes : 0;
        for (var versuch = 0; versuch < liste.Count; versuch++)
        {
            var name = liste[(ab + versuch) % liste.Count];
            try
            {
                var stream = new FileStream(Path.Combine(Ordner, name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var strom = (oeffnen ?? ((s, e) => Tondekoder.Strom(s, e)))(stream, Path.GetExtension(name));
                if (weiterzaehlen)
                {
                    _naechstes = (_naechstes + versuch + 1) % liste.Count;
                }
                return (name, strom);
            }
            catch (Exception fehler)
            {
                Fehlerprotokoll.Schreiben("Ton: eigenes Lied übersprungen (" + name + "): " + fehler.GetType().Name + ": " + fehler.Message, FehlerPfad);
            }
        }
        return null;
    }
}

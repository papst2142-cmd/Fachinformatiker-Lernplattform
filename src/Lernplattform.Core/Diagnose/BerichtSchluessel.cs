using System.Text;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// BEFRISTET bis 1.0 (Weisung P11-29, Nico): eingebauter Schluessel fuer "Bericht senden" (Geheimnis
/// FEHLERBERICHT_TOKEN, darf nur in das private Repo fi-fehlerberichte schreiben). Vor 1.0 kommt ein Umbau ohne Konto
/// und ohne eingebauten Schluessel (Kopplung per QR, eigener Bericht-Briefkasten); der Test
/// BerichtVersandTests.Befristet_Bis_1_0 wird rot, sobald die Version 1.0 erreicht und diese Datei noch da ist.
///
/// Die Daten erzeugt nur der Bau (Ziel "BerichtSchluessel" in Lernplattform.Core.csproj) in die Datei
/// obj/.../BerichtSchluessel.g.cs aus der Umgebungsvariable FEHLERBERICHT_TOKEN; die setzen nur release.yml und der
/// Dispatch-Lauf "voll" der CI. Ohne sie ist die Liste leer und "Bericht senden" zeigt "nicht verfuegbar".
/// Die Maske ist KEIN Schutz (wer das Programm zerlegt, findet den Schluessel), nur gegen Zufallsfunde: kein lesbarer
/// "github_pat_..."-Text im Programm (Weisung P11-30, Frage 4a).
/// </summary>
internal static partial class BerichtSchluessel
{
    /// <summary>Maske je Stelle; der Bau rechnet genau so (Lernplattform.Core.csproj).</summary>
    internal static byte Maske(int stelle) => (byte)((0xA7 ^ (stelle * 151 + 29)) & 0xFF);

    /// <summary>Der Schluessel oder null, wenn ohne Geheimnis gebaut. Nie in ein Protokoll oder einen Text schreiben.</summary>
    public static string? Wert()
    {
        var daten = Daten;
        if (daten.Length == 0)
        {
            return null;
        }
        var roh = new byte[daten.Length];
        for (var i = 0; i < daten.Length; i++)
        {
            roh[i] = (byte)(daten[i] ^ Maske(i));
        }
        return Encoding.UTF8.GetString(roh);
    }
}

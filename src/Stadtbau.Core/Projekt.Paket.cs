using System.IO.Compression;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.57.0 (Inhaltspakete W3, Weisung P12-297 W3-1, Plan P12-175 Abschnitt 4): „Neue Stadt (Paket)“. Eine Paket-Stadt
/// ist eine leere Stadt mit dem Abschnitt <see cref="StadtPaket.Abschnitt"/> (Kennung und Name, gesetzt nur in
/// <see cref="Neu"/>); exportiert und an CC gesendet wird sie wie jede Stadt. Das Werkzeug zeigt die geschaetzte Groesse des
/// Pakets: die Stadt-Datei so, wie der Export sie schreibt, mit einem Kopf wie <c>paket_bauen.py</c> als Deflate-Archiv gepackt
/// (CC baut das echte Paket mit dem Werkzeug im Repo; die Schaetzung weicht nur um die Zeitangaben im Kopf ab).
/// </summary>
public sealed partial class Projekt
{
    /// <summary>Kennung und Name des Pakets (null = keine Paket-Stadt).</summary>
    public (string Kennung, string Name)? Paket => StadtPaket.Von(Aussen.Stadtdaten(AussenStand));

    /// <summary>Geschaetzte Groesse des Pakets in Bytes (null = keine Paket-Stadt). Blockiert kurz (Datei schreiben und packen).</summary>
    public long? PaketGroesse()
    {
        if (Paket is not { } p)
        {
            return null;
        }
        return PaketGroesseVon(Stand("2026-01-01T00:00:00+01:00").Schreiben(), p.Kennung, p.Name);
    }

    /// <summary>Groesse eines Pakets mit dieser Stadt-Datei (Kopf und Packen wie <c>werkzeuge/pakete/paket_bauen.py</c>).</summary>
    public static long PaketGroesseVon(byte[] stadtDatei, string kennung, string name)
    {
        var kopf = new PyObjekt
        {
            { "art", Lernplattform.Core.Pakete.Paket.ArtStadt }, { "braucht_app", "A 0.89.0" },
            {
                "dateien", new PyObjekt
                {
                    { Lernplattform.Core.Pakete.Paket.StadtDatei, new PyObjekt { { "groesse", (long)stadtDatei.Length }, { "sha256", new string('0', 64) } } },
                }
            },
            { "format", 1L }, { "kennung", kennung }, { "name", name }, { "stand", 1L }, { "text", "" },
        };
        using var ziel = new MemoryStream();
        using (var zip = new ZipArchive(ziel, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (datei, inhalt) in new[] { (Lernplattform.Core.Pakete.Paket.KopfDatei, Encoding.UTF8.GetBytes(PythonJson.Text(kopf) + "\n")), (Lernplattform.Core.Pakete.Paket.StadtDatei, stadtDatei) })
            {
                using var strom = zip.CreateEntry(datei, CompressionLevel.SmallestSize).Open();
                strom.Write(inhalt);
            }
        }
        return ziel.Length;
    }
}

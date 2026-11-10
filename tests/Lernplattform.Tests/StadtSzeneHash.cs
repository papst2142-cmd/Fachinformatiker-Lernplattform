using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 13 Welle 1 (Weisung P12-16): Pruefsumme einer ganzen 3D-Szene (Stadt und Innenraeume), damit "Stadt aus Daten"
/// belegt bildgleich ist. Gezaehlt wird alles, was gezeichnet wird oder das Gehen bestimmt: jede Ecke (Ort, Normale,
/// Farbe als Bitmuster, die Jahreszeit- und Nachtlicht-Marken stecken in der Farbe) und jeder Index aller Netze
/// (Stadt ohne Haeuser, jedes Haus, Kleingruen, Paket nah und fern), die Signatur, Tueren, Orte, Laternen und das
/// Laufraster (jede freie Zelle). Die Sollwerte in <see cref="StadtbauFassungTests"/> stammen vom Stand vor Welle 1
/// (main 42bbee7, Szene noch aus den Dateien und den festen Werten im Programm).
/// </summary>
internal static class StadtSzeneHash
{
    internal static string Stadt(StadtSzene szene)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var f = szene.Vorbereiten();
        Netz(h, "stadt", f.Stadt);
        foreach (var haus in szene.Haeuser)
        {
            Netz(h, "haus:" + haus.Id, f.Haeuser[haus]);
        }
        Netz(h, "gruen", f.Gruen);
        Netz(h, "paket-nah", f.PaketNah);
        Netz(h, "paket-fern", f.PaketFern);
        Text(h, "signatur:" + szene.Signatur);
        foreach (var t in szene.Tueren)
        {
            Text(h, string.Create(CultureInfo.InvariantCulture, $"tuer:{t.Ort}:{t.Mitte.X:R}:{t.Mitte.Y:R}:{t.Aussen.X:R}:{t.Aussen.Y:R}:{t.WegAnfang.X:R}:{t.WegAnfang.Y:R}:{t.Modell}:{t.Hoehe:R}:{t.Hinten:R}"));
        }
        foreach (var o in szene.Orte)
        {
            Text(h, string.Create(CultureInfo.InvariantCulture, $"ort:{o}"));
        }
        foreach (var l in szene.Laternen)
        {
            Text(h, string.Create(CultureInfo.InvariantCulture, $"laterne:{l.Nr}:{l.Ort.X:R}:{l.Ort.Y:R}:{l.Doppelt}:{l.Richtung.X:R}:{l.Richtung.Y:R}:{l.Ziel.X:R}:{l.Ziel.Y:R}"));
        }
        var raster = szene.Laufraster;
        var sb = new StringBuilder("raster:");
        for (var s = 0; s < 200; s++)
        {
            for (var z = 0; z < 200; z++)
            {
                sb.Append(raster.IstFrei((s, z)) ? '1' : '0');
            }
        }
        Text(h, sb.ToString());
        return Convert.ToHexStringLower(h.GetHashAndReset());
    }

    /// <summary>
    /// Phase 13 Welle 2 (Zusatz Chat zu Weisung P12-17): rechnerunabhaengige Pruefsumme der Stadt. Alles Ganzzahlige
    /// (Indizes und Anzahl der Ecken jedes Netzes, Tueren mit Ort und Modell, Orte, Laternen mit Nummer und Art, freie Zellen
    /// des Laufrasters) geht genau in den Hash; die Gleitkommawerte jedes Netzes gehen als Summe und Summe der Betraege
    /// (double) ein, die der Test mit einer Toleranz von 1e-6 je Ecke vergleicht. So gilt der feste Wert auch auf Rechnern,
    /// deren Vektor-Befehle im letzten Bit abweichen (GitHub), und jede echte Aenderung an Geometrie oder Ablauf faellt auf.
    /// </summary>
    /// <remarks>Welle D1: <paramref name="fern"/> = statt der Netze von nah die Fernformen (Stadt und je Haus), Rest gleich.</remarks>
    internal static (string Hash, List<(string Netz, int Werte, double Summe, double Betraege)> Summen) StadtRechnerfest(StadtSzene szene, bool fern = false)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var summen = new List<(string, int, double, double)>();
        void NetzFest(string name, Netz? netz)
        {
            Text(h, name + ":" + (netz is null ? "null" : string.Create(CultureInfo.InvariantCulture, $"{netz.EckenFeld.Length}:{netz.IndizesFeld.Length}")));
            if (netz is null)
            {
                return;
            }
            var pi = new byte[netz.IndizesFeld.Length * 4];
            for (var i = 0; i < netz.IndizesFeld.Length; i++)
            {
                BitConverter.TryWriteBytes(pi.AsSpan(i * 4), netz.IndizesFeld[i]);
            }
            h.AppendData(pi);
            double summe = 0, betraege = 0;
            foreach (var e in netz.EckenFeld)
            {
                summe += e;
                betraege += Math.Abs(e);
            }
            summen.Add((name, netz.EckenFeld.Length, summe, betraege));
        }
        var f = szene.Vorbereiten();
        if (fern)
        {
            NetzFest("stadt-fern", f.StadtFern);
            foreach (var haus in szene.Haeuser)
            {
                NetzFest("haus-fern:" + haus.Id, f.HaeuserFern![haus]);
            }
        }
        else
        {
            NetzFest("stadt", f.Stadt);
            foreach (var haus in szene.Haeuser)
            {
                NetzFest("haus:" + haus.Id, f.Haeuser[haus]);
            }
            NetzFest("gruen", f.Gruen);
            NetzFest("paket-nah", f.PaketNah);
            NetzFest("paket-fern", f.PaketFern);
        }
        foreach (var t in szene.Tueren)
        {
            Text(h, "tuer:" + t.Ort + ":" + t.Modell);
        }
        foreach (var o in szene.Orte)
        {
            Text(h, string.Create(CultureInfo.InvariantCulture, $"ort:{o}"));
        }
        foreach (var l in szene.Laternen)
        {
            Text(h, string.Create(CultureInfo.InvariantCulture, $"laterne:{l.Nr}:{l.Doppelt}:{Math.Round(l.Ort.X, 1):F1}:{Math.Round(l.Ort.Y, 1):F1}"));
        }
        var frei = 0;
        for (var s = 0; s < 200; s++)
        {
            for (var z = 0; z < 200; z++)
            {
                frei += szene.Laufraster.IstFrei((s, z)) ? 1 : 0;
            }
        }
        Text(h, string.Create(CultureInfo.InvariantCulture, $"raster:{frei}"));
        return (Convert.ToHexStringLower(h.GetHashAndReset()), summen);
    }

    internal static string Innen(Innenraum innen)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        innen.Vorbereiten();
        Netz(h, "innen", innen.NetzBauen());
        Netz(h, "ohne-waende", innen.NetzOhneWaende);
        foreach (var d in innen.DekoUmrisse)
        {
            Text(h, "deko:" + PythonJson.Standard(d.Deko));
        }
        return Convert.ToHexStringLower(h.GetHashAndReset());
    }

    private static void Netz(IncrementalHash h, string name, Netz? netz)
    {
        Text(h, name);
        if (netz is null)
        {
            Text(h, "null");
            return;
        }
        var ecken = netz.EckenFeld;
        var puffer = new byte[ecken.Length * 4];
        for (var i = 0; i < ecken.Length; i++)
        {
            BitConverter.TryWriteBytes(puffer.AsSpan(i * 4), BitConverter.SingleToInt32Bits(ecken[i]));
        }
        h.AppendData(puffer);
        var indizes = netz.IndizesFeld;
        var pi = new byte[indizes.Length * 4];
        for (var i = 0; i < indizes.Length; i++)
        {
            BitConverter.TryWriteBytes(pi.AsSpan(i * 4), indizes[i]);
        }
        h.AppendData(pi);
    }

    private static void Text(IncrementalHash h, string text) => h.AppendData(Encoding.UTF8.GetBytes(text + "\n"));
}

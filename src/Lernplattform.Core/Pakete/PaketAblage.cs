using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pakete;

/// <summary>Ein geladenes Paket im Datenordner (gueltig oder mit Grund).</summary>
public sealed record GeladenesPaket(string Kennung, long Stand, PaketKopf? Kopf, string? Grund);

/// <summary>
/// Welle A 0.87.0 (Plan P12-175 Abschnitt 2): Pakete liegen nur im Datenordner der App (nie im Installationsordner), entpackt
/// unter <c>pakete/&lt;kennung&gt;/&lt;stand&gt;/</c>. Laden geht immer erst nach <c>pakete/_laden/</c>, dann wird alles geprueft
/// und erst danach in EINEM Schritt (Umbenennen) an seinen Platz gesetzt; bricht etwas ab, bleibt der alte Stand unberuehrt.
/// Reste in <c>_laden/</c> raeumt <see cref="Aufraeumen"/> (Start der Verwaltung und Ende jedes Ladens) weg. Entfernen setzt den Ordner zuerst nach
/// <c>_laden/</c> und loescht ihn dort; leere Ordner verschwinden, der Datenordner ist danach wie vorher.
/// </summary>
public sealed class PaketAblage(string datenordner)
{
    public const string OrdnerName = "pakete";
    public const string LadenName = "_laden";

    public string Ordner { get; } = Path.Combine(datenordner, OrdnerName);

    public string Laden => Path.Combine(Ordner, LadenName);

    /// <summary>Reste eines abgebrochenen Ladens wegraeumen, leere Ordner entfernen.</summary>
    public void Aufraeumen()
    {
        try
        {
            if (Directory.Exists(Laden))
            {
                Directory.Delete(Laden, true);
            }
            if (Directory.Exists(Ordner))
            {
                foreach (var kennung in Directory.GetDirectories(Ordner).Where(d => !Directory.EnumerateFileSystemEntries(d).Any()))
                {
                    Directory.Delete(kennung);
                }
                if (!Directory.EnumerateFileSystemEntries(Ordner).Any())
                {
                    Directory.Delete(Ordner);
                }
            }
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            // eine offene Datei (Windows) bleibt bis zum naechsten Start liegen; nie ein Absturz
        }
    }

    /// <summary>Alle geladenen Pakete, je Kennung der hoechste Stand, jedes vollstaendig geprueft (nie Absturz).</summary>
    public IReadOnlyList<GeladenesPaket> Liste(string app)
    {
        var liste = new List<GeladenesPaket>();
        if (!Directory.Exists(Ordner))
        {
            return liste;
        }
        foreach (var ordner in Directory.GetDirectories(Ordner).OrderBy(d => d, StringComparer.Ordinal))
        {
            var kennung = Path.GetFileName(ordner);
            if (kennung == LadenName || !Paket.KennungGueltig(kennung))
            {
                continue;
            }
            var staende = Directory.GetDirectories(ordner)
                .Select(d => long.TryParse(Path.GetFileName(d), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0)
                .Where(n => n > 0).OrderDescending().ToList();
            if (staende.Count == 0)
            {
                continue;
            }
            var stand = staende[0];
            var ergebnis = OrdnerPruefen(Path.Combine(ordner, stand.ToString(System.Globalization.CultureInfo.InvariantCulture)), app);
            var grund = ergebnis.Grund ?? (ergebnis.Kopf!.Kennung != kennung || ergebnis.Kopf.Stand != stand
                ? PaketTexte.Format(PaketTexte.Anders, kennung, stand) : null);
            liste.Add(new GeladenesPaket(kennung, stand, grund is null ? ergebnis.Kopf : null, grund));
        }
        return liste;
    }

    private static PaketErgebnis OrdnerPruefen(string ordner, string app)
    {
        try
        {
            var dateien = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long summe = 0;
            foreach (var datei in Directory.GetFiles(ordner))
            {
                summe += new FileInfo(datei).Length;
                if (summe > Paket.HoechstGroesse)
                {
                    return new(null, PaketTexte.ZuGross);
                }
                dateien[Path.GetFileName(datei)] = File.ReadAllBytes(datei);
            }
            return Paket.Pruefen(dateien, app);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            return new(null, PaketTexte.Format(PaketTexte.Speichern, x.Message));
        }
    }

    /// <summary>Ordner fuer eine geladene Paketdatei (in <c>_laden/</c>).</summary>
    public string Ladedatei(string kennung, long stand)
    {
        Directory.CreateDirectory(Laden);
        return Path.Combine(Laden, Paket.Dateiname(kennung, stand));
    }

    /// <summary>
    /// Setzt eine fertig geladene Paketdatei ein: pruefen, entpacken nach <c>_laden/</c>, dann in einem Schritt an den Platz;
    /// aeltere Staende derselben Kennung verschwinden erst danach. Grund statt Ausnahme; bei jedem Fehler bleibt der alte Stand.
    /// </summary>
    public PaketErgebnis Einsetzen(string datei, string kennung, long stand, string app)
    {
        var neu = Path.Combine(Laden, kennung + "_" + stand);
        try
        {
            PaketErgebnis ergebnis;
            Dictionary<string, byte[]>? dateien;
            using (var strom = File.OpenRead(datei))
            {
                string? grund;
                (dateien, grund) = Paket.Entpacken(strom);
                ergebnis = dateien is null ? new(null, grund) : Paket.Pruefen(dateien, app);
            }
            if (!ergebnis.Gut)
            {
                return ergebnis;
            }
            if (ergebnis.Kopf!.Kennung != kennung || ergebnis.Kopf.Stand != stand)
            {
                return new(null, PaketTexte.Format(PaketTexte.Anders, kennung, stand));
            }
            if (Directory.Exists(neu))
            {
                Directory.Delete(neu, true);
            }
            Directory.CreateDirectory(neu);
            foreach (var (name, inhalt) in dateien!)
            {
                File.WriteAllBytes(Path.Combine(neu, name), inhalt);
            }
            var ziel = Path.Combine(Ordner, kennung, stand.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Path.Combine(Ordner, kennung));
            if (Directory.Exists(ziel))
            {
                // gleicher Stand neu geladen (z. B. nach einer kaputten Datei): alten Ordner erst beiseite, dann tauschen
                var weg = Path.Combine(Laden, "weg_" + kennung + "_" + stand);
                Directory.Move(ziel, weg);
            }
            Directory.Move(neu, ziel);
            foreach (var alt in Directory.GetDirectories(Path.Combine(Ordner, kennung)).Where(d => d != ziel))
            {
                Directory.Delete(alt, true);
            }
            return ergebnis;
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            return new(null, PaketTexte.Format(PaketTexte.Speichern, x.Message));
        }
        finally
        {
            Aufraeumen();
        }
    }

    /// <summary>Entfernt ein Paket ganz (erst beiseite, dann loeschen); true, wenn danach nichts mehr davon da ist.</summary>
    public bool Entfernen(string kennung)
    {
        if (!Paket.KennungGueltig(kennung))
        {
            return false;
        }
        var ordner = Path.Combine(Ordner, kennung);
        try
        {
            if (Directory.Exists(ordner))
            {
                Directory.CreateDirectory(Laden);
                var weg = Path.Combine(Laden, "weg_" + kennung);
                Directory.Move(ordner, weg);
            }
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            Aufraeumen();
        }
        return !Directory.Exists(ordner);
    }
}

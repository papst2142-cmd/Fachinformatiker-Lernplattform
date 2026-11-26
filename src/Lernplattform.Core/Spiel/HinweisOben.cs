namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.13.0 (Sammelliste 464, Weisung P12-394, Nico 1a 2a 3a 5a): der Hinweis des Spiels steht nicht mehr unten auf dem Weg
/// der Figur, sondern als schmale Zeile oben neben der Uhr. Ein neuer Text steht <see cref="VollDauer"/> (6 s) ganz in der Zeile
/// (fett der Titel, dahinter der Rest, so weit er passt), danach bleibt nur der Titel blass (<see cref="TitelDeckkraft"/>); geht
/// die Figur dann weiter (mindestens <see cref="WeiterAb"/>), ist die Zeile weg. Tippen, Klick oder die Taste
/// (<see cref="Probe.Tastenbelegung.Hinweis"/>) oeffnen den ganzen Text oben; noch einmal Tippen schliesst ihn (zurueck zum Titel),
/// Weitergehen schliesst ihn ganz. Nur der Ablauf, ohne Oberflaeche (Tests <c>HinweisObenTests</c>); kein Spielstand, kein
/// Ereignis. Das Hinweisfeld zum Auf- und Zuklappen mit dem Dreieck gibt es seitdem nicht mehr (Nico 5a: der gemerkte Wert
/// <c>spiel_hinweis</c> bleibt unbenutzt liegen, nichts wird geloescht oder geschrieben).
/// </summary>
public sealed class HinweisOben
{
    /// <summary>So lange steht ein neuer Text ganz in der Zeile (Nico 2a).</summary>
    public static readonly TimeSpan VollDauer = TimeSpan.FromSeconds(6);

    /// <summary>Deckkraft der Zeile, wenn nur noch der Titel steht („blass“).</summary>
    public const double TitelDeckkraft = 0.5;

    /// <summary>Weg der Figur (Felder), ab dem sie „weitergeht“ (kleiner als ein Teilschritt des freien Gehens mal drei).</summary>
    public const float WeiterAb = 0.06f;

    public enum Stufe
    {
        /// <summary>keine Zeile zu sehen</summary>
        Aus,
        /// <summary>Titel und Text, so weit er in die Zeile passt</summary>
        Voll,
        /// <summary>nur der Titel, blass</summary>
        Titel,
        /// <summary>der ganze Text oben (aufgeklappt)</summary>
        Offen,
    }

    private string _text = "";
    private TimeSpan _seit;
    private (float X, float Z) _ort;

    /// <summary>Was gerade zu sehen ist.</summary>
    public Stufe Jetzt { get; private set; } = Stufe.Aus;

    /// <summary>Der ganze Text (wie in der Tafelzeile), leer = keiner.</summary>
    public string Text => _text;

    /// <summary>Titel (erste Zeile eines mehrzeiligen Texts, sonst leer) und Rest.</summary>
    public static (string Titel, string Inhalt) Teile(string text)
    {
        var t = (text ?? "").Trim();
        var umbruch = t.IndexOf('\n', StringComparison.Ordinal);
        return umbruch < 0 ? ("", t) : (t[..umbruch].Trim(), t[(umbruch + 1)..].Trim());
    }

    /// <summary>Was in der Stufe <see cref="Stufe.Titel"/> steht: der Titel, ohne Titel der erste Satz.</summary>
    public static string Kurz(string text)
    {
        var (titel, rest) = Teile(text);
        if (titel.Length > 0)
        {
            return titel;
        }
        var zeile = rest.Split('\n')[0];
        var ende = zeile.IndexOf(". ", StringComparison.Ordinal);
        return ende < 0 ? zeile : zeile[..(ende + 1)];
    }

    /// <summary>Der Rest in einer Zeile (Umbrueche als Leerzeichen) fuer die Stufe <see cref="Stufe.Voll"/>.</summary>
    public static string RestInEinerZeile(string text) => string.Join(" ", Teile(text).Inhalt.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// Je Beschriften und Bild: <paramref name="text"/> ist die Tafelzeile, <paramref name="jetzt"/> eine fortlaufende Zeit,
    /// (<paramref name="x"/>, <paramref name="z"/>) die Stelle der Figur. Gibt true zurueck, wenn ein neuer Text kam (vorlesen).
    /// </summary>
    public bool Takt(string? text, TimeSpan jetzt, float x, float z)
    {
        var neu = (text ?? "").Trim();
        if (neu.Length == 0)
        {
            _text = "";
            Jetzt = Stufe.Aus;
            return false;
        }
        if (neu != _text)
        {
            _text = neu;
            _seit = jetzt;
            _ort = (x, z);
            Jetzt = Stufe.Voll;
            return true;
        }
        switch (Jetzt)
        {
            case Stufe.Voll when jetzt - _seit >= VollDauer:
                Jetzt = Stufe.Titel;
                _ort = (x, z);
                break;
            case Stufe.Titel or Stufe.Offen when Weg(x, z) >= WeiterAb:
                Jetzt = Stufe.Aus;
                break;
        }
        return false;
    }

    /// <summary>Tippen, Klick oder Taste: zu sehen oder weg -> ganz offen; offen -> nur der Titel. False ohne Text.</summary>
    public bool Tippen(float x, float z)
    {
        if (_text.Length == 0)
        {
            return false;
        }
        _ort = (x, z);
        Jetzt = Jetzt == Stufe.Offen ? Stufe.Titel : Stufe.Offen;
        return true;
    }

    private float Weg(float x, float z) => MathF.Sqrt(((x - _ort.X) * (x - _ort.X)) + ((z - _ort.Z) * (z - _ort.Z)));
}

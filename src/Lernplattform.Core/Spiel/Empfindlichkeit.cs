using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 445/446, Weisung P12-360 Punkte 6 und 7, Nico: „einen schalter in optionen um die empfindlichkeit
/// der Maus ein zu stellen“, „weitere empfindlichkeits regler für die Joysticks“): drei Regler in der Karte „Steuerung“, am PC
/// und am Handy gleich, je von <see cref="Min"/> bis <see cref="Max"/> Prozent in Schritten von <see cref="Schritt"/>, Vorgabe
/// <see cref="Vorgabe"/> = genau das Verhalten ohne Regler. Gespeichert wird nur ein Wert, der von der Vorgabe abweicht (Vorgabe =
/// kein Eintrag), ueber <see cref="Einstellungen.Aendern"/>; ein kaputter oder fremder Wert gilt als Vorgabe (Einstellungen.cs
/// unveraendert, wie <see cref="Blickfeldbedienung"/>).
/// </summary>
public sealed class Empfindlichkeit
{
    public const int Min = 25;

    public const int Max = 200;

    public const int Schritt = 5;

    public const int Vorgabe = 100;

    /// <summary>Maus beim Umsehen (rechte Taste ueberall, linke Taste in der Ich-Sicht): Drehen und Neigen.</summary>
    public static readonly Empfindlichkeit Maus = new("maus_empfindlichkeit");

    /// <summary>Linker Stick (Gehen): wenig Ausschlag geht schneller bzw. langsamer, nie schneller als volles Tempo.</summary>
    public static readonly Empfindlichkeit StickLinks = new("stick_links_empfindlichkeit");

    /// <summary>Rechter Stick (Umsehen): Drehen und Neigen.</summary>
    public static readonly Empfindlichkeit StickRechts = new("stick_rechts_empfindlichkeit");

    public static IReadOnlyList<Empfindlichkeit> Alle => [Maus, StickLinks, StickRechts];

    public string Schluessel { get; }

    private readonly AsyncLocal<int?> _testWert = new();

    private int _prozent = Vorgabe;

    private Empfindlichkeit(string schluessel) => Schluessel = schluessel;

    /// <summary>Wert in Prozent (setzt die Oberflaeche beim Start und nach jeder Aenderung; immer im Bereich).</summary>
    public int Prozent
    {
        get => _testWert.Value ?? _prozent;
        set => _prozent = Begrenzen(value);
    }

    /// <summary>Faktor (1 = Vorgabe).</summary>
    public double Faktor => Prozent / 100.0;

    /// <summary>Nur fuer Tests (Sammelliste 307: AsyncLocal, wirkt nur im eigenen Ablauf, nie prozessweit).</summary>
    internal IDisposable FuerTests(int prozent)
    {
        var vorher = _testWert.Value;
        _testWert.Value = Begrenzen(prozent);
        return new Zurueck(() => _testWert.Value = vorher);
    }

    /// <summary>In den Bereich legen und auf den Schritt runden.</summary>
    public static int Begrenzen(int prozent) =>
        Math.Clamp((int)Math.Round(prozent / (double)Schritt, MidpointRounding.AwayFromZero) * Schritt, Min, Max);

    /// <summary>Wert aus einstellungen.json: nur eine ganze Zahl im Bereich gilt (auf den Schritt gerundet), sonst die Vorgabe.</summary>
    public int Lesen(JsonObject werte) =>
        werte[Schluessel] is JsonValue v && v.TryGetValue<int>(out var p) && p >= Min && p <= Max ? Begrenzen(p) : Vorgabe;

    /// <summary>In einer Aenderung setzen (Vorgabe: Eintrag entfernen).</summary>
    public void Wert(JsonObject werte, int prozent)
    {
        prozent = Begrenzen(prozent);
        if (prozent == Vorgabe)
        {
            werte.Remove(Schluessel);
        }
        else
        {
            werte[Schluessel] = prozent;
        }
    }

    /// <summary>Alle drei Werte aus einstellungen.json uebernehmen (Programmstart).</summary>
    public static void AlleUebernehmen(JsonObject werte)
    {
        foreach (var e in Alle)
        {
            e.Prozent = e.Lesen(werte);
        }
    }

    /// <summary>
    /// Linker Stick: Ausschlag (vor, seitwaerts) mal Faktor, die Laenge hoechstens 1 (volles Tempo bleibt die Grenze). Bei der
    /// Vorgabe unveraendert.
    /// </summary>
    public static (double Vor, double Seit) Gehen((double Vor, double Seit) eingabe)
    {
        var f = StickLinks.Faktor;
        if (f == 1)
        {
            return eingabe;
        }
        var (v, s) = (eingabe.Vor * f, eingabe.Seit * f);
        var laenge = Math.Sqrt((v * v) + (s * s));
        return laenge > 1 ? (v / laenge, s / laenge) : (v, s);
    }

    /// <summary>Rechter Stick: Weg (dx, dy) in Punkten mal Faktor.</summary>
    public static (double Dx, double Dy) Umsehen((double Dx, double Dy) weg) => (weg.Dx * StickRechts.Faktor, weg.Dy * StickRechts.Faktor);

    private sealed class Zurueck(Action aktion) : IDisposable
    {
        public void Dispose() => aktion();
    }
}

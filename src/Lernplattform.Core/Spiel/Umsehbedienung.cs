using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.97.0 (Sammelliste 394, Weisung P12-314, Wunsch Nico): zwei Schalter in der Karte „Steuerung“, am PC und am Handy
/// gleich: „Umsehen links/rechts umkehren“ und „Umsehen oben/unten umkehren“. Standard beide aus (die Richtung nach P12-313).
/// Sie gelten fuer jedes Umsehen (E1 Chat): rechter Stick am Handy/Tablet, rechte Maustaste, in der Ich-Sicht ein Finger oder
/// die linke Maustaste (laeuft ueber dieselbe Rechnung <see cref="Kamerabedienung.RechteMaus"/>) und Q/E (nur links/rechts).
/// Gehen (linker Stick, Gehtasten), zwei Finger und Zoomen bleiben unberuehrt. Gespeichert wird nur „an“ (Standard = kein
/// Eintrag) ueber <see cref="Einstellungen.Aendern"/>; ein kaputter Wert gilt als Standard (aus).
/// </summary>
public static class Umsehbedienung
{
    public const string SchluesselLinksRechts = "umsehen_lr_umkehren";

    public const string SchluesselObenUnten = "umsehen_ob_umkehren";

    private static readonly AsyncLocal<(bool Lr, bool Ob)?> TestWert = new();

    private static bool _lr;
    private static bool _ob;

    /// <summary>Links/rechts umgekehrt (setzt die Oberflaeche beim Start und nach jeder Aenderung).</summary>
    public static bool LinksRechts
    {
        get => TestWert.Value?.Lr ?? _lr;
        set => _lr = value;
    }

    /// <summary>Oben/unten umgekehrt (setzt die Oberflaeche beim Start und nach jeder Aenderung).</summary>
    public static bool ObenUnten
    {
        get => TestWert.Value?.Ob ?? _ob;
        set => _ob = value;
    }

    /// <summary>Nur fuer Tests (Sammelliste 307: AsyncLocal, wirkt nur im eigenen Ablauf, nie prozessweit).</summary>
    internal static IDisposable FuerTests(bool linksRechts, bool obenUnten)
    {
        var vorher = TestWert.Value;
        TestWert.Value = (linksRechts, obenUnten);
        return new Zurueck(() => TestWert.Value = vorher);
    }

    /// <summary>Faktor fuer die waagrechte Drehung (1 oder -1).</summary>
    public static float FaktorLinksRechts => LinksRechts ? -1f : 1f;

    /// <summary>Faktor fuer die Neigung (1 oder -1).</summary>
    public static float FaktorObenUnten => ObenUnten ? -1f : 1f;

    /// <summary>Wert aus einstellungen.json: nur ein echtes <c>true</c> schaltet ein.</summary>
    public static bool Lesen(JsonObject werte, string schluessel) =>
        werte[schluessel] is JsonValue v && v.TryGetValue<bool>(out var an) && an;

    /// <summary>In einer Aenderung setzen (aus = Standard: Eintrag entfernen).</summary>
    public static void Wert(JsonObject werte, string schluessel, bool an)
    {
        if (an)
        {
            werte[schluessel] = true;
        }
        else
        {
            werte.Remove(schluessel);
        }
    }

    /// <summary>Beide Werte aus einstellungen.json uebernehmen (Programmstart).</summary>
    public static void Uebernehmen(JsonObject werte)
    {
        LinksRechts = Lesen(werte, SchluesselLinksRechts);
        ObenUnten = Lesen(werte, SchluesselObenUnten);
    }

    private sealed class Zurueck(Action aktion) : IDisposable
    {
        public void Dispose() => aktion();
    }
}

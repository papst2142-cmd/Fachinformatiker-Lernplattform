using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.1.0 (Sammelliste 409, Weisung P12-328 Punkt 2): Regler „Blickfeld Ich-Sicht“ in der Karte „Steuerung“, am PC und
/// am Handy gleich: waagrechtes Blickfeld der Ich-Sicht von <see cref="Kamera.IchBlickfeldMin"/> bis
/// <see cref="Kamera.IchBlickfeldMax"/> Grad in Schritten von <see cref="Schritt"/>, Vorgabe <see cref="Kamera.IchBlickfeldVorgabe"/>.
/// Die normale Kamera bleibt unveraendert. Gespeichert wird nur ein Wert, der von der Vorgabe abweicht (Vorgabe = kein Eintrag),
/// ueber <see cref="Einstellungen.Aendern"/>; ein kaputter oder fremder Wert gilt als Vorgabe.
/// </summary>
public static class Blickfeldbedienung
{
    public const string Schluessel = "ich_blickfeld";

    /// <summary>Schrittweite des Reglers in Grad.</summary>
    public const int Schritt = 5;

    public static int Min => (int)Kamera.IchBlickfeldMin;

    public static int Max => (int)Kamera.IchBlickfeldMax;

    public static int Vorgabe => (int)Kamera.IchBlickfeldVorgabe;

    private static readonly AsyncLocal<int?> TestWert = new();

    private static int _grad = (int)Kamera.IchBlickfeldVorgabe;

    /// <summary>Blickfeld in Grad (setzt die Oberflaeche beim Start und nach jeder Aenderung; immer im Bereich).</summary>
    public static int Grad
    {
        get => TestWert.Value ?? _grad;
        set => _grad = Begrenzen(value);
    }

    /// <summary>Nur fuer Tests (Sammelliste 307: AsyncLocal, wirkt nur im eigenen Ablauf, nie prozessweit).</summary>
    internal static IDisposable FuerTests(int grad)
    {
        var vorher = TestWert.Value;
        TestWert.Value = Begrenzen(grad);
        return new Zurueck(() => TestWert.Value = vorher);
    }

    /// <summary>In den Bereich legen und auf den Schritt runden.</summary>
    public static int Begrenzen(int grad) =>
        Math.Clamp((int)Math.Round(grad / (double)Schritt, MidpointRounding.AwayFromZero) * Schritt, Min, Max);

    /// <summary>Wert aus einstellungen.json: nur eine ganze Zahl im Bereich gilt (auf den Schritt gerundet), sonst die Vorgabe.</summary>
    public static int Lesen(JsonObject werte) =>
        werte[Schluessel] is JsonValue v && v.TryGetValue<int>(out var grad) && grad >= Min && grad <= Max ? Begrenzen(grad) : Vorgabe;

    /// <summary>In einer Aenderung setzen (Vorgabe: Eintrag entfernen).</summary>
    public static void Wert(JsonObject werte, int grad)
    {
        grad = Begrenzen(grad);
        if (grad == Vorgabe)
        {
            werte.Remove(Schluessel);
        }
        else
        {
            werte[Schluessel] = grad;
        }
    }

    /// <summary>Wert aus einstellungen.json uebernehmen (Programmstart).</summary>
    public static void Uebernehmen(JsonObject werte) => Grad = Lesen(werte);

    private sealed class Zurueck(Action aktion) : IDisposable
    {
        public void Dispose() => aktion();
    }
}

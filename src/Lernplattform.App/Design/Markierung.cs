using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace Lernplattform.Design;

/// <summary>
/// Sammelliste 341 (Patch A 0.81.1, gleiche Ursache wie im Werkzeug, Weisung P12-253): Farbe eines Elements, das an- und
/// abgewaehlt wird. <see cref="FarbenAnwenden.Farbe{T}"/> bindet die Eigenschaft an eine Farb-Ressource; <c>ClearValue</c>
/// beendet diese Bindung in Avalonia aber NICHT: nach dem Wechsel hell/dunkel setzt die alte Bindung ihre Farbe wieder.
/// Hier merkt sich jedes Element je Eigenschaft seine Bindung; eine neue Farbe oder <see cref="Loesen"/> beendet die alte
/// zuerst. Fuer jedes Element, dessen Farbe mit einer Auswahl wechselt (Seitenleiste, untere Leiste, Rundgang-Felder), nur
/// diese Stelle nutzen, nie <c>Farbe</c> plus <c>ClearValue</c>.
/// </summary>
public static class Markierung
{
    private static readonly ConditionalWeakTable<AvaloniaObject, Dictionary<AvaloniaProperty, IDisposable>> Bindungen = new();

    /// <summary>Gegenprobe Start-Test: true = alter Weg bis A 0.81.0 (<c>Farbe</c> plus <c>ClearValue</c>). Nur fuer Tests.</summary>
    private static readonly AsyncLocal<bool> Alt = new();

    /// <summary>Gegenprobe Start-Test (AsyncLocal): Markierungen wie bis A 0.81.0 setzen und loesen.</summary>
    public static bool AltFuerTests
    {
        get => Alt.Value;
        set => Alt.Value = value;
    }

    /// <summary>Bindet <paramref name="eigenschaft"/> an die Farb-Ressource; eine fruehere Bindung derselben Eigenschaft endet.</summary>
    public static T FarbeNeu<T>(this T element, AvaloniaProperty eigenschaft, string schluessel) where T : Control
    {
        if (Alt.Value)
        {
            // alter Weg: Bindung ohne Ende (wie Farbe); gemerkt nur, damit der Start-Test danach aufraeumen kann
            AltBindungen.Add(element.Bind(eigenschaft, element.GetResourceObservable(schluessel)));
            return element;
        }
        Beenden(element, eigenschaft);
        Bindungen.GetOrCreateValue(element)[eigenschaft] = element.Bind(eigenschaft, element.GetResourceObservable(schluessel));
        return element;
    }

    /// <summary>Bindung beenden und den Wert loeschen (danach gilt wieder der Stil).</summary>
    public static void Loesen(Control element, AvaloniaProperty eigenschaft)
    {
        if (!Alt.Value)
        {
            Beenden(element, eigenschaft);
        }
        element.ClearValue(eigenschaft);
    }

    private static readonly List<IDisposable> AltBindungen = [];

    /// <summary>Nach der Gegenprobe: alle Bindungen des alten Wegs beenden (danach die Leiste neu markieren). Nur fuer Tests.</summary>
    public static void AltAufraeumenFuerTests()
    {
        AltBindungen.ForEach(b => b.Dispose());
        AltBindungen.Clear();
    }

    private static void Beenden(AvaloniaObject element, AvaloniaProperty eigenschaft)
    {
        if (Bindungen.TryGetValue(element, out var je) && je.Remove(eigenschaft, out var alt))
        {
            alt.Dispose();
        }
    }
}

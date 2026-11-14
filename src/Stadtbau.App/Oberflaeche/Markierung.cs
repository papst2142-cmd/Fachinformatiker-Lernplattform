using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Weisung P12-253 (Fund Bild 26): Farbe eines Knopfs, der an- und abgewaehlt wird. <c>FarbenAnwenden.Farbe</c> bindet die
/// Eigenschaft an eine Farb-Ressource; <c>ClearValue</c> loest diese Bindung in Avalonia aber NICHT (gemessen: nach dem
/// Wechsel hell/dunkel setzt die alte Bindung den Verlauf wieder, darum sahen alle drei Staerke-Knoepfe gewaehlt aus). Hier
/// merkt sich jeder Knopf je Eigenschaft seine Bindung; eine neue Farbe oder <see cref="Loesen"/> beendet die alte zuerst.
/// Fuer jeden Knopf, dessen Farbe wechselt, nur diese Stelle nutzen, nie <c>Farbe</c> plus <c>ClearValue</c>.
/// </summary>
internal static class Markierung
{
    private static readonly ConditionalWeakTable<AvaloniaObject, Dictionary<AvaloniaProperty, IDisposable>> Bindungen = new();

    /// <summary>Bindet <paramref name="eigenschaft"/> an die Farb-Ressource; eine fruehere Bindung derselben Eigenschaft endet.</summary>
    public static T FarbeNeu<T>(this T element, AvaloniaProperty eigenschaft, string schluessel) where T : Control
    {
        Beenden(element, eigenschaft);
        Bindungen.GetOrCreateValue(element)[eigenschaft] = element.Bind(eigenschaft, element.GetResourceObservable(schluessel));
        return element;
    }

    /// <summary>Bindung beenden und den Wert loeschen (danach gilt wieder der Stil).</summary>
    public static void Loesen(Control element, AvaloniaProperty eigenschaft)
    {
        Beenden(element, eigenschaft);
        element.ClearValue(eigenschaft);
    }

    private static void Beenden(AvaloniaObject element, AvaloniaProperty eigenschaft)
    {
        if (Bindungen.TryGetValue(element, out var je) && je.Remove(eigenschaft, out var alt))
        {
            alt.Dispose();
        }
    }
}

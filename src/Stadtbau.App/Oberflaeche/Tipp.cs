using System.Runtime.CompilerServices;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Design;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-35 Punkt 2): ein Satz, was ein Knopf oder eine Gruppe tut. Am PC erscheint er beim Zeigen
/// mit der Maus, am Tablet bei langem Druck mit dem Finger (Avalonia zeigt Tipps sonst nur fuer die Maus); der
/// Screenreader liest ihn als Hilfetext. Ein langer Druck auf einen Knopf zeigt nur den Tipp und loest den Knopf nicht aus.
/// </summary>
internal static class Tipp
{
    /// <summary>So lange bleibt ein Tipp nach einem langen Druck am Tablet stehen.</summary>
    public static readonly TimeSpan FingerDauer = TimeSpan.FromSeconds(4);

    private static readonly ConditionalWeakTable<Control, string> Texte = new();
    private static readonly ConditionalWeakTable<Button, object> NachHalten = new();

    /// <summary>Tipp an ein Element haengen (Rueckgabe: dasselbe Element).</summary>
    public static T Mit<T>(this T c, string text) where T : Control
    {
        Texte.AddOrUpdate(c, text);
        ToolTip.SetTip(c, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 });
        AutomationProperties.SetHelpText(c, text);
        c.AddHandler(InputElement.HoldingEvent, (_, e) =>
        {
            if (e.HoldingState == HoldingState.Started)
            {
                Halten(c);
            }
        });
        if (c is Button knopf)
        {
            // Nach einem langen Druck kein Klick (der Finger wollte nur lesen). Mit() kommt vor dem eigenen Click-Handler,
            // also laeuft dieser Handler zuerst; Handled haelt die spaeteren an
            knopf.AddHandler(Button.ClickEvent, (_, e) =>
            {
                if (NachHalten.TryGetValue(knopf, out _))
                {
                    NachHalten.Remove(knopf);
                    e.Handled = true;
                }
            });
        }
        return c;
    }

    /// <summary>Text eines schon mit <see cref="Mit{T}"/> versehenen Elements aendern (ohne neue Handler).</summary>
    public static void Setzen(Control c, string text)
    {
        Texte.AddOrUpdate(c, text);
        ToolTip.SetTip(c, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 });
        AutomationProperties.SetHelpText(c, text);
    }

    /// <summary>Tipp eines Elements (Start-Test: jeder Knopf der Kopfleiste und jede Gruppe links hat einen).</summary>
    public static string? Von(Control c) => Texte.TryGetValue(c, out var t) ? t : null;

    /// <summary>Langer Druck mit dem Finger: Tipp zeigen, der folgende Klick des Knopfs zaehlt nicht (Start-Test ruft es direkt).</summary>
    public static void Halten(Control c)
    {
        ToolTip.SetIsOpen(c, true);
        if (c is Button k)
        {
            NachHalten.AddOrUpdate(k, new object());
        }
        DispatcherTimer.RunOnce(() =>
        {
            ToolTip.SetIsOpen(c, false);
            if (c is Button b)
            {
                NachHalten.Remove(b);
            }
        }, FingerDauer);
    }
}

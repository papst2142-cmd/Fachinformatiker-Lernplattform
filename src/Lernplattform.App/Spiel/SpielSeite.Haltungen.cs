using Avalonia.Controls;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.93.0 „Haltungen“ (Weisung P12-303, Sammelliste 352): Knoepfe „Springen“, „Ducken“ und „Kriechen“ im aufgeklappten
/// Menue (neben „Ich-Sicht“, PC und Handy gleich beschriftet), Tasten nach der Tastenbelegung (Leertaste, C, X; frei belegbar),
/// Screenreader-Namen nach dem Zustand und Ansagen (gelungen oder warum nicht).
/// </summary>
public sealed partial class SpielSeite
{
    private Button? _springenKnopf;
    private Button? _duckenKnopf;
    private Button? _kriechenKnopf;

    /// <summary>Die drei Knoepfe anlegen (im Konstruktor, vor dem Menue).</summary>
    private IEnumerable<Button> HaltungKnoepfeAnlegen()
    {
        _springenKnopf = Schrift.Knopf(SpielProbeTexte.KnopfSpringen);
        _springenKnopf.Click += (_, _) => HaltungAusfuehren(Tastenbelegung.Springen);
        _duckenKnopf = Schrift.Knopf(SpielProbeTexte.KnopfDucken);
        _duckenKnopf.Click += (_, _) => HaltungAusfuehren(Tastenbelegung.Ducken);
        _kriechenKnopf = Schrift.Knopf(SpielProbeTexte.KnopfKriechen);
        _kriechenKnopf.Click += (_, _) => HaltungAusfuehren(Tastenbelegung.Kriechen);
        return [_springenKnopf, _duckenKnopf, _kriechenKnopf];
    }

    /// <summary>Knopf oder Taste: springen, ducken oder kriechen; die Ansage sagt, was geschah. true = die Aufgabe gehoert hierher.</summary>
    public bool HaltungAusfuehren(string aufgabe)
    {
        if (Spiel is not { } spiel || aufgabe is not (Tastenbelegung.Springen or Tastenbelegung.Ducken or Tastenbelegung.Kriechen))
        {
            return false;
        }
        if (!Flaeche.GlBereit || _ersatz.IsVisible)
        {
            return true;
        }
        // in der Ich-Sicht schaut der Koerper in die Blickrichtung: von dort aus Platz suchen
        float? blick = Flaeche.AktuelleKamera is { } k && StadtFlaeche.IchNah(k) ? IchSicht.FigurDrehung(k, spiel) : null;
        _ = aufgabe switch
        {
            Tastenbelegung.Springen => spiel.Springen(),
            Tastenbelegung.Ducken => spiel.Ducken(blick),
            _ => spiel.Kriechen(blick),
        };
        if (spiel.HaltungHinweis is { } hinweis)
        {
            Views.Ansage.Sagen(hinweis);
        }
        HaltungKnoepfeSetzen();
        Flaeche.AnfordernBild();
        return true;
    }

    /// <summary>Sichtbar nur mit 3D, wenn die Figur steht (nicht im Sitzen, Liegen, Bus und beim Einrichten); Text und Name nach dem Zustand.</summary>
    private void HaltungKnoepfeSetzen()
    {
        if (_springenKnopf is null || _duckenKnopf is null || _kriechenKnopf is null)
        {
            return;
        }
        // Welle A 0.99.0 (Sammelliste 400): am Handy und Tablet statt der drei Eintraege die runden Knoepfe am rechten Daumen
        // passen die runden Knoepfe nicht ins Bild (sehr flach), bleiben die Menue-Eintraege (sonst gaebe es kein Ducken)
        var an = Flaeche.GlBereit && !_ersatz.IsVisible && Spiel is { KoerperMoeglich: true }
            && !(DaumenKnoepfeAn && HaltungKnopf.Lage(Flaeche.StickBereich) is not null);
        DaumenSetzen();
        var koerper = Spiel?.Koerper ?? Koerperhaltung.Stehen;
        Setzen(_springenKnopf, an, SpielProbeTexte.KnopfSpringen);
        Setzen(_duckenKnopf, an, koerper == Koerperhaltung.Ducken ? SpielProbeTexte.KnopfAufstehen : SpielProbeTexte.KnopfDucken);
        Setzen(_kriechenKnopf, an, koerper == Koerperhaltung.Kriechen ? SpielProbeTexte.KnopfAufstehen : SpielProbeTexte.KnopfKriechen);

        static void Setzen(Button knopf, bool sichtbar, string text)
        {
            knopf.IsVisible = sichtbar;
            if (knopf.Content is TextBlock block)
            {
                block.Text = text;
            }
            else
            {
                knopf.Content = text;
            }
            Avalonia.Automation.AutomationProperties.SetName(knopf, text);
        }
    }

    /// <summary>Start-Test: die Knoepfe „Springen“, „Ducken“, „Kriechen“.</summary>
    public (Button Springen, Button Ducken, Button Kriechen) HaltungKnoepfe => (_springenKnopf!, _duckenKnopf!, _kriechenKnopf!);
}

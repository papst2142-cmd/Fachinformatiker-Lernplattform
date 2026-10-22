using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Ausbildung = Lernplattform.Core.Pruefung.Ausbildung;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W4 (Plan 2.4/2.5, Wortliste N3-N8): Ausbildungsfrage beim ersten Start, Warnhinweis
/// Rahmenplan und der Vergleich des Merkers ausbildung_zuletzt (Fachrichtung von anderswo, Frage 2 a).
/// Die Regeln stehen in Core.Hilfe.AusbildungFrage; hier nur die Fenster.
/// </summary>
public static class AusbildungTeil
{
    private static bool _laeuft;

    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// Die Frage (N3/N4) ohne Vorwahl; "Speichern" erst nach einer Wahl. "Überspringen", Esc und die
    /// Zurueck-Geste setzen nur den Merker (es gilt weiter SI). Danach der Warnhinweis, wenn noetig.
    /// </summary>
    public static async Task FrageAsync()
    {
        if (_laeuft)
        {
            return;
        }
        _laeuft = true;
        try
        {
            var (fenster, gewaehlt) = FrageZeigen();
            var antwort = await fenster;
            var wahl = gewaehlt();
            var (gespeichert, fehler, merker) = AusbildungFrage.FrageAntwort(Zustand.Schreiber, antwort, wahl, Heute);
            Merken(merker);
            if (fehler)
            {
                await Dialog.Zeigen(Optionen.SpeichernFehlgeschlagenTitel, Optionen.SpeichernFehlgeschlagen, Dialog.Ok);
                return;
            }
            if (gespeichert is null)
            {
                return;
            }
            AbgleichDienst.NachDemLernen();
            await WarnungAsync(gespeichert);
        }
        finally
        {
            _laeuft = false;
        }
    }

    /// <summary>
    /// Das Fenster der Frage (auch Start ohne Bedienung: zeigen und mit Dialog.AlleSchliessen schliessen,
    /// ohne die Antwort auszuwerten). Ergebnis: der Knopf und die angeklickte Wahl.
    /// </summary>
    public static (Task<int> Fenster, Func<string?> Wahl) FrageZeigen()
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        string? wahl = null;
        Button? speichern = null;
        var auswahl = new StackPanel { Spacing = 4 };
        foreach (var (wert, text) in PruefungTexte.Fachrichtungen)
        {
            var knopf = new RadioButton
            {
                Content = new LernText(text, "Normal", FontWeight.Normal, "Pinsel.text"),
                GroupName = "ausbildung_frage",
                IsChecked = false,
            };
            knopf.Farbe(RadioButton.ForegroundProperty, "Pinsel.text");
            Avalonia.Automation.AutomationProperties.SetName(knopf, text);
            knopf.IsCheckedChanged += (_, _) =>
            {
                if (knopf.IsChecked == true)
                {
                    wahl = wert;
                    speichern?.IsEnabled = true;
                }
            };
            auswahl.Children.Add(knopf);
        }
        var inhalt = new StackPanel
        {
            Spacing = 10,
            Children = { new LernText(HilfeTexte.AusbildungFrage, "Normal", FontWeight.Normal, "Pinsel.text"), auswahl },
        };
        // Zurueck-Geste am Handy gilt wie "Überspringen" (letzter Knopf)
        var top = Avalonia.Application.Current?.ApplicationLifetime switch
        {
            Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime e => TopLevel.GetTopLevel(e.MainView),
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d => d.MainWindow,
            _ => null,
        };
        void Zurueck(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            Dialog.AlleSchliessen();
        }
        top?.BackRequested += Zurueck;
        var fenster = Dialog.Zeigen(RundgangTexte.AusbildungTitel, inhalt, knoepfe =>
        {
            speichern = knoepfe[0];
            speichern.IsEnabled = false;
        }, RundgangTexte.AusbildungSpeichern, RundgangTexte.AusbildungUeberspringen);
        Messung.Messwerte.Reihe("Ausbildungsfrage zeigen", uhr.Elapsed.TotalMilliseconds);
        return (Abmelden(), () => wahl);

        async Task<int> Abmelden()
        {
            try
            {
                return await fenster;
            }
            finally
            {
                top?.BackRequested -= Zurueck;
            }
        }
    }

    private static bool Merken(Action<System.Text.Json.Nodes.JsonObject> aenderung) => Zustand.Aendere(aenderung);

    /// <summary>
    /// Warnhinweis Rahmenplan (N6/N7/N8), nur wenn WarnungNoetig: "Ja" schaltet genau den einen Abschnitt ein
    /// (ein Schreibvorgang), "Nein" und Esc aendern nichts.
    /// </summary>
    public static async Task WarnungAsync(string fachrichtung)
    {
        if (!AusbildungFrage.WarnungNoetig(fachrichtung, Zustand.Werte))
        {
            return;
        }
        var fenster = WarnungZeigen(fachrichtung);
        if (AusbildungFrage.WarnungAntwort(await fenster, fachrichtung) is { } aenderung && !Zustand.Aendere(aenderung))
        {
            await Dialog.Zeigen(Optionen.SpeichernFehlgeschlagenTitel, Optionen.SpeichernFehlgeschlagen, Dialog.Ok);
        }
    }

    /// <summary>Das Fenster des Warnhinweises (auch Start ohne Bedienung, ohne Auswertung).</summary>
    public static Task<int> WarnungZeigen(string fachrichtung)
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var fenster = Dialog.Zeigen(RundgangTexte.WarnungTitel, new LernText(AusbildungFrage.WarnungText(fachrichtung), "Normal",
            FontWeight.Normal, "Pinsel.text"), RundgangTexte.Ja, RundgangTexte.Nein);
        Messung.Messwerte.Reihe("Warnhinweis zeigen", uhr.Elapsed.TotalMilliseconds);
        return fenster;
    }

    /// <summary>
    /// Nach einem Wechsel in Optionen &gt; Ausbildung: Merker setzen (kein zweiter Hinweis spaeter), dann der Hinweis.
    /// </summary>
    public static async Task GewechseltAsync(string fachrichtung)
    {
        Merken(w => AusbildungFrage.Merken(w, fachrichtung));
        await WarnungAsync(fachrichtung);
    }

    /// <summary>
    /// Fachrichtung von anderswo (Start, nach Abgleich, Uebernahme, Ruecksicherung): weicht der Merker ab
    /// (oder fehlt er bei gewaehlter Fachrichtung), wird er gesetzt und einmal der Hinweis gezeigt, wenn noetig.
    /// Laesst sich der Merker nicht speichern, kommt kein Hinweis (er kaeme sonst immer wieder).
    /// </summary>
    public static async Task VergleichenAsync()
    {
        if (_laeuft || Zustand.Einstellungen.NurAnzeige || MainView.AutoModus)
        {
            return;
        }
        _laeuft = true;
        try
        {
            var gespeichert = await Task.Run(() => Ausbildung.Gespeichert(Zustand.Schreiber));
            if (gespeichert is null || !AusbildungFrage.VonAnderswo(gespeichert, Zustand.Werte) || !Merken(w => AusbildungFrage.Merken(w, gespeichert)))
            {
                return;
            }
            await WarnungAsync(gespeichert);
        }
        finally
        {
            _laeuft = false;
        }
    }
}

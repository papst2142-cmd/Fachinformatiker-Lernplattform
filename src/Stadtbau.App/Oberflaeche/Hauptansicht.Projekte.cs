using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.23.0 (Weisung P12-96, Sammelliste 242, Nico: „das man angefangen Projekte auch löschen kann“): im Fenster
/// „Projekt öffnen“ steht jedes Projekt in einem Kasten wie „Meine Blueprints“: oben der Name (antippen = oeffnen), darunter
/// „Löschen“. Rueckfrage mit „Abbrechen“ als Vorgabe; vorher kommt die Datei als Kopie in die Sicherungen des Projekts
/// (<see cref="Projekt.Loeschen"/>). Ist das Projekt gerade offen, wartet das Werkzeug die Arbeit im Hintergrund ab, fragt bei
/// einer nicht gespeicherten Aenderung nach und zeigt danach die Startseite. Taste Entf loescht das Projekt, dessen Kasten
/// den Fokus hat.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Je Projekt im Fenster „Projekt öffnen“: sein Kasten und die Knoepfe darin (Start-Test).</summary>
    public List<(Border Kasten, List<Button> Knoepfe, string Datei)> ProjektKaesten { get; } = [];

    /// <summary>Nur Start-Test (Gegenprobe): Liste wie bis S 0.22.1, nur ein Knopf je Projekt, kein Kasten, kein Löschen.</summary>
    internal static bool ProjektListeAltFuerTests { get; set; }

    /// <summary>Nur Start-Test: Uhr fuer den Namen der Kopie.</summary>
    internal static Func<DateTime> LoeschUhr { get; set; } = () => DateTime.Now;

    /// <summary>Die Liste im Fenster „Projekt öffnen“ (neu) fuellen; <paramref name="oeffnen"/> oeffnet eine Datei.</summary>
    private void ProjektListeFuellen(StackPanel liste, Action<string> oeffnen)
    {
        liste.Children.Clear();
        ProjektKaesten.Clear();
        var projekte = Projekt.Liste(_ordner);
        if (projekte.Count == 0)
        {
            liste.Children.Add(Schrift.Text(Texte.KeineProjekte, farbe: "Pinsel.muted"));
            return;
        }
        foreach (var datei in projekte)
        {
            var name = Projekt.AnzeigeName(datei);
            var k = Schrift.Knopf(name + " (" + File.GetLastWriteTime(datei).ToString("dd.MM.yyyy HH:mm", De) + ")")
                .Vorlese(Texte.OeffnenTitel + " „" + name + "“");
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            k.Click += (_, _) => oeffnen(datei);
            if (ProjektListeAltFuerTests)
            {
                liste.Children.Add(k);
                ProjektKaesten.Add((new Border(), [k], datei));
                continue;
            }
            k.Margin = new Thickness(0);
            var loeschen = KleinKnopf(Schrift.Gefahrknopf(Texte.Loeschen).Vorlese(Texte.Loeschen + " „" + name + "“").Mit(Texte.ProjektLoeschenTipp));
            loeschen.HorizontalAlignment = HorizontalAlignment.Left;
            loeschen.Click += (_, _) => _ = ProjektLoeschenAsync(datei, name, () => ProjektListeFuellen(liste, oeffnen));
            var kasten = new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Child = new StackPanel { Spacing = 8, Children = { k, loeschen } },
            }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
            // Taste Entf, wenn ein Knopf dieses Kastens den Fokus hat (das Fenster haelt die Tasten der Karte fern)
            kasten.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None)
                {
                    e.Handled = true;
                    _ = ProjektLoeschenAsync(datei, name, () => ProjektListeFuellen(liste, oeffnen));
                }
            };
            liste.Children.Add(kasten);
            ProjektKaesten.Add((kasten, [k, loeschen], datei));
        }
    }

    private static Button KleinKnopf(Button k)
    {
        k.Padding = new Thickness(10, 4);
        if (k.Content is TextBlock t)
        {
            t.Groesse("Klein");
        }
        k.Margin = new Thickness(0);
        return k;
    }

    /// <summary>Ist <paramref name="datei"/> das gerade offene Projekt?</summary>
    private bool IstOffen(string datei) =>
        _projekt is not null && string.Equals(Path.GetFullPath(_projekt.Datei), Path.GetFullPath(datei), StringComparison.Ordinal);

    /// <summary>Rueckfrage, dann loeschen; danach <paramref name="neu"/> (Liste neu zeigen).</summary>
    private async Task ProjektLoeschenAsync(string datei, string name, Action neu)
    {
        if (await Dialog.Rueckfrage(Texte.ProjektLoeschenTitel, Texte.ProjektLoeschenFrage(name), 1, Texte.Loeschen, Texte.Abbrechen) != 0)
        {
            return;
        }
        await ProjektLoeschenOhneFrageAsync(datei, name);
        neu();
    }

    /// <summary>
    /// Loeschen ohne die erste Rueckfrage (Start-Test). Offenes Projekt: Hintergrund abwarten, bei nicht gespeicherter
    /// Aenderung nachfragen (Speichern und löschen / Ohne Speichern löschen / Abbrechen), danach Startseite.
    /// Rueckgabe: true = geloescht.
    /// </summary>
    public async Task<bool> ProjektLoeschenOhneFrageAsync(string datei, string name)
    {
        var offen = IstOffen(datei);
        if (offen)
        {
            await HintergrundAbwartenAsync();
            if (Ungespeichert)
            {
                var wahl = await Dialog.Zeigen(Texte.ProjektLoeschenTitel, Texte.ProjektLoeschenUngespeichert, Texte.SpeichernUndLoeschen, Texte.OhneSpeichernLoeschen, Texte.Abbrechen);
                if (wahl is not (0 or 1))
                {
                    return false;
                }
                if (wahl == 0)
                {
                    try
                    {
                        _projekt?.Speichern(vonHand: false, ohneSicherung: true);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        _ordner.Fehler("Speichern vor dem Löschen: " + e.Message);
                        await Dialog.Zeigen(Texte.ProjektLoeschenTitel, Texte.ProjektLoeschenFehler + e.Message, Texte.Ok);
                        return false;
                    }
                }
            }
        }
        try
        {
            Projekt.Loeschen(_ordner, datei, LoeschUhr());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Projekt löschen: " + e.GetType().Name + ": " + e.Message);
            await Dialog.Zeigen(Texte.ProjektLoeschenTitel, Texte.ProjektLoeschenFehler + e.Message, Texte.Ok);
            return false;
        }
        if (offen)
        {
            ProjektSchliessen();
        }
        else if (_projekt is null && _entwurf is null)
        {
            StartZeigen();
        }
        Melden(Texte.ProjektGeloescht(name));
        return true;
    }

    /// <summary>Das offene Projekt schliessen und die Startseite zeigen (nach dem Loeschen).</summary>
    private void ProjektSchliessen()
    {
        if (_projekt is null)
        {
            return;
        }
        Karte.SetzenBeenden();
        Karte.Waehlen(null);
        if (_drei)
        {
            DreiTeileZeigen(false);
        }
        _drei = false;
        _innen = false;
        _geschichteOffen = false;
        _projekt.Geaendert -= ProjektGeaendert;
        _projekt = null;
        _ort = null;
        _aussenStand++;
        TitelSetzen();
        StartZeigen();
    }

    /// <summary>Arbeit im Hintergrund abwarten (wie „Werkzeug beenden“, hoechstens <see cref="BeendenWartetHoechstens"/>).</summary>
    private async Task HintergrundAbwartenAsync()
    {
        var offen = _hintergrund.Where(t => !t.IsCompleted).ToArray();
        if (offen.Length > 0)
        {
            using (Dialog.Warten(Texte.BeendenWartet))
            {
                await Task.WhenAny(Task.WhenAll(offen), Task.Delay(BeendenWartetHoechstens));
            }
        }
    }
}

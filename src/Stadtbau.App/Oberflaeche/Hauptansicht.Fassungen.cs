using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.47.0 W6 (Weisung P12-226): im Fenster „Projekt öffnen“ je Projekt „Frühere Fassungen“. Das Fenster zeigt die
/// Sicherungen (neueste zuerst) mit Zeit und Vorschaubild der Draufsicht; „Diese Fassung als neues Projekt öffnen“ legt ein
/// neues Projekt an (<see cref="Projekt.AusSicherung"/>), das Projekt und die Sicherung bleiben unveraendert, nichts wird
/// geloescht oder ueberschrieben.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Breite und Hoehe eines Vorschaubilds.</summary>
    public const double FassungBildBreite = 240;
    public const double FassungBildHoehe = 150;

    /// <summary>Start-Test: Kaesten des offenen Fensters (Bild, Knopf, Datei) und das Laden der Bilder.</summary>
    internal List<(Border Kasten, KartenFlaeche Bild, Button Oeffnen, string Datei)> FassungKaesten { get; } = [];
    internal Task? FassungBilderGeladen { get; private set; }

    private string? _fassungenFuer;

    private Button FassungenKnopf(string name, Action gedrueckt)
    {
        var k = KleinKnopf(Schrift.Knopf(Texte.FruehereFassungen).Vorlese(Texte.FruehereFassungen + " „" + name + "“").Mit(Texte.FruehereFassungenTipp));
        k.HorizontalAlignment = HorizontalAlignment.Left;
        k.Click += (_, _) =>
        {
            _fassungenFuer = name;
            gedrueckt();
        };
        return k;
    }

    /// <summary>Fenster „Frühere Fassungen“ fuer das Projekt <paramref name="name"/>; Rueckgabe: true = eine Fassung geoeffnet.</summary>
    public async Task<bool> FassungenZeigenAsync(string name)
    {
        if (_daten is not { } daten)
        {
            return false;
        }
        FassungKaesten.Clear();
        var liste = new StackPanel { Spacing = 8 };
        var fassungen = Projekt.Fassungen(_ordner, name);
        string? gewaehlt = null;
        DateTime zeit = default;
        if (fassungen.Count == 0)
        {
            liste.Children.Add(Schrift.Text(Texte.FruehereFassungenLeer, farbe: "Pinsel.muted"));
        }
        var laden = new List<(KartenFlaeche Bild, TextBlock Zeile, Button Knopf, string Datei)>();
        foreach (var f in fassungen)
        {
            var bild = new KartenFlaeche
            {
                Width = FassungBildBreite,
                Height = FassungBildHoehe,
                IsHitTestVisible = false,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var zeile = Schrift.Text(Texte.FassungZeit(f.Zeit) + (f.VorLoeschen ? " · " + Texte.FassungVorLoeschen : ""), "Normal", Avalonia.Media.FontWeight.Bold);
            var neuName = Projekt.NameAusSicherung(name, f.Zeit, _ordner);
            var knopf = KleinKnopf(Schrift.Knopf(Texte.FassungAlsProjekt).Vorlese(Texte.FassungAlsProjekt + ", " + zeile.Text).Mit(Texte.FassungAlsProjektTipp(neuName)));
            knopf.HorizontalAlignment = HorizontalAlignment.Left;
            knopf.IsEnabled = false;
            var fz = f;
            knopf.Click += (_, _) =>
            {
                gewaehlt = fz.Datei;
                zeit = fz.Zeit;
                Dialog.AlleSchliessen();
            };
            var rechts = new StackPanel { Spacing = 6, Children = { zeile, knopf } };
            var kasten = new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Child = new WrapPanel { Orientation = Orientation.Horizontal, Children = { bild, new Border { Width = 12 }, rechts } },
            }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
            liste.Children.Add(kasten);
            FassungKaesten.Add((kasten, bild, knopf, f.Datei));
            laden.Add((bild, zeile, knopf, f.Datei));
        }
        // Bilder im Hintergrund lesen: das Fenster ist sofort da, jedes Bild kommt, sobald seine Sicherung gelesen ist
        FassungBilderGeladen = BilderLadenAsync();
        await Dialog.Zeigen(Texte.FruehereFassungenTitel(name), new ScrollViewer { Content = liste, MaxHeight = 460 }, Texte.Schliessen);
        FassungKaesten.Clear();
        return gewaehlt is { } datei && await FassungOeffnenAsync(datei, name, zeit);

        async Task BilderLadenAsync()
        {
            foreach (var (bild, zeile, knopf, datei) in laden)
            {
                try
                {
                    var p = await Task.Run(() => Projekt.Oeffnen(datei, daten, _ordner));
                    bild.Zeigen(p);
                    bild.Einpassen();
                    knopf.IsEnabled = !p.NurLesen;
                    if (p.NurLesen)
                    {
                        zeile.Text += " · " + Texte.FassungNeuer;
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or StadtbauFehler or InvalidDataException)
                {
                    zeile.Text += " · " + Texte.FassungKaputt;
                    bild.IsVisible = false;
                }
            }
        }
    }

    /// <summary>Die Sicherung <paramref name="datei"/> als neues Projekt anlegen und oeffnen (nie ueberschrieben).</summary>
    public async Task<bool> FassungOeffnenAsync(string datei, string name, DateTime zeit)
    {
        if (_daten is null)
        {
            return false;
        }
        try
        {
            var neu = Projekt.NameAusSicherung(name, zeit, _ordner);
            var p = Projekt.AusSicherung(datei, _daten, _ordner, neu);
            ProjektSetzen(p, p.Orte.FirstOrDefault(), innen: false);
            Melden(Texte.FassungGeoeffnet(neu));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or StadtbauFehler or ArgumentException)
        {
            var text = e is StadtbauFehler f ? string.Join(" ", f.Funde) : e.Message;
            _ordner.Fehler("Fassung öffnen: " + e.GetType().Name + ": " + text);
            await Dialog.Zeigen(Texte.FruehereFassungen, text, Texte.Ok);
            return false;
        }
    }
}

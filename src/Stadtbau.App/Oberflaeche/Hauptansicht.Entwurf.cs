using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.9.0 (Weisung P12-48): Knopf „Gebäude entwerfen“ in der Kopfleiste. Neu oder einen eigenen Blueprint (Format 2)
/// weiterbauen, Speichern als Blueprint (gleicher Name = die Datei ersetzen, Sicherung vorher), Zurueck zur Stadt mit
/// Rueckfrage, wenn etwas ungespeichert ist. Solange der Editor offen ist, gelten Rueckgaengig, Wiederholen und die Tasten
/// fuer den Entwurf; die Knoepfe fuer das Projekt sind gesperrt.
/// </summary>
public sealed partial class Hauptansicht
{
    private EntwurfAnsicht? _entwurf;
    private object? _vorEntwurf;

    /// <summary>Offener Editor (Start-Test), sonst null.</summary>
    public EntwurfAnsicht? Entwurf => _entwurf;

    /// <summary>Ist der Knopf der Kopfleiste frei (Start-Test)?</summary>
    public bool KopfKnopfFrei(string name) => _kopf[name].IsEnabled;

    /// <summary>Eigene Gebaeude (Blueprints im Format 2) zum Weiterbauen.</summary>
    public List<Blueprint> EigeneBlueprints()
    {
        try
        {
            return Blueprint.Alle(_ordner).Select(e => e.Vorlage).Where(b => b?.Bauteile is not null).Select(b => b!).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Blueprints lesen: " + e.GetType().Name + ": " + e.Message);
            return [];
        }
    }

    private async Task EntwerfenAsync()
    {
        if (_daten is null || _entwurf is not null)
        {
            return;
        }
        var eigene = EigeneBlueprints();
        if (eigene.Count == 0)
        {
            EntwurfOeffnen(null);
            return;
        }
        var wahl = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
        foreach (var b in eigene)
        {
            wahl.Items.Add(new ComboBoxItem { Content = b.Name });
        }
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children = { Schrift.Text(Texte.EntwurfFrage), Schrift.Text(Texte.EntwurfWeiter + ":", "Klein", FontWeight.Bold).Fuer(wahl), wahl },
        };
        switch (await Dialog.Zeigen(Texte.GebaeudeEntwerfen, inhalt, Texte.EntwurfNeu, Texte.EntwurfWeiter, Texte.Abbrechen))
        {
            case 0:
                EntwurfOeffnen(null);
                break;
            case 1 when wahl.SelectedIndex >= 0:
                EntwurfOeffnen(eigene[wahl.SelectedIndex]);
                break;
        }
    }

    /// <summary>Editor oeffnen (neu oder mit einem eigenen Blueprint); Rueckgabe: die Ansicht.</summary>
    public EntwurfAnsicht? EntwurfOeffnen(Blueprint? weiter)
    {
        if (_daten is null)
        {
            return null;
        }
        var e = weiter is null ? new Entwurf() : Core.Entwurf.Aus(weiter);
        if (e is null)
        {
            return null;
        }
        var ansicht = new EntwurfAnsicht(e, _daten, Melden, _ordner);
        ansicht.SpeichernGewuenscht += () => _ = EntwurfSpeichernAsync();
        ansicht.ZurueckGewuenscht += () => _ = EntwurfZurueckAsync();
        ansicht.VerlaufGeaendert += KnoepfeFreigeben;
        _vorEntwurf = _inhalt.Content;
        _entwurf = ansicht;
        _inhalt.Content = ansicht;
        KnoepfeFreigeben();
        Melden(Texte.GebaeudeEntwerfen);
        return ansicht;
    }

    /// <summary>Zurueck zur Stadt ohne Rueckfrage (nach der Rueckfrage und im Start-Test).</summary>
    public void EntwurfSchliessen()
    {
        if (_entwurf is null)
        {
            return;
        }
        _entwurf.Drei.Spiel = null;
        _entwurf = null;
        _inhalt.Content = _vorEntwurf;
        _vorEntwurf = null;
        if (_projekt is null)
        {
            StartZeigen();
        }
        else
        {
            BlueprintsZeigen();
        }
        KnoepfeFreigeben();
    }

    private async Task EntwurfZurueckAsync()
    {
        if (_entwurf is not { } a)
        {
            return;
        }
        if (a.Ungespeichert && a.Entwurf.Teile.Count > 0
            && await Dialog.Zeigen(Texte.EntwurfZurueck, Texte.EntwurfZurueckFrage, Texte.EntwurfVerwerfen, Texte.Abbrechen) != 0)
        {
            return;
        }
        EntwurfSchliessen();
    }

    /// <summary>Als Blueprint speichern ohne Fenster (Start-Test): null = gespeichert, sonst der Grund.</summary>
    public string? EntwurfSpeichern(string name)
    {
        if (_entwurf is not { } a)
        {
            return Texte.BlueprintNurHaus;
        }
        if (!Blueprint.NameGueltig(name))
        {
            return Texte.NameUngueltig;
        }
        var e = a.Entwurf;
        bool grundrissNeu;
        int etagenNeu;
        try
        {
            var b = e.AlsBlueprint(name, Katalog.Bauteil, _projekt?.Name ?? "", _projekt?.Basis.StadtId ?? "", out grundrissNeu, out etagenNeu);
            // Weiterbauen unter demselben Namen ersetzt die Datei (Sicherung vorher), sonst ein neuer Blueprint
            if (e.Datei is { } datei && File.Exists(datei) && string.Equals(e.Name, name, StringComparison.CurrentCultureIgnoreCase))
            {
                b.Ersetzen(_ordner, datei, Jetzt);
            }
            else
            {
                b.Speichern(_ordner, Jetzt);
            }
        }
        catch (StadtbauFehler f)
        {
            return Texte.EntwurfRotSpeichern + " " + string.Join(" ", f.Funde);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            return x.Message;
        }
        a.Gespeichert(name);
        Melden(Texte.EntwurfGespeichert + ": „" + name + "“" + (grundrissNeu ? " " + Texte.EntwurfGrundrissNeu : "")
            + (etagenNeu > 0 ? " " + Texte.EntwurfEtagenAngelegt(etagenNeu) : ""));
        return null;
    }

    private async Task EntwurfSpeichernAsync()
    {
        if (_entwurf is not { } a)
        {
            return;
        }
        var name = new TextBox { Text = a.Entwurf.Name ?? "", MaxLength = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children = { Schrift.Text(Texte.BlueprintName, gewicht: FontWeight.Bold).Fuer(name), name, Schrift.Text(Texte.BlueprintNameHinweis, "Klein", farbe: "Pinsel.muted"), fehler },
        };
        while (true)
        {
            if (await Dialog.Zeigen(Texte.EntwurfSpeichern, inhalt, Texte.Speichern, Texte.Abbrechen) != 0)
            {
                return;
            }
            if (EntwurfSpeichern((name.Text ?? "").Trim()) is not { } grund)
            {
                return;
            }
            fehler.Text = grund;
        }
    }

    /// <summary>Tasten im Editor (aus <see cref="TasteGedrueckt"/>): true = erledigt.</summary>
    private bool EntwurfTaste(KeyEventArgs e)
    {
        if (_entwurf is not { } a)
        {
            return false;
        }
        var strg = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (strg && e.Key == Key.Z)
        {
            Rueckgaengig();
            return true;
        }
        if (strg && e.Key == Key.Y)
        {
            Wiederholen();
            return true;
        }
        if (strg && e.Key == Key.S)
        {
            _ = EntwurfSpeichernAsync();
            return true;
        }
        if (e.Source is TextBox || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return false;
        }
        if (e.Key == Key.Space)
        {
            KartenFlaeche.LeertasteGehalten = true;
            return true;
        }
        return a.Taste(e.Key);
    }
}

using Avalonia.Controls;
using Avalonia.Threading;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.57.0 (Inhaltspakete W3, Weisung P12-297 W3-1): „Neue Stadt (Paket)“ im Fenster „Neues Projekt“ (Kennung und Name
/// des Pakets, Vorschlag aus dem Projektnamen) und die Zeile neben dem Titel mit der geschaetzten Paketgroesse. Die Groesse
/// wird nach jedem Speichern neu geschaetzt (Datei schreiben auf dem Oberflaechen-Faden, Packen im Hintergrund; das letzte
/// Ergebnis gewinnt).
/// </summary>
public sealed partial class Hauptansicht
{
    private readonly TextBlock _paketZeile = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private object? _paketGeschaetzt;
    private int _paketLauf;

    /// <summary>Knopf und Felder im zuletzt gezeigten Fenster „Neues Projekt“ (Start-Test).</summary>
    internal RadioButton? NeuPaketKnopf { get; private set; }

    internal TextBox? NeuKennungFeld { get; private set; }

    internal TextBox? NeuPaketNameFeld { get; private set; }

    /// <summary>Text der Paket-Zeile neben dem Titel (leer = keine Paket-Stadt; Start-Test).</summary>
    internal string PaketZeileText => _paketZeile.IsVisible ? _paketZeile.Text ?? "" : "";

    /// <summary>Letzte geschaetzte Groesse in Bytes (null = noch keine; Start-Test).</summary>
    internal long? PaketGroesseGeschaetzt { get; private set; }

    /// <summary>Vorschlag der Kennung aus dem Projektnamen (ASCII, klein, _; am Anfang ein Buchstabe; leer, wenn nichts passt).</summary>
    internal static string KennungVorschlag(string name)
    {
        var k = Projekt.LeerStadtId(name)[4..].Trim('_');
        if (k.Length > 0 && k[0] is < 'a' or > 'z')
        {
            k = "s" + k;
        }
        k = k.Length > 32 ? k[..32].TrimEnd('_') : k;
        return k.Length >= 2 ? k : "";
    }

    private void PaketZeileZeigen(bool offen)
    {
        if (_projekt?.Paket is not { } p)
        {
            _paketZeile.IsVisible = false;
            _paketGeschaetzt = null;
            PaketGroesseGeschaetzt = null;
            return;
        }
        _paketZeile.IsVisible = true;
        // geschaetzt wird der gespeicherte Stand (gleich, was „An CC senden“ schickt)
        var schluessel = (_projekt, _projekt.SchritteZurueck, _projekt.SchritteVor);
        if (offen || Equals(_paketGeschaetzt, schluessel))
        {
            _paketZeile.Text = Texte.PaketZeile(p.Kennung, PaketGroesseGeschaetzt is { } g ? PaketTexte.Mb(g) : null);
            return;
        }
        _paketGeschaetzt = schluessel;
        _paketZeile.Text = Texte.PaketZeile(p.Kennung, null);
        byte[] datei;
        try
        {
            datei = _projekt.Stand("2026-01-01T00:00:00+01:00").Schreiben();
        }
        catch (Exception x) when (x is IOException or InvalidOperationException or ArgumentException)
        {
            return;
        }
        var lauf = ++_paketLauf;
        _ = Task.Run(() => Projekt.PaketGroesseVon(datei, p.Kennung, p.Name)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (lauf != _paketLauf || t.IsFaulted || _projekt?.Paket is not { } jetzt || jetzt.Kennung != p.Kennung)
            {
                return;
            }
            PaketGroesseGeschaetzt = t.Result;
            _paketZeile.Text = Texte.PaketZeile(p.Kennung, PaketTexte.Mb(t.Result));
        }), TaskScheduler.Default);
    }
}

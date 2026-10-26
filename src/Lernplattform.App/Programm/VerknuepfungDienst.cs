using Avalonia.Platform;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Linux;
using Lernplattform.Core.Texte;
using Lernplattform.Views;
using Aktuell = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Programm;

/// <summary>
/// Phase 6 W2 (VE5): Linux-Verknuepfung wie fisi_verknuepfung.py (create_and_report, should_ask) und
/// maybe_ask_shortcut (app_gui 10905). Nur unter Linux im installierten AppImage; der Merker
/// desktop_verknuepfung wird nur ueber Einstellungen.Aendern geschrieben (Zustand.Aendere).
/// </summary>
public static class VerknuepfungDienst
{
    private static readonly Lazy<VerknuepfungUmgebung> Echt = new(() => VerknuepfungUmgebung.Echt(Symbol));

    private static readonly Lazy<bool> Moeglich = new(() =>
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }
        try
        {
            return Verknuepfung.Unterstuetzt(true, Aktuell.Quelle?.Invoke().Installiert ?? false, Echt.Value.AppImage);
        }
        catch (Exception)
        {
            return false;
        }
    });

    /// <summary>Knopf und Rueckfrage zeigen (Linux, installiertes AppImage).</summary>
    public static bool Unterstuetzt => Moeglich.Value;

    /// <summary>Umgebung fuer Knopf und Rueckfrage (Start ohne Bedienung: nie gebraucht, dort wird nichts angelegt).</summary>
    public static VerknuepfungUmgebung Umgebung => Echt.Value;

    /// <summary>Zeile neben dem Knopf (W2-2, W2-3).</summary>
    public static string StatusText() => Verknuepfung.StatusText(Umgebung);

    /// <summary>
    /// create_and_report(): anlegen oder erneuern; bei Erfolg Merker "erledigt". Text mit Umbruchstellen in Pfaden.
    /// Sammelliste 77 (Weisung P8-3): Das Anlegen (xdg-user-dir und gio je bis 5 s) laeuft im Hintergrund, die
    /// Oberflaeche bleibt bedienbar; der Merker wird danach wieder auf dem Oberflaechen-Thread gesetzt.
    /// </summary>
    public static async Task<string> AnlegenUndMeldenAsync()
    {
        try
        {
            var umgebung = Umgebung;
            var ergebnis = await Task.Run(() => Verknuepfung.Anlegen(umgebung));
            if (ergebnis.Ok)
            {
                Zustand.Aendere(werte => Verknuepfung.Setzen(werte, Verknuepfung.Erledigt));
            }
            return Brechbar(ergebnis.Text);
        }
        catch (Exception e)
        {
            Fehlerprotokoll.Schreiben(e);
            return VerknuepfungTexte.Fehler.Replace("%s", e.GetType().Name, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// maybe_ask_shortcut(): beim Start nach Ausbildungsfrage und Rundgang einmal fragen (Ja / Nein / Nicht mehr
    /// fragen). "Ja" zeigt das Ergebnis mit OK, "Nicht mehr fragen" setzt den Merker "nie", "Nein" fragt beim
    /// naechsten Start wieder. Bei nur lesbaren Einstellungen und im Start ohne Bedienung nie.
    /// </summary>
    public static async Task StartFrageAsync()
    {
        try
        {
            if (MainView.AutoModus || !Unterstuetzt || Zustand.Einstellungen.NurAnzeige || !Verknuepfung.SollFragen(Zustand.Werte, Umgebung))
            {
                return;
            }
            var wahl = await Dialog.Zeigen(VerknuepfungTexte.FrageTitel, VerknuepfungTexte.FrageText,
                VerknuepfungTexte.Ja, VerknuepfungTexte.Nein, VerknuepfungTexte.NieFragen);
            if (wahl == 0)
            {
                var text = await AnlegenUndMeldenAsync();
                Geaendert?.Invoke();
                await Dialog.Zeigen(VerknuepfungTexte.FrageTitel, text, Dialog.Ok);
            }
            else if (wahl == 2)
            {
                Zustand.Aendere(werte => Verknuepfung.Setzen(werte, Verknuepfung.Nie));
            }
        }
        catch (Exception e)
        {
            Fehlerprotokoll.Schreiben(e);
        }
    }

    /// <summary>Nach "Ja" in der Rueckfrage: die Zeile in den Optionen neu zeigen (settings_view.lbl_shortcut).</summary>
    public static event Action? Geaendert;

    /// <summary>Lange Pfade duerfen nach "/" und "-" umbrechen (U+200B ohne Breite), sonst trennt die Layoutpruefung Woerter.</summary>
    public static string Brechbar(string text)
    {
        var aus = new System.Text.StringBuilder(text.Length * 2);
        foreach (var zeichen in text)
        {
            aus.Append(zeichen);
            if (zeichen is '/' or '-')
            {
                aus.Append('​');
            }
        }
        return aus.ToString();
    }

    private static byte[]? Symbol()
    {
        try
        {
            using var strom = AssetLoader.Open(new Uri("avares://Lernplattform.App/Assets/icon.png"));
            using var puffer = new MemoryStream();
            strom.CopyTo(puffer);
            return puffer.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

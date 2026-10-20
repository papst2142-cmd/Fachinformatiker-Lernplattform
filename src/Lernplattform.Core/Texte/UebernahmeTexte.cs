using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 2, L5: Texte des Dialogs "Lernstand übernehmen" nach dem verbindlichen Wortlaut
/// (Fi-Austausch anlagen/P2-4_Wortlaut_L5.md, E2 Chat); mit (Py) markierte Teile zeichengleich
/// aus Python v0.63.2 (fisi_sicherung, fisi_sync). Vergleichswerte G22.
/// </summary>
public static class UebernahmeTexte
{
    // 1. Knoepfe, Titel, Dateiwahl
    public const string Knopf = "Lernstand übernehmen";
    public const string DateiwahlTitel = "Lernstand übernehmen: Datei wählen";
    public const string FilterSicherung = "Lernstand-Sicherung (*.fisisicherung)";   // (Py) FILE_TYPE + Muster
    public const string FilterSicherungMuster = "*.fisisicherung";                   // (Py) BACKUP_EXT
    public const string FilterAlle = "Alle Dateien";                                 // (Py) app_gui 8073
    public const string Titel = "Lernstand übernehmen";
    public const string Zusammenfuehren = "Zusammenführen";                           // (Py) BTN_MERGE
    public const string Abbrechen = "Abbrechen";                                     // (Py) BTN_CANCEL
    public const string Ok = "OK";                                                   // (Py) Handy app.info

    // 2. Vorschau
    public const string Vorschau = "Vorschau";
    public const string QuelleSicherung = "Quelle: Sicherung der Lernplattform";
    public const string QuelleAbgleich = "Quelle: Abgleich-Datei";
    public const string QuelleAlteDb = "Quelle: Datenbank der alten App";
    public const string ZeileNeu = "Neu auf diesem Gerät: %s";
    public const string ZeileVorhanden = "Schon vorhanden: %s";
    public const string ZeileEntfernt = "Wegen einer Löschung entfernt: %s";
    public const string ZeileBleibtGeloescht = "Bleiben gelöscht: %s";
    public const string HinweisDoppelt = "Zusammenführen übernimmt, was auf diesem Gerät noch fehlt – doppelt wird nichts.";   // (V, Py)
    public const string HinweisNichts = "Die Zahlen zeigen, was beim Übernehmen passieren würde. Solange du nicht „Zusammenführen“ wählst, ändert sich nichts.";   // (V)
    public const string HinweisKopie = "Vorher wird dein jetziger Lernstand als Kopie im Ordner „sicherungen“ abgelegt. Diese Kopien löscht die Lernplattform nie von allein.";   // (V)

    // 3. Ergebnis
    public const string EinleitungSicherung = "Sicherung eingespielt:";   // (Py)
    public const string EinleitungAbgleich = "Abgleich-Datei eingelesen:";
    public const string EinleitungAlteDb = "Alte Datenbank eingelesen:";
    public const string KernUebernommen = "%s übernommen.";                   // (Py)
    public const string KernNichts = "Nichts übernommen.";                    // (Py)
    public const string KernAllesDa = "Es fehlte nichts, alles war schon da."; // (Py)
    public const string BleibtGeloeschtEins = "1 Eintrag bleibt gelöscht, weil er auf diesem Gerät gelöscht wurde.";
    public const string BleibtGeloeschtMehr = "%s bleiben gelöscht, weil sie auf diesem Gerät gelöscht wurden.";
    public const string Entfernt = "%s wegen einer Löschung entfernt.";       // (Py) removed_text
    public const string KopieLiegt = "Dein bisheriger Lernstand liegt als Kopie im Ordner „sicherungen“: %s.";   // (V)

    // 4. Hinweise zu Loeschmarken
    public const string UngueltigSicherung = "Die Sicherung enthält eine ungültige Löschmarke. Sie wurde nicht angewendet.";
    public const string UngueltigAbgleich = "Die Abgleich-Datei enthält eine ungültige Löschmarke. Sie wurde nicht angewendet.";   // (Py) MSG_INVALID_MARKER
    public const string ZukunftSicherung = "Die Sicherung enthält eine Löschmarke in der Zukunft. Sie wurde nicht angewendet.";      // (Py) MSG_FUTURE_BACKUP
    public const string ZukunftAbgleich = "Die Abgleich-Datei enthält eine Löschmarke in der Zukunft. Sie wurde nicht angewendet."; // (V)

    // 5. Fehlermeldungen
    public const string KeineSicherung = "Diese Datei ist weder eine Sicherung der Lernplattform noch eine Abgleich-Datei noch die Datenbank der alten App.";
    public const string ZuNeu = "Die Sicherung stammt aus Version %s. Bitte zuerst die App aktualisieren.";   // (Py) MSG_NEWER
    public const string PruefsummeFalsch = "Die Sicherung ist beschädigt (Prüfsumme stimmt nicht).";           // (Py) MSG_DAMAGED
    public const string InhaltKaputt = "Die Sicherung ist beschädigt (der Inhalt hat ein unerwartetes Format)."; // (Py) MSG_BROKEN
    public const string AbgleichDateiKaputt = "Die Abgleich-Datei ist beschädigt oder hat ein unerwartetes Format.";
    public const string DatenbankNichtLesbar = "Die Datenbank der alten App lässt sich nicht lesen. Sie ist vermutlich beschädigt.";
    public const string GleicheDatei = "Du hast die Lerndatenbank dieses Geräts selbst gewählt. Wähle eine Sicherung, eine Abgleich-Datei oder eine Kopie der alten Datenbank.";
    public const string DateiNichtLesbar = "Die Datei lässt sich nicht öffnen oder lesen.";
    public const string NurLesen = "Der Lernstand lässt sich nicht übernehmen. %s";
    public const string VorschauGescheitert = "Die Vorschau ließ sich nicht berechnen. Es wurde nichts geändert.";
    public const string SicherungGescheitert = "Die Kopie deines jetzigen Lernstands ließ sich nicht anlegen. Es wurde nichts übernommen, dein Lernstand ist unverändert. Prüfe den freien Speicherplatz.";
    public const string SchreibenGescheitert = "Das Übernehmen ist fehlgeschlagen. Es wurde nichts übernommen, dein Lernstand ist wie vorher. Die Datenbank ist gerade gesperrt (z. B. von einem anderen Programm), schreibgeschützt oder die Platte ist voll.";

    /// <summary>
    /// Dateiname der Kopie im Ergebnis (Weisung P2-6 A3): am Handy darf er nach „_“ umbrechen
    /// (unsichtbares Zeichen U+200B ohne Breite), sonst trennt ein schmaler Bildschirm mit großer
    /// Schrift ihn mitten in einer Zahl. Am PC unverändert, damit der Name kopierbar bleibt.
    /// </summary>
    public static string KopieName(string dateiname, bool handy) =>
        handy ? dateiname.Replace("_", "_\u200B", StringComparison.Ordinal) : dateiname;

    /// <summary>plural(n, "Eintrag", "Einträge") wie Python.</summary>
    public static string Eintraege(int anzahl) => DashboardTexte.Plural(anzahl, "Eintrag", "Einträge");

    /// <summary>Zeile "Quelle: …" der Vorschau.</summary>
    public static string QuelleZeile(UebernahmeQuelle quelle) => quelle switch
    {
        UebernahmeQuelle.Sicherung => QuelleSicherung,
        UebernahmeQuelle.AbgleichDatei => QuelleAbgleich,
        _ => QuelleAlteDb,
    };

    /// <summary>Die vier Zahlzeilen der Vorschau (immer alle, in dieser Reihenfolge).</summary>
    public static IReadOnlyList<string> Zahlzeilen(UebernahmeBericht bericht) =>
    [
        Beschriftungen.Einsetzen(ZeileNeu, Eintraege(bericht.Neu)),
        Beschriftungen.Einsetzen(ZeileVorhanden, Eintraege(bericht.SchonVorhanden)),
        Beschriftungen.Einsetzen(ZeileEntfernt, Eintraege(bericht.EntferntDurchLoeschmarke)),
        Beschriftungen.Einsetzen(ZeileBleibtGeloescht, Eintraege(bericht.NichtUebernommen)),
    ];

    /// <summary>Hinweise zu Loeschmarken je Quelle (alte Datenbank: keiner).</summary>
    /// <param name="ungueltigInDatei">Ungueltige Marken der Datei selbst (nicht die eigenen).</param>
    public static IReadOnlyList<string> Hinweise(UebernahmeQuelle quelle, int ungueltigInDatei, int zukunft)
    {
        var hinweise = new List<string>();
        if (quelle == UebernahmeQuelle.AlteDatenbank)
        {
            return hinweise;
        }
        var sicherung = quelle == UebernahmeQuelle.Sicherung;
        if (ungueltigInDatei > 0)
        {
            hinweise.Add(sicherung ? UngueltigSicherung : UngueltigAbgleich);
        }
        if (zukunft > 0)
        {
            hinweise.Add(sicherung ? ZukunftSicherung : ZukunftAbgleich);
        }
        return hinweise;
    }

    /// <summary>Ergebnis nach dem Zusammenfuehren: Einleitung, Kern und Saetze, je mit einem Leerzeichen.</summary>
    public static string Ergebnis(UebernahmeBericht bericht, string? sicherungsName)
    {
        var teile = new List<string>
        {
            bericht.Quelle switch
            {
                UebernahmeQuelle.Sicherung => EinleitungSicherung,
                UebernahmeQuelle.AbgleichDatei => EinleitungAbgleich,
                _ => EinleitungAlteDb,
            },
            bericht.Neu > 0 ? Beschriftungen.Einsetzen(KernUebernommen, Eintraege(bericht.Neu))
                : bericht.NichtUebernommen > 0 ? KernNichts : KernAllesDa,
        };
        if (bericht.NichtUebernommen > 0)
        {
            teile.Add(bericht.NichtUebernommen == 1 ? BleibtGeloeschtEins
                : Beschriftungen.Einsetzen(BleibtGeloeschtMehr, Eintraege(bericht.NichtUebernommen)));
        }
        if (bericht.EntferntDurchLoeschmarke > 0)
        {
            teile.Add(Beschriftungen.Einsetzen(Entfernt, Eintraege(bericht.EntferntDurchLoeschmarke)));
        }
        if (sicherungsName is not null)
        {
            teile.Add(Beschriftungen.Einsetzen(KopieLiegt, sicherungsName));
        }
        teile.AddRange(Hinweise(bericht.Quelle, bericht.UngueltigInDatei, bericht.ZukunftsMarken));
        return string.Join(" ", teile);
    }

    /// <summary>Fehlermeldung zu einem Code. vorschau: Rechenfehler der Vorschau zeigen immer "Vorschau scheitert".</summary>
    public static string Fehler(UebernahmeFehler fehler, string? version, string? nurLesenGrund, bool vorschau, UebernahmeQuelle? quelle = null) => fehler switch
    {
        _ when vorschau && fehler == UebernahmeFehler.SchreibenGescheitert => VorschauGescheitert,
        UebernahmeFehler.KeineSicherung => KeineSicherung,
        // Abgleich-Datei mit neuerem Format nennt keine App-Version (Meldung P2-4)
        UebernahmeFehler.ZuNeu when quelle == UebernahmeQuelle.AbgleichDatei => AbgleichDateiKaputt,
        UebernahmeFehler.ZuNeu => Beschriftungen.Einsetzen(ZuNeu, version ?? "?"),
        UebernahmeFehler.PruefsummeFalsch => PruefsummeFalsch,
        UebernahmeFehler.InhaltKaputt => InhaltKaputt,
        UebernahmeFehler.AbgleichDateiKaputt => AbgleichDateiKaputt,
        UebernahmeFehler.DatenbankNichtLesbar => DatenbankNichtLesbar,
        UebernahmeFehler.GleicheDatei => GleicheDatei,
        UebernahmeFehler.NurLesen => NurLesenText(nurLesenGrund),
        UebernahmeFehler.SicherungGescheitert => SicherungGescheitert,
        _ => SchreibenGescheitert,
    };

    /// <summary>Grund neben dem gesperrten Knopf (MSG_READ_ONLY mit DB_READ_ONLY_LINES, Python strip()).</summary>
    public static string NurLesenText(string? grund) =>
        Beschriftungen.Einsetzen(NurLesen, grund is not null && Lernstand.NurLesenZeilen.TryGetValue(grund, out var zeile) ? zeile : "").Trim();
}

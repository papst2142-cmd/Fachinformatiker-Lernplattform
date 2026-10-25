using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Phase 7, W7b (Weisung P7-13): Schalter in Bank und Amt. Die Bank zeigt die Seite "Kredite" der
/// Firmen-Tafel (_firm_kredite), das Amt die Gruendung (_firm_founding) und nach der Gruendung die
/// Umsatzsteuer (_tax_card). Aufnehmen, Abloesen und Gruenden gehen hier ueber die Schalter-Regel
/// (nur im geoeffneten Ort, in dem die Figur steht); die Reiter der Firmen-Tafel bleiben wie bisher.
/// </summary>
public sealed partial class SpielTafel
{
    /// <summary>Ort des Schalters, dessen Seite oben liegt (null = Firmen-Tafel oder andere Seite).</summary>
    private string? _schalter;

    /// <summary>Seite am Schalter des Orts, in dem die Figur steht (oder <paramref name="ort"/>).</summary>
    public void SchalterOeffnen(string? ort = null)
    {
        ort ??= _s.Spiel.Ort;
        if (ort is null || !Schalter.Orte.Contains(ort))
        {
            return;
        }
        _stapel.Clear();
        Oeffnen(Krumen("SPIEL", ort == Schalter.Bank ? "BANK" : "AMT"), () => ort == Schalter.Bank ? BankSeite(Stand) : AmtSeite(Stand));
    }

    private IEnumerable<Control?> BankSeite(Spielstand stand)
    {
        _schalter = Schalter.Bank;
        yield return T(PyFormat.Prozent("Kontostand: %s", Euro(stand.Geld)), 14, "text", true);
        if (!Py.Wahr(stand.Firma))
        {
            yield return Karte(Py.Str(Wirtschaft.KreditRegeln(I)["bank"]), [
                T("Kredite gibt die Bank deiner Firma. Gründen kannst du sie auf dem Amt.", 14, "text_soft"),
            ], "cyan");
            yield break;
        }
        foreach (var teil in FirmaKredite(stand))
        {
            yield return teil;
        }
    }

    private IEnumerable<Control?> AmtSeite(Spielstand stand)
    {
        _schalter = Schalter.Amt;
        if (!Py.Wahr(stand.Firma))
        {
            // mit Firma steht der Kontostand im Firmenkopf
            yield return T(PyFormat.Prozent("Kontostand: %s", Euro(stand.Geld)), 14, "text", true);
            yield return FirmenGruendung(stand);
            yield break;
        }
        yield return FirmenKopf(stand);
        yield return Wirtschaft.SteuerAktiv(stand)
            ? SteuerKarte(stand)
            : Karte("Umsatzsteuer", [T("Bei deiner Schwierigkeit fällt keine Umsatzsteuer an.", 14, "text_soft")], "orange");
    }

    /// <summary>Kredit aufnehmen: am Schalter der Bank mit der Schalter-Regel, sonst wie die Firmen-Tafel.</summary>
    private PyObjekt KreditAufnehmenAm(Spielablauf a, object? summe, object? laufzeit, object? paket = null) =>
        _schalter == Schalter.Bank ? a.BankKreditAufnehmen(_s.Stadt, summe, laufzeit, paket) : a.KreditAufnehmen(summe, laufzeit, paket);

    private PyObjekt KreditAbloesenAm(Spielablauf a, object? kredit) =>
        _schalter == Schalter.Bank ? a.BankKreditAbloesen(_s.Stadt, kredit) : a.KreditAbloesen(kredit);

    private PyObjekt GruendenAm(Spielablauf a, string? name) =>
        _schalter == Schalter.Amt ? a.AmtGruenden(_s.Stadt, name) : a.Gruenden(name);

    private static bool IstSchalterAktion(string aktion) => aktion == Innenraum.AktionSchalter;
}

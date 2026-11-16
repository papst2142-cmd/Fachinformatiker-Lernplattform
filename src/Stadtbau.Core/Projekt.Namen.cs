using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 2): Stadtteil-Namen als Schritte im Verlauf (Rueckgaengig geht). Ein abgelehnter
/// Stand aendert nichts und macht keinen Schritt; Rueckgabe dann der Grund (<see cref="StadtteilNamen"/>).
/// </summary>
public sealed partial class Projekt
{
    public IReadOnlyList<Stadtteile.Bereich?> StadtteilEintraege() => StadtteilNamen.Eintraege(AussenStand);

    public string? StadtteilNeu(string name, double x1, double z1, double x2, double z2) =>
        AussenAendern(Texte.StadtteilNeuSchritt, a => StadtteilNamen.Neu(a, new Stadtteile.Bereich(name, x1, z1, x2, z2)));

    public string? StadtteilUmbenennen(int nummer, string name) =>
        AussenAendern(Texte.StadtteilUmbenannt, a => StadtteilNamen.Umbenennen(a, nummer, name));

    public string? StadtteilBereich(int nummer, double x1, double z1, double x2, double z2, bool verschoben) =>
        AussenAendern(verschoben ? Texte.StadtteilVerschoben : Texte.StadtteilGroesse, a => StadtteilNamen.Bereich(a, nummer, x1, z1, x2, z2));

    public string? StadtteilLoeschen(int nummer) => AussenAendern(Texte.StadtteilGeloescht, a => StadtteilNamen.Loeschen(a, nummer));
}

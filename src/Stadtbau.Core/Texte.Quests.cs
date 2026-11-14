using System.Globalization;

namespace Stadtbau.Core;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186, Kurzplan Meldung P12-111): Texte des Quest-Editors, des Questgeber-Editors,
/// der Verkettungen und der Pruefregeln. Gleiche Regeln wie der Rest von <see cref="Texte"/>: einfache Worte, ein Satz je Tipp.
/// </summary>
public static partial class Texte
{
    // ---- Reiter ----
    public const string Quests = "Quests";
    public const string Questgeber = "Questgeber";
    public const string TippQuests = "Vorhandene Quests ansehen, umhängen oder kopieren und neue Quests (Auswahl oder Zuordnung) schreiben.";
    public const string TippQuestgeber = "Personen, die Quests vergeben: drinnen oder draußen hinstellen, eigene anlegen, Aussehen wählen.";

    /// <summary>Welle Q2: Regel der Ladepruefung des Spiels (Questebene), die die Pruefung des Werkzeugs nicht schon meldet.</summary>
    public const string QuestSpielTipp = "So lädt das Spiel die Stadt nicht. Die Quest oder den Questgeber ändern.";

    // ---- Quest-Editor ----
    public const string QuestNeuTitel = "Neue Quest";
    public const string QuestKopieZusatz = " (Kopie)";
    public const string QuestNeu = "Neue Quest";
    public const string QuestNeuTipp = "Legt eine neue Quest an (Typ Auswahl oder Zuordnung).";
    public const string QuestKopieren = "Als neue Quest kopieren";
    public const string QuestKopierenTipp = "Legt eine neue Quest mit denselben Texten an, die du dann ändern kannst; die vorhandene bleibt unverändert.";
    public const string QuestLoeschen = "Quest löschen";
    public const string QuestLoeschenFrage = "Diese neue Quest löschen? Quests, die nach ihr kommen, verlieren die Bedingung.";
    public const string QuestBestandHinweis = "Vorhandene Quest aus dem Spiel: Texte bleiben, du kannst nur den Questgeber umhängen oder sie als neue Quest kopieren.";
    public const string QuestSuche = "Quests suchen";
    public const string QuestFilterAlle = "Alle";
    public const string QuestFilterTyp = "Typ";
    public const string QuestFilterQuestgeber = "Questgeber";
    public const string QuestFilterOrt = "Ort";
    public const string QuestOhne = "ohne Questgeber";
    public const string QuestKeine = "Keine Quest gefunden.";
    public const string QuestNichtsGewaehlt = "Wähle links eine Quest oder lege eine neue an.";
    public const string QuestTypAuswahl = "Auswahl";
    public const string QuestTypZuordnung = "Zuordnung";
    public const string QuestFeldTyp = "Typ";
    public const string QuestFeldTitel = "Titel";
    public const string QuestFeldTicket = "Ticket (Text der Aufgabe)";
    public const string QuestFeldFrage = "Frage";
    public const string QuestFeldHilfe = "Hilfe";
    public const string QuestFeldErklaerung = "Erklärung nach der Antwort";
    public const string QuestFeldOption = "Antwort";
    public const string QuestFeldAntwort = "Richtige Antwort";
    public const string QuestFeldPaar = "Paar";
    public const string QuestFeldPaarLinks = "Begriff";
    public const string QuestFeldPaarRechts = "passt zu";
    public const string QuestFeldThema = "Thema";
    public const string QuestFeldGeld = "Geld (€)";
    public const string QuestFeldRuf = "Ruf";
    public const string QuestFeldAbTag = "Frei ab Tag";
    public const string QuestFeldNach = "Erst nach";
    public const string QuestFeldQuestgeber = "Questgeber";
    public const string QuestNachKeine = "keine Bedingung";
    public const string QuestOptionDazu = "Antwort dazu";
    public const string QuestOptionWeg = "Antwort weg";
    public const string QuestPaarDazu = "Paar dazu";
    public const string QuestPaarWeg = "Paar weg";
    public const string QuestRichtig = "richtig";
    public static readonly IReadOnlyDictionary<string, string> ThemenNamen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["systeme"] = "Systeme", ["netzwerk"] = "Netzwerk", ["wirtschaft"] = "Wirtschaft", ["sicherheit"] = "Sicherheit",
        ["datenbanken"] = "Datenbanken",
    };

    /// <summary>Name eines Felds fuer den Verlauf (Rueckgaengig: „Rückgängig: Titel“).</summary>
    public static string QuestFeldName(string feld) => feld switch
    {
        "typ" => QuestFeldTyp,
        "titel" => QuestFeldTitel,
        "ticket" => QuestFeldTicket,
        "frage" => QuestFeldFrage,
        "optionen" => QuestFeldOption,
        "antwort" => QuestFeldAntwort,
        "paare" => QuestFeldPaar,
        "hilfe" => QuestFeldHilfe,
        "erklaerung" => QuestFeldErklaerung,
        "cat" => QuestFeldThema,
        "belohnung" => QuestFeldGeld,
        "ab_tag" => QuestFeldAbTag,
        "nach" => QuestFeldNach,
        _ => feld,
    };

    public static string QuestTypName(string typ) => typ switch
    {
        "auswahl" => QuestTypAuswahl,
        "zuordnung" => QuestTypZuordnung,
        _ => typ,
    };

    // ---- Arbeitsort (Welle E4, S 0.53.0, Weisung P12-247) ----
    public const string QuestArbeitsortTitel = "Arbeitsort";
    public const string QuestArbeitsortErklaerung = "Wo die Arbeit stattfindet. Ohne Arbeitsort gilt die Quest am Platz der Person, die sie vergibt.";
    public const string QuestArbeitsortBestand = "Vorhandene Quests behalten ihren Ort aus dem Spiel; ein Arbeitsort geht nur bei neuen Quests.";
    public const string QuestArbeitsortKeiner = "kein eigener Arbeitsort";
    public const string QuestArbeitsort = "Arbeitsort";
    public const string QuestArbeitsortOrt = "Gebäude";
    public const string QuestArbeitsortRaum = "Raum am Arbeitsort";
    public const string QuestArbeitsortEtage = "Etage am Arbeitsort";
    public const string QuestArbeitsortStelle = "Stelle am Arbeitsort";
    public const string QuestArbeitsortStelleText = "Stelle: {0}";
    public const string QuestArbeitsortStelleAuto = "Stelle: die erste freie Stelle im Raum (wird automatisch gesetzt).";
    public const string QuestArbeitsortStelleWaehlen = "Stelle wählen";
    public const string QuestArbeitsortStelleWaehlenTipp = "Danach in den Grundriss tippen, wo die Arbeit stattfindet.";
    public const string QuestArbeitsortStelleHinweis = "Tippe auf die Stelle im Grundriss. Escape bricht ab.";
    public const string QuestArbeitsortWartet = "Questgeber wartet am Arbeitsort";
    public const string QuestArbeitsortWartetTipp = "Ja: die Person geht für diese Quest an den Arbeitsort und wartet dort. Nein: sie bleibt an ihrem Platz, die Aufgabe liegt im Raum am Arbeitsort.";
    public const string QuestArbeitsortEntfernen = "Arbeitsort entfernen";
    public const string QuestArbeitsortEntfernenTipp = "Die Quest gilt danach wieder am Platz der Person, die sie vergibt.";
    public const string QuestArbeitsortZiehenTipp = "In der Draufsicht die Markierung „A“ mit dem Finger oder der Maus auf ein anderes Haus ziehen. Escape bricht ab.";
    public const string QuestArbeitsortZiehenUngueltig = "Dieses Haus kann kein Arbeitsort sein: nur Häuser mit Ort und Innenraum (Büro, Kunden, Geschäfte, Schalter).";
    public const string QuestArbeitsortZiehenUeber = "Arbeitsort: {0}";
    public const string QuestArbeitsortZiehenKeinHaus = "Arbeitsort: hier ist kein Haus";
    public const string QuestArbeitsortZiehenAbgebrochen = "Ziehen abgebrochen, der Arbeitsort bleibt.";
    public const string QuestArbeitsortGleich = "Der Arbeitsort bleibt, die Marke liegt schon auf diesem Haus.";
    public const string QuestArbeitsortGesetzt = "Arbeitsort jetzt: {0}.";
    public const string QuestArbeitsortZeigen = "In der Draufsicht zeigen";
    public const string QuestArbeitsortZeigenTipp = "Öffnet die Draufsicht: Linie von der Person zum Arbeitsort, die Marke „A“ lässt sich auf ein anderes Haus ziehen.";
    public const string QuestArbeitsortKeineStelle = "An diesem Ort gibt es keine freie Stelle, die vom Eingang aus erreichbar ist.";
    public const string QuestArbeitsortRaumKurz = "Raum";
    public static string QuestArbeitsortEtageName(int etage) => etage == 0 ? "Erdgeschoss" : string.Create(CultureInfo.InvariantCulture, $"{etage}. Etage");
    public const string QuestArbeitsortFundTipp = "Den Arbeitsort in der Quest-Karte ändern oder entfernen.";
    public const string QuestArbeitsortGeschaeft = "im ganzen Geschäft";
    public static string QuestArbeitsortHier(int n) => string.Create(CultureInfo.InvariantCulture, $"Quests mit Arbeitsort hier ({n})");
    public static string QuestArbeitsortInnenHier(int n) => string.Create(CultureInfo.InvariantCulture, $"Arbeitsort hier: {n} {(n == 1 ? "Quest" : "Quests")}");
    public const string QuestArbeitsortHierZeile = "neu · ab Tag {0} · Questgeber {1}";
    public const string QuestArbeitsortOeffnen = "Quest öffnen";
    public const string QuestArbeitsortOeffnenTipp = "Öffnet die Quest im Reiter „Quests“.";
    public const string QuestArbeitsortMarkeWartet = "Die Marke zeigt, wo {0} wartet.";
    public const string QuestArbeitsortMarkeAuftrag = "Die Marke zeigt, wo die Aufgabe bereitliegt ({0} bleibt an ihrem Platz).";
    public const string QuestArbeitsortMarkeAuftragName = "Aufgabe";
    public const string QuestArbeitsortMarkeQuest = "{0} (Quest)";
    public static string QuestArbeitsortStelleWert(double x, double y) =>
        string.Format(CultureInfo.InvariantCulture, QuestArbeitsortStelleText, string.Create(CultureInfo.InvariantCulture, $"{x:0.##} / {y:0.##}"));

    // ---- Questgeber-Editor ----
    public const string QgNeu = "Neuer Questgeber";
    public const string QgNeuName = "Neue Person";
    public const string QgNeuTipp = "Legt eine eigene Person an, die Quests vergeben kann; danach Platz und Aussehen wählen.";
    public const string QgLoeschen = "Questgeber löschen";
    public const string QgLoeschenFrage = "Diesen Questgeber löschen? Seine Quests bleiben, stehen danach aber „ohne Questgeber“ (rot), bis du sie neu zuweist.";
    public const string QgBestandHinweis = "Person aus dem Spiel: Name und Aussehen bleiben, du kannst sie nur an einen anderen Platz stellen.";
    public const string QgName = "Name";
    public const string QgRolle = "Rolle";
    public const string QgAussehen = "Aussehen";
    public const string QgPlatz = "Platz";
    public const string QgPlatzDrinnen = "drinnen";
    public const string QgPlatzDraussen = "draußen";
    public const string QgPlatzWaehlen = "Platz wählen";
    public const string QgPlatzWaehlenTipp = "Danach in die Karte (draußen) oder in den Grundriss (drinnen) tippen.";
    public const string QgPlatzWaehlenHinweis = "Tippe auf die Stelle, an der die Person stehen soll. Escape bricht ab.";
    public const string QgPlatzZurueck = "Zurück an den alten Platz";
    public const string QgPlatzZurueckTipp = "Stellt die Person wieder dorthin, wo sie im Spiel heute steht.";
    public const string QgBlickName = "Blick";
    public const string QgOrt = "Ort";
    public const string QgEtage = "Etage";
    public const string QgOhnePlatzKurz = "noch kein Platz";
    public const string QgAmAltenPlatz = "am Platz im Spiel";
    public const string QgQuests = "Quests dieser Person";
    public const string QgKeineQuests = "Diese Person vergibt noch keine Quest.";
    public const string QgDraussenZahl = "Draußen: {0} von 12";
    public static string QgDraussenZahlText(int n) => string.Format(CultureInfo.InvariantCulture, QgDraussenZahl, n);
    public static readonly IReadOnlyDictionary<long, string> BlickNamen = new Dictionary<long, string>
    {
        [0] = "nach unten", [90] = "nach rechts", [180] = "nach oben", [270] = "nach links",
    };

    // ---- Verkettungen ----
    public const string VerkettungJeOrt = "je Ort";
    public const string VerkettungJeQuestgeber = "je Questgeber";
    public const string VerkettungTipp = "Zeigt die Zusammenhänge je Ort oder je Person, die die Quests vergibt.";
    public const string VerkettungZiehenTipp = "Pfeil von einer Quest auf eine andere ziehen = „erst nach“ setzen (nur bei neuen Quests).";
    public const string VerkettungOhne = "ohne Questgeber";
    public const string VerkettungZeileJeQuestgeber = "Je Person ein Kasten mit ihren Quests, so viele Spalten, wie ins Fenster passen (weitere darunter: Ansicht verschieben), von links nach rechts nach dem Tag, ab dem die Person im Spiel ist. Linie = „erst nach“. Eine Quest auf eine andere ziehen = „erst nach“ setzen (nur bei neuen Quests). Kasten antippen = Questgeber öffnen.";

    // ---- Pruefregeln Quests ----
    public static string QuestDatenKaputt(string abschnitt) => $"Der Abschnitt „{abschnitt}“ ist beschädigt.";
    public const string QuestDatenKaputtTipp = "Rückgängig machen oder die Sicherung öffnen; das Werkzeug schreibt ihn sonst immer richtig.";
    public static string QuestKennungDoppelt(string id) => $"Die Kennung „{id}“ steht zweimal.";
    public const string QuestKennungDoppeltTipp = "Eine der beiden löschen; Kennungen vergibt das Werkzeug selbst.";
    public static string QgKennungDoppelt(string id) => $"Der Questgeber „{id}“ steht zweimal.";
    public static string QuestBestandGeaendert(string id) => $"Die vorhandene Quest „{id}“ wurde geändert.";
    public const string QuestBestandGeaendertTipp = "Vorhandene Quests behalten ihre Texte; als neue Quest kopieren und dort ändern.";
    public static string QuestUmhaengenUnbekannt(string id) => $"Umhängen nennt eine Quest „{id}“, die es nicht gibt.";
    public const string QuestUmhaengenUnbekanntTipp = "Rückgängig machen; das Spiel kennt diese Quest nicht.";
    public static string QuestOhneQuestgeber(string n) => $"Quest „{n}“ hat keinen Questgeber.";
    public const string QuestOhneQuestgeberTipp = "Rechts unter „Questgeber“ eine Person wählen.";
    public static string QuestQuestgeberUnbekannt(string n) => $"Quest „{n}“ nennt einen Questgeber, den es nicht gibt.";
    public const string QuestQuestgeberUnbekanntTipp = "Rechts unter „Questgeber“ eine vorhandene Person wählen.";
    public static string QuestTypUnbekannt(string n) => $"Quest „{n}“ hat einen Typ, den es in Q1 nicht gibt.";
    public const string QuestTypUnbekanntTipp = "Neue Quests sind „Auswahl“ oder „Zuordnung“.";
    public static string QuestPflichtLeer(string n, string feld) => $"Quest „{n}“: „{feld}“ ist leer.";
    public const string QuestPflichtLeerTipp = "Das Feld ausfüllen; ohne Text kann man die Quest nicht spielen.";
    public static string QuestTextLang(string n, string feld) =>
        $"Quest „{n}“: „{feld}“ ist länger als {QuestDaten.TextHoechstens} Zeichen.";
    public const string QuestTextLangTipp = "Kürzer fassen; lange Texte passen am Handy nicht auf eine Karte.";
    public static string QuestThemaUnbekannt(string n) => $"Quest „{n}“ hat kein bekanntes Thema.";
    public const string QuestThemaUnbekanntTipp = "Ein Thema wählen: Systeme, Netzwerk, Wirtschaft, Sicherheit oder Datenbanken.";
    public static string QuestBelohnung(string n) =>
        $"Quest „{n}“: Geld 0 bis {QuestDaten.GeldHoechstens} € und Ruf 0 bis {QuestDaten.RufHoechstens}.";
    public const string QuestBelohnungTipp = "Die vorhandenen Quests geben höchstens 150 € und 10 Ruf.";
    public static string QuestAbTag(string n) => $"Quest „{n}“: „Frei ab Tag“ muss zwischen 1 und {QuestDaten.AbTagHoechstens} liegen.";
    public const string QuestAbTagTipp = "Einen Tag von 1 bis 365 eintragen.";
    public static string QuestOptionenZahl(string n) =>
        $"Quest „{n}“ braucht 2 bis {QuestDaten.OptionenHoechstens} Antworten.";
    public const string QuestOptionenZahlTipp = "Mit „Antwort dazu“ oder „Antwort weg“ ändern; die vorhandenen Quests haben 4.";
    public static string QuestAntwortFehlt(string n) => $"Quest „{n}“: die richtige Antwort steht nicht unter den Antworten.";
    public const string QuestAntwortTipp = "Bei einer Antwort „richtig“ wählen.";
    public static string QuestOptionenDoppelt(string n) => $"Quest „{n}“ hat zwei gleiche Antworten.";
    public const string QuestOptionenDoppeltTipp = "Jede Antwort anders schreiben.";
    public static string QuestVerneinung(string n, string wort) => $"Quest „{n}“: „{wort}“ in Großschrift verrät die Lösung.";
    public const string QuestVerneinungTipp = "Klein schreiben (nicht, kein, nur …), wie bei den Lerninhalten.";
    public static string QuestLaengsteRichtig(string n) => $"Quest „{n}“: die richtige Antwort ist die längste.";
    public const string QuestLaengsteRichtigTipp = "Die längste Antwort fällt auf; eine falsche Antwort ausführlicher schreiben.";
    public static string QuestPaareZahl(string n) => $"Quest „{n}“ braucht 2 bis {QuestDaten.PaareHoechstens} Paare.";
    public const string QuestPaareZahlTipp = "Mit „Paar dazu“ oder „Paar weg“ ändern; die vorhandenen Quests haben 4 bis 5.";
    public static string QuestPaareDoppelt(string n) => $"Quest „{n}“ hat zwei gleiche Begriffe oder zwei gleiche Zuordnungen.";

    // ---- Pruefregeln Verkettungen ----
    public static string QuestNachUnbekannt(string n) => $"Quest „{n}“ kommt „erst nach“ einer Quest, die es nicht gibt.";
    public const string QuestNachUnbekanntTipp = "Unter „Erst nach“ eine vorhandene Quest oder „keine Bedingung“ wählen.";
    public static string QuestKreis(IReadOnlyList<string> titel) => "Quests warten im Kreis aufeinander: " + string.Join(" → ", titel) + ".";
    public const string QuestKreisTipp = "Bei einer dieser Quests „Erst nach“ ändern; sonst wird keine davon je frei.";
    public static string QuestKetteLang(string n, int laenge) =>
        $"Quest „{n}“ steht am Ende einer Kette aus {laenge} Quests (mehr als {QuestDaten.KetteHoechstens}).";
    public const string QuestKetteLangTipp = "Lange Ketten halten Spieler lange auf; eine Bedingung weglassen.";
    public static string QuestZuFrueh(string n, string ort, long frei, long tag) =>
        $"Quest „{n}“ ist ab Tag {tag} frei, ihr Ort „{ort}“ kommt aber erst ab Tag {frei} ins Spiel.";
    public const string QuestZuFruehTipp = "„Frei ab Tag“ erhöhen oder die Quest einer Person an einem früheren Ort geben.";

    // ---- Pruefregeln Questgeber ----
    public const string QgNameLeer = "Ein Questgeber hat keinen Namen.";
    public static string QgNameLang(string n) => $"Questgeber „{n}“: der Name ist länger als {QuestgeberDaten.NameHoechstens} Zeichen.";
    public static string QgNameDoppelt(string n) => $"Den Namen „{n}“ gibt es zweimal.";
    public const string QgNameTipp = "Einen eigenen, kurzen Namen eintragen.";
    public static string QgNameAnrede(string n, string kurz) => $"Questgeber „{n}“: das Spiel nennt die Person nur „{kurz}“ (erstes Wort des Namens).";
    public const string QgNameAnredeTipp = "Den Vornamen zuerst schreiben, zum Beispiel „Anna Berger“ statt „Frau Berger“.";
    public static string QgAussehenUnbekannt(string n) => $"Questgeber „{n}“ hat ein Aussehen, das es nicht gibt.";
    public const string QgAussehenTipp = "Unter „Aussehen“ neu wählen.";
    public static string QgOhnePlatz(string n) => $"Questgeber „{n}“ hat noch keinen Platz.";
    public const string QgOhnePlatzTipp = "„Platz wählen“ und in die Karte oder den Grundriss tippen.";
    public static string QgPlatzKaputt(string n) => $"Der Platz von „{n}“ ist beschädigt.";
    public static string QgBlick(string n) => $"Questgeber „{n}“ schaut schräg; erlaubt sind nur die vier Richtungen.";
    public const string QgBlickTipp = "Unter „Blick“ eine Richtung wählen.";
    public static string QgDraussenZuViele(int n) => $"{n} Questgeber stehen draußen; erlaubt sind höchstens {QuestgeberDaten.DraussenHoechstens}.";
    public const string QgDraussenZuVieleTipp = "Einige Personen in ein Haus stellen.";
    public static string QgNichtAufKarte(string n) => $"Questgeber „{n}“ steht außerhalb der Karte.";
    public const string QgNichtFreiTipp = "„Platz wählen“ und auf eine freie Stelle am Weg tippen.";
    public static string QgDraussenNichtFrei(string n) => $"Questgeber „{n}“ steht auf einem Haus, Wasser, Baum oder einer Straße.";
    public static string QgNichtErreichbar(string n) => $"Questgeber „{n}“ ist zu Fuß nicht erreichbar.";
    public const string QgNichtErreichbarTipp = "Die Person an den Weg stellen oder einen Weg zu ihr bauen.";
    public static string QgOrtFehlt(string n, string ort) => $"Questgeber „{n}“ steht in „{ort}“, den es in dieser Stadt nicht gibt.";
    public const string QgOrtFehltTipp = "„Platz wählen“ und die Person in einen vorhandenen Ort stellen.";
    public static string QgInnenAussen(string n) => $"Questgeber „{n}“ steht außerhalb des Grundrisses.";
    public static string QgInnenNichtFrei(string n) => $"Questgeber „{n}“ steht auf einem Möbelstück oder in der Wand.";

    // ---- Oberflaeche (Welle Q1) ----
    public static string VerkettungGewaehlt(string name) => name + ", gewählt";
    public const string VerkettungNurNeu = "„Erst nach“ lässt sich nur bei neuen Quests setzen; vorhandene bleiben wie im Spiel.";
    public static string VerkettungGesetzt(string nach, string vor) => $"„{nach}“ kommt jetzt erst nach „{vor}“.";
    public const string QuestNeuAuswahl = "Neue Quest: Auswahl";
    public const string QuestNeuZuordnung = "Neue Quest: Zuordnung";
    public const string QuestNeuAuswahlTipp = "Legt eine neue Quest an, bei der eine von mehreren Antworten richtig ist.";
    public const string QuestNeuZuordnungTipp = "Legt eine neue Quest an, bei der Begriffe einander zugeordnet werden.";
    public const string QuestListeTitel = "Alle Quests";
    public static string QuestTreffer(int n, int alle) => $"{n} von {alle} Quests";
    public const string QuestBestandMarke = "vorhanden";
    public const string QuestNeuMarke = "neu";
    public const string QuestUmgehaengtMarke = "umgehängt";
    public const string QuestFunde = "Prüfen";
    public static string QuestFundeTitel(int rot) => rot == 0 ? "Prüfen – alles grün" : $"Prüfen – {rot} rot";
    public const string QuestFundeLaeuft = "Wird geprüft …";
    public const string QuestFeldTipp = "Änderungen gelten, sobald du das Feld verlässt oder Enter drückst.";
    public const string QuestZeilenTipp = "Eine Zeile je Absatz.";
    public const string QuestOptionWegTipp = "Nimmt diese Antwort heraus (mindestens zwei bleiben).";
    public const string QuestPaarWegTipp = "Nimmt dieses Paar heraus (mindestens zwei bleiben).";
    public const string QuestRichtigTipp = "Diese Antwort ist die richtige.";
    public const string QuestUmhaengenTipp = "Wählt die Person, die die Quest im Spiel vergibt.";
    public const string QuestNachTipp = "Die Quest erscheint erst, wenn die gewählte Quest erledigt ist.";
    public const string QuestTypTipp = "Beim Wechsel fallen die Antworten bzw. Paare des anderen Typs weg.";
    public const string QuestZahlFalsch = "Bitte eine ganze Zahl eintragen.";
    public static string QuestOptionName(int i) => $"Antwort {i}";
    public static string QuestPaarName(int i, bool rechts) => rechts ? $"Paar {i}, passt zu" : $"Paar {i}, Begriff";
    public static string QuestRichtigName(int i) => $"Antwort {i} ist richtig";
    public const string QgListeTitel = "Alle Questgeber";
    public const string QgNichtsGewaehlt = "Wähle links eine Person oder lege eine neue an.";
    public const string QgOrtWahlTipp = "Wo die Person stehen soll: draußen oder in einem Innenraum.";
    public const string QgBlickTipp2 = "Richtung, in die die Person schaut.";
    public const string QgAussehenWahlTipp = "Teil des Aussehens der Person.";
    public static string QgPlatzText(string ort) => $"steht in „{ort}“";
    public static string QgPlatzDraussenText(double x, double y) =>
        string.Format(CultureInfo.InvariantCulture, "steht draußen bei {0:0.##} / {1:0.##}", x, y);
    public static readonly string QgZuVieleDraussen = $"Draußen stehen schon {QuestgeberDaten.DraussenHoechstens} Questgeber; diese Person bitte in ein Haus stellen.";
    public static string QgEtageName(string ort, int etage) => etage == 0 ? ort : $"{ort} – {etage}. Etage";
    public const string QgQuestOeffnenTipp = "Öffnet die Quest im Reiter „Quests“.";
    public static string QgGesetzt(string n) => $"„{n}“ steht jetzt am neuen Platz.";
    public static string QgDreiecke(long d) => string.Format(CultureInfo.GetCultureInfo("de-DE"), "Dreiecke draußen: {0:N0}", d);
    public const string VerkettungNurQuests = "Pfeile gehen nur von einer Quest zu einer Quest.";
    public const string QuestPruefFehler = "Prüfen ist gescheitert: ";
    public static string AussehenTeilName(string teil) => teil switch
    {
        "haut" => "Hautton",
        "frisur" => "Frisur",
        "haarfarbe" => "Haarfarbe",
        "oberteil" => "Oberteil",
        _ => teil,
    };
    public static string ListeWeitere(int n) => $"Weitere zeigen ({n} mehr)";
    public const string ListeWeitereTipp = "Zeigt die nächsten Einträge; mit Suche und Filter findest du eine Quest schneller.";
}

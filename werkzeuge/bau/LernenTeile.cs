// Welle A 0.97.0 (Weisung P12-312 Punkt 3, W2): Lern-Dateien je Fachrichtung beim Bauen aufteilen.
//
// Quelle bleibt allein inhalte_csharp/lernen/*.json. Beim Bauen der App (MSBuild-Aufgabe in LernenTeile.targets) schreibt
// dieser Teiler je Liste und Fachrichtung eine Datei "<liste>.<fr>.json" mit den Eintraegen dieser Fachrichtung und ihrer
// Stelle in der ganzen Datei: [[stelle, eintrag], ...]. Jeder Eintrag wird Byte fuer Byte aus der Quelle kopiert (kein neues
// Schreiben, also genau dieselben Werte). Dazu "teile.json" mit Laenge, SHA-256 der Quelle, Anzahl je Teil und den
// Fachrichtungen mit Platzhaltern ("im_paket": true) je Datei. Die App zerlegt beim Start dann nur die Teile der geladenen
// Fachrichtungen (bei SI rund 2 MB statt 16 MB). Die Tests (InhalteLadenSchnellTests) nehmen denselben Teiler und pruefen
// Eintrag fuer Eintrag mit Stelle gegen die Quelle und das Laden mit Teilen gegen das Laden ohne.
//
// Der Teiler braucht keine JSON-Bibliothek (die MSBuild-Aufgabe kennt nur netstandard): er sucht nur die Grenzen der
// Eintraege und die Felder "fachrichtung" und "im_paket" der obersten Ebene eines Eintrags.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lernplattform.Bau
{
    public static class LernenTeiler
    {
        /// <summary>Listen, die aufgeteilt werden (gleich Lerninhalte.LernenListen).</summary>
        public static readonly string[] Listen = { "karteikarten", "quizfragen", "szenarien_ap2", "projektarbeiten" };

        /// <summary>Weitere Dateien, deren Platzhalter im Index stehen (Liste im Objekt unter diesem Feld).</summary>
        public static readonly string[][] NurIndex = { new[] { "fachgespraech", "fragen" }, new[] { "pruefungssaetze", "saetze" } };

        public const string IndexName = "teile.json";

        /// <summary>Teil fuer Eintraege ohne gueltigen Namen der Fachrichtung (die App liest ihn nie).</summary>
        public const string Ohne = "_ohne";

        /// <summary>
        /// Alle Dateien der Teile und den Index aus den Quellen (Name ohne ".json" -> Inhalt; fehlende Dateien fehlen auch hier).
        /// Ergebnis: Dateiname -> Inhalt (UTF-8).
        /// </summary>
        public static Dictionary<string, byte[]> Teilen(Func<string, byte[]> lesen)
        {
            var dateien = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var index = new StringBuilder("{\"format\":1,\"listen\":{");
            var erste = true;
            foreach (var name in Listen)
            {
                var quelle = lesen(name);
                if (quelle == null)
                {
                    continue;
                }
                var eintraege = Eintraege(quelle, 0, name);
                var teile = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
                for (var i = 0; i < eintraege.Count; i++)
                {
                    var fr = Teil(eintraege[i].Fachrichtung);
                    if (!teile.TryGetValue(fr, out var liste))
                    {
                        teile[fr] = liste = new List<int>();
                    }
                    liste.Add(i);
                }
                foreach (var paar in teile)
                {
                    using (var strom = new MemoryStream())
                    {
                        strom.WriteByte((byte)'[');
                        var komma = false;
                        foreach (var i in paar.Value)
                        {
                            if (komma)
                            {
                                strom.WriteByte((byte)',');
                            }
                            komma = true;
                            var kopf = Encoding.ASCII.GetBytes("[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                            strom.Write(kopf, 0, kopf.Length);
                            strom.Write(quelle, eintraege[i].Start, eintraege[i].Ende - eintraege[i].Start);
                            strom.WriteByte((byte)']');
                        }
                        strom.WriteByte((byte)']');
                        dateien[name + "." + paar.Key + ".json"] = strom.ToArray();
                    }
                }
                index.Append(erste ? "" : ",");
                erste = false;
                IndexEintrag(index, name, quelle, eintraege, teile);
            }
            foreach (var paar in NurIndex)
            {
                var quelle = lesen(paar[0]);
                if (quelle == null)
                {
                    continue;
                }
                var start = ListeImObjekt(quelle, paar[1], paar[0]);
                index.Append(erste ? "" : ",");
                erste = false;
                IndexEintrag(index, paar[0], quelle, start < 0 ? new List<Eintrag>() : Eintraege(quelle, start, paar[0]), null);
            }
            index.Append("}}");
            dateien[IndexName] = Encoding.UTF8.GetBytes(index.ToString());
            return dateien;
        }

        private static void IndexEintrag(StringBuilder index, string name, byte[] quelle, List<Eintrag> eintraege, SortedDictionary<string, List<int>> teile)
        {
            index.Append(Text(name)).Append(":{\"laenge\":").Append(eintraege.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            index.Append(",\"sha256\":").Append(Text(Pruefsumme(quelle)));
            if (teile != null)
            {
                index.Append(",\"teile\":{");
                index.Append(string.Join(",", teile.Select(t => Text(t.Key) + ":" + t.Value.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                index.Append('}');
            }
            var platzhalter = eintraege.Where(e => e.Platzhalter && e.Fachrichtung != null).Select(e => e.Fachrichtung).Distinct().OrderBy(f => f, StringComparer.Ordinal);
            index.Append(",\"platzhalter\":[").Append(string.Join(",", platzhalter.Select(Text))).Append("]}");
        }

        /// <summary>SHA-256 einer Datei (klein geschrieben, hex).</summary>
        public static string Pruefsumme(byte[] daten)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(daten).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>Name des Teils: der Wert des Feldes "fachrichtung", wenn er nur aus a-z besteht, sonst <see cref="Ohne"/>.</summary>
        public static string Teil(string fachrichtung) =>
            fachrichtung != null && fachrichtung.Length > 0 && fachrichtung.All(z => z >= 'a' && z <= 'z') ? fachrichtung : Ohne;

        private static string Text(string s)
        {
            var b = new StringBuilder("\"");
            foreach (var z in s)
            {
                if (z == '"' || z == '\\')
                {
                    b.Append('\\').Append(z);
                }
                else if (z < 0x20)
                {
                    b.Append("\\u").Append(((int)z).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    b.Append(z);
                }
            }
            return b.Append('"').ToString();
        }

        /// <summary>Ein Eintrag der Liste: Grenzen in der Quelle, Feld "fachrichtung" (roh, ohne Anfuehrungszeichen) und Platzhalter.</summary>
        public sealed class Eintrag
        {
            public int Start;
            public int Ende;
            public string Fachrichtung;
            public bool Platzhalter;
        }

        /// <summary>Die Eintraege der Liste, die an <paramref name="start"/> beginnt (bei 0 die ganze Datei als Liste).</summary>
        public static List<Eintrag> Eintraege(byte[] b, int start, string name)
        {
            var i = Leer(b, start == 0 ? Bom(b) : start);
            if (i >= b.Length || b[i] != '[')
            {
                throw new InvalidDataException(name + ".json enthaelt keine Liste");
            }
            var liste = new List<Eintrag>();
            i = Leer(b, i + 1);
            if (i < b.Length && b[i] == ']')
            {
                return liste;
            }
            while (true)
            {
                var ende = Ende(b, i, name);
                var e = new Eintrag { Start = i, Ende = ende };
                if (b[i] == '{')
                {
                    Felder(b, i, name, (schluessel, von, bis) =>
                    {
                        if (schluessel == "fachrichtung" && b[von] == '"')
                        {
                            e.Fachrichtung = Encoding.UTF8.GetString(b, von + 1, bis - von - 2);
                        }
                        else if (schluessel == "im_paket" && bis - von == 4 && Encoding.ASCII.GetString(b, von, 4) == "true")
                        {
                            e.Platzhalter = true;
                        }
                    });
                }
                liste.Add(e);
                i = Leer(b, ende);
                if (i >= b.Length)
                {
                    throw new InvalidDataException(name + ".json: Liste endet nicht");
                }
                if (b[i] == ']')
                {
                    return liste;
                }
                if (b[i] != ',')
                {
                    throw new InvalidDataException(name + ".json: Komma fehlt an Stelle " + i);
                }
                i = Leer(b, i + 1);
            }
        }

        /// <summary>Beginn der Liste im Feld <paramref name="feld"/> des obersten Objekts, -1 ohne Feld.</summary>
        public static int ListeImObjekt(byte[] b, string feld, string name)
        {
            var i = Leer(b, Bom(b));
            if (i >= b.Length || b[i] != '{')
            {
                throw new InvalidDataException(name + ".json enthaelt kein Objekt");
            }
            var gefunden = -1;
            Felder(b, i, name, (schluessel, von, bis) =>
            {
                if (schluessel == feld && gefunden < 0)
                {
                    gefunden = von;
                }
            });
            return gefunden;
        }

        /// <summary>Felder der obersten Ebene eines Objekts: Schluessel (roh) und Grenzen des Werts.</summary>
        private static void Felder(byte[] b, int start, string name, Action<string, int, int> feld)
        {
            var i = Leer(b, start + 1);
            if (b[i] == '}')
            {
                return;
            }
            while (true)
            {
                if (b[i] != '"')
                {
                    throw new InvalidDataException(name + ".json: Schluessel erwartet an Stelle " + i);
                }
                var schluesselEnde = Ende(b, i, name);
                var schluessel = Encoding.UTF8.GetString(b, i + 1, schluesselEnde - i - 2);
                i = Leer(b, schluesselEnde);
                if (b[i] != ':')
                {
                    throw new InvalidDataException(name + ".json: Doppelpunkt fehlt an Stelle " + i);
                }
                i = Leer(b, i + 1);
                var wertEnde = Ende(b, i, name);
                feld(schluessel, i, wertEnde);
                i = Leer(b, wertEnde);
                if (b[i] == '}')
                {
                    return;
                }
                if (b[i] != ',')
                {
                    throw new InvalidDataException(name + ".json: Komma fehlt an Stelle " + i);
                }
                i = Leer(b, i + 1);
            }
        }

        /// <summary>Stelle hinter dem Wert, der an <paramref name="i"/> beginnt.</summary>
        private static int Ende(byte[] b, int i, string name)
        {
            if (i >= b.Length)
            {
                throw new InvalidDataException(name + ".json endet zu frueh");
            }
            if (b[i] == '"')
            {
                for (var j = i + 1; j < b.Length; j++)
                {
                    if (b[j] == '\\')
                    {
                        j++;
                    }
                    else if (b[j] == '"')
                    {
                        return j + 1;
                    }
                }
                throw new InvalidDataException(name + ".json: Text endet nicht");
            }
            if (b[i] == '{' || b[i] == '[')
            {
                var tiefe = 0;
                for (var j = i; j < b.Length; j++)
                {
                    var z = b[j];
                    if (z == '"')
                    {
                        j = Ende(b, j, name) - 1;
                    }
                    else if (z == '{' || z == '[')
                    {
                        tiefe++;
                    }
                    else if (z == '}' || z == ']')
                    {
                        tiefe--;
                        if (tiefe == 0)
                        {
                            return j + 1;
                        }
                    }
                }
                throw new InvalidDataException(name + ".json: Klammer schliesst nicht");
            }
            var k = i;
            while (k < b.Length && b[k] != ',' && b[k] != ']' && b[k] != '}' && b[k] != ' ' && b[k] != '\n' && b[k] != '\r' && b[k] != '\t')
            {
                k++;
            }
            if (k == i)
            {
                throw new InvalidDataException(name + ".json: Wert erwartet an Stelle " + i);
            }
            return k;
        }

        private static int Leer(byte[] b, int i)
        {
            while (i < b.Length && (b[i] == ' ' || b[i] == '\n' || b[i] == '\r' || b[i] == '\t'))
            {
                i++;
            }
            return i;
        }

        private static int Bom(byte[] b) => b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
    }

#if !LERNEN_TEILE_OHNE_MSBUILD
    /// <summary>MSBuild-Aufgabe: liest die Quellen aus <see cref="Quelle"/> und schreibt die Teile nach <see cref="Ziel"/>.</summary>
    public sealed class LernenTeileBauen : Microsoft.Build.Utilities.Task
    {
        [Microsoft.Build.Framework.Required]
        public string Quelle { get; set; }

        [Microsoft.Build.Framework.Required]
        public string Ziel { get; set; }

        public override bool Execute()
        {
            try
            {
                var dateien = LernenTeiler.Teilen(n =>
                {
                    var pfad = Path.Combine(Quelle, n + ".json");
                    return File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;
                });
                Directory.CreateDirectory(Ziel);
                foreach (var alt in Directory.GetFiles(Ziel, "*.json"))
                {
                    if (!dateien.ContainsKey(Path.GetFileName(alt)))
                    {
                        File.Delete(alt);
                    }
                }
                foreach (var paar in dateien)
                {
                    File.WriteAllBytes(Path.Combine(Ziel, paar.Key), paar.Value);
                }
                Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Normal, "Lern-Dateien aufgeteilt: " + dateien.Count + " Dateien");
                return true;
            }
            catch (Exception fehler)
            {
                Log.LogError("Lern-Dateien aufteilen: " + fehler.Message);
                return false;
            }
        }
    }
#endif
}

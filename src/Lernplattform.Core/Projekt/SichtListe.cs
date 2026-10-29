using System.Collections;

namespace Lernplattform.Core.Projekt;

/// <summary>
/// Phase 9 W2: Liste, die bei jedem Zugriff ihre Eintraege neu holt (Fragen fuers Fachgespraech der
/// aktuell gewaehlten Fachrichtung, ohne den Ablauf des Abschlussprojekts neu zu bauen).
/// </summary>
public sealed class SichtListe<T>(Func<IReadOnlyList<T>> holen) : IReadOnlyList<T>
{
    public T this[int index] => holen()[index];

    public int Count => holen().Count;

    public IEnumerator<T> GetEnumerator() => holen().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

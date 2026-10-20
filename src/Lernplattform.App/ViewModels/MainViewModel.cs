using CommunityToolkit.Mvvm.ComponentModel;

namespace Lernplattform.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Titel { get; set; } = "Fachinformatiker Lernplattform";

    /// <summary>Anzeige "A 0.1.0" (Stufe A = Alphaphase, Zahlenversion aus Directory.Build.props).</summary>
    public string Version { get; } = Lernplattform.Aktualisierung.Aktualisierung.Anzeige;

    public string Hinweis { get; } = "Phase 0: Fundament. Noch keine Lernfunktionen.";
}

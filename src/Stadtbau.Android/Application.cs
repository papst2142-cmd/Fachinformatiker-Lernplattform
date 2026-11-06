using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Stadtbau.Android;

[Application]
public class Application : AvaloniaAndroidApplication<StadtbauApp>
{
    protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithInterFont();
}

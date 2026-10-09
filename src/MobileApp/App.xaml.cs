using MobileApp.Core.Session;
using MobileApp.Core.Sync;

namespace MobileApp;

public partial class App : Application
{
    private readonly SyncEngine _sync;

    /// <summary>
    /// The offline queue is sent when the connection returns (and every 30 s), after sign-in and whenever the app
    /// comes back to the foreground (PLAN-MOBILE §3.6 step 4).
    /// </summary>
    public App(SyncEngine sync, SessionService session)
    {
        InitializeComponent();

        _sync = sync;
        _sync.Start();
        session.Changed += (_, _) =>
        {
            if (session.IsSignedIn)
            {
                _ = _sync.TryRunAsync();
            }
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "IntiPlasma" };
        window.Resumed += (_, _) => _ = _sync.TryRunAsync();

        return window;
    }
}

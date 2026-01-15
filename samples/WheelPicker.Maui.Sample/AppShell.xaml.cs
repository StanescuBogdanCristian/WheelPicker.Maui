#if MACCATALYST
using UIKit;
#endif

namespace WheelPicker.Maui.Sample
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

#if MACCATALYST
        Navigated += (_, __) => ForceTabBarVisible();
#endif
        }

#if MACCATALYST
    static void ForceTabBarVisible()
    {
        var root = Platform.GetCurrentUIViewController();
        var tab = FindTabBarController(root);
        if (tab?.TabBar is null) return;

        tab.TabBar.Hidden = false;
        tab.TabBar.Alpha = 1.0f;
    }

    static UITabBarController? FindTabBarController(UIViewController? vc)
    {
        if (vc is null) return null;
        if (vc is UITabBarController tbc) return tbc;

        var presented = vc.PresentedViewController;
        var found = FindTabBarController(presented);
        if (found != null) return found;

        if (vc is UINavigationController nav)
            return FindTabBarController(nav.VisibleViewController);

        if (vc is UISplitViewController split && split.ViewControllers?.Length > 0)
        {
            foreach (var child in split.ViewControllers)
            {
                found = FindTabBarController(child);
                if (found != null) return found;
            }
        }

        foreach (var child in vc.ChildViewControllers ?? Array.Empty<UIViewController>())
        {
            found = FindTabBarController(child);
            if (found != null) return found;
        }

        return null;
    }
#endif
    }
}

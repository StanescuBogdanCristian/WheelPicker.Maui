using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;

namespace WheelPicker.Maui.Sample
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            OnBackPressedDispatcher.AddCallback(this, new OnBackbuttonPressedCallback(this));
        }
    }

    public class OnBackbuttonPressedCallback : OnBackPressedCallback
    {
        private readonly Activity _activity;

        public OnBackbuttonPressedCallback(Activity activity) : base(true)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            var navigation = Microsoft.Maui.Controls.Application.Current?.Windows[0].Page?.Navigation;
            if (navigation is not null &&
                navigation.NavigationStack.Count <= 1 &&
                navigation.ModalStack.Count <= 0)
            {
                _activity.FinishAndRemoveTask();
                Process.KillProcess(Process.MyPid());
            }
        }
    }
}

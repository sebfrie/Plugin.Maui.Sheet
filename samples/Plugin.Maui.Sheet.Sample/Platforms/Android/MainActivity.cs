using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Plugin.Maui.Sheet.Sample;

// A fixed Java name lets the screenshot workflow start it: adb shell am start -n <package>/<package>.MainActivity
[Activity(Name = "com.sebfrie.plugin.maui.sheet.sample.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}

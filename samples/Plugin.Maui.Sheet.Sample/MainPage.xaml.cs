using System.ComponentModel;

namespace Plugin.Maui.Sheet.Sample;

public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		Loaded += OpenRequestedSheet;
	}

	// The screenshot workflow opens a sheet at launch with SAMPLE_SHEET=form|list|passthrough
	// (an intent extra on Android, an environment variable on iOS and Windows).
	void OpenRequestedSheet(object? sender, EventArgs e)
	{
		Loaded -= OpenRequestedSheet;
#if ANDROID
		var sheet = Platform.CurrentActivity?.Intent?.GetStringExtra("SAMPLE_SHEET");
#else
		var sheet = Environment.GetEnvironmentVariable("SAMPLE_SHEET");
#endif
		var viewModel = (MainViewModel)BindingContext;
		switch (sheet)
		{
			case "form":
				viewModel.Name = "Ada Lovelace";
				viewModel.IsFormOpen = true;
				break;
			case "list":
				viewModel.IsListOpen = true;
				break;
			case "passthrough":
				viewModel.IsPassThroughOpen = true;
				break;
		}
	}

	void OnPassThroughDismissAttempted(object? sender, CancelEventArgs e) =>
		((MainViewModel)BindingContext).Status = "That sheet is not dismissible by swiping; use its close button.";

	void OnListDetentChanged(object? sender, SheetDetentChangedEventArgs e) =>
		((MainViewModel)BindingContext).Status = $"List sheet moved to {e.Detent}.";
}

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

	// The screenshot workflow opens a sheet at launch with SAMPLE_SHEET=form|list|passthrough|nav|nav-…
	// (an intent extra on Android, an environment variable on iOS and Windows).
	async void OpenRequestedSheet(object? sender, EventArgs e)
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
			case "nav" or "nav-overflow": // the workflow opens the overflow menu itself
				await OpenEntryAsync();
				break;
			case "nav-pushed":
				await (await OpenEntryAsync()).ChooseCategoryAsync();
				break;
			case "nav-picked":
			{
				var entry = await OpenEntryAsync();
				await entry.ChooseCategoryAsync();
				await Task.Delay(1000);
				await ((CategoryPickerView)((NativeSheet)entry.Parent).NavigationStack[^1]).PickAsync("Groceries");
				break;
			}
			case "nav-alert":
				await (await OpenEntryAsync()).ShowAlertAsync();
				break;
			case "nav-modal":
				await (await OpenEntryAsync()).ShowModalAsync();
				break;
			case "nav-date":
				(await OpenEntryAsync()).OpenDatePicker();
				break;
		}

		async Task<NewEntryView> OpenEntryAsync()
		{
			var entry = await viewModel.OpenNewEntryAsync();
			entry.Fill("Weekly shop");
			return entry;
		}
	}

	void OnPassThroughDismissAttempted(object? sender, CancelEventArgs e) =>
		((MainViewModel)BindingContext).Status = "That sheet is not dismissible by swiping; use its close button.";

	void OnListDetentChanged(object? sender, SheetDetentChangedEventArgs e) =>
		((MainViewModel)BindingContext).Status = $"List sheet moved to {e.Detent}.";
}

using System.ComponentModel;

namespace Plugin.Maui.Sheet.Sample;

public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	void OnPassThroughDismissAttempted(object? sender, CancelEventArgs e) =>
		((MainViewModel)BindingContext).Status = "That sheet is not dismissible by swiping; use its close button.";

	void OnListDetentChanged(object? sender, SheetDetentChangedEventArgs e) =>
		((MainViewModel)BindingContext).Status = $"List sheet moved to {e.Detent}.";
}

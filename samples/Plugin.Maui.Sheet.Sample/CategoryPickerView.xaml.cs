namespace Plugin.Maui.Sheet.Sample;

public partial class CategoryPickerView : ContentView
{
	readonly Action<string> picked;

	public CategoryPickerView(Action<string> picked)
	{
		InitializeComponent();
		this.picked = picked;
		List.ItemsSource = new[] { "Errands", "Groceries", "Health", "Home", "Travel", "Work" };
	}

	public Task PickAsync(string category)
	{
		picked(category);
		// Back to the root view, with the platform's back transition.
		return ((NativeSheet)Parent).PopAsync();
	}

	async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is string category)
			await PickAsync(category);
	}
}

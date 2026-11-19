namespace Plugin.Maui.Sheet.Sample;

public partial class NewEntryView : ContentView
{
	public NewEntryView() => InitializeComponent();

	/// <summary>What was saved, or <see langword="null"/> when the sheet was closed without saving.</summary>
	public string? Saved { get; private set; }

	// The sheet this view is the root of.
	NativeSheet Sheet => (NativeSheet)Parent;

	protected override void OnParentSet()
	{
		base.OnParentSet();
		// Popped is raised only when the user goes back (back button, edge swipe, system back), not for PopAsync.
		if (Parent is NativeSheet sheet)
			sheet.Popped += (_, _) => StatusLabel.Text = "Went back without choosing a category.";
	}

	public Task ChooseCategoryAsync() =>
		Sheet.PushAsync(new CategoryPickerView(category => CategoryLabel.Text = category));

	public Task ShowAlertAsync() =>
		Shell.Current.DisplayAlertAsync("Alert", "Shown from inside the sheet, above it.", "OK");

	public Task ShowModalAsync()
	{
		var close = new Button { Text = "Close", AutomationId = "CloseModal" };
		var page = new ContentPage
		{
			Content = new VerticalStackLayout
			{
				Padding = 24,
				Spacing = 12,
				Children = { new Label { Text = "A modal page pushed from inside the sheet appears above it." }, close },
			},
		};
		close.Clicked += async (_, _) => await page.Navigation.PopModalAsync();
		return Shell.Current.Navigation.PushModalAsync(page);
	}

	public void OpenDatePicker() => DuePicker.IsOpen = true;

	public void Fill(string title)
	{
		TitleEntry.Text = title;
		DuePicker.Date = new DateTime(2026, 11, 20);
	}

	void OnTitleChanged(object? sender, TextChangedEventArgs e) => SaveItem.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);

	void OnSave(object? sender, EventArgs e)
	{
		Saved = $"\"{TitleEntry.Text}\" in {CategoryLabel.Text}, due {DuePicker.Date:d}";
		Sheet.IsOpen = false;
	}

	void OnMenuItem(object? sender, EventArgs e) => StatusLabel.Text = $"{((ToolbarItem)sender!).Text} tapped.";

	async void OnChooseCategory(object? sender, EventArgs e) => await ChooseCategoryAsync();

	async void OnShowAlert(object? sender, EventArgs e) => await ShowAlertAsync();

	async void OnShowModal(object? sender, EventArgs e) => await ShowModalAsync();
}

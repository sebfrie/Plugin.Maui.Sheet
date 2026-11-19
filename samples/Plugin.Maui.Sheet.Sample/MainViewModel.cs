using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Plugin.Maui.Sheet.Sample;

public sealed class MainViewModel : INotifyPropertyChanged
{
	string name = string.Empty;
	string status = "Open a sheet. The list behind the pass-through sheet stays scrollable.";
	bool isFormOpen;
	bool isListOpen;
	bool isPassThroughOpen;
	SheetDetent? listDetent;

	readonly INativeSheetService sheets;

	public MainViewModel(INativeSheetService sheets)
	{
		this.sheets = sheets;
		OpenFormCommand = new Command(() => IsFormOpen = true);
		OpenListCommand = new Command(() => IsListOpen = true);
		OpenPassThroughCommand = new Command(() => IsPassThroughOpen = true);
		SaveCommand = new Command(() =>
		{
			Status = $"Saved \"{Name}\".";
			Name = string.Empty;
			IsFormOpen = false;
		});
		ExpandListCommand = new Command(() => ListDetent = SheetDetent.Large);
		ShowServiceSheetCommand = new Command(async () =>
		{
			Status = "Service sheet open…";
			var fruit = await sheets.ShowSheetAsync<ServiceSheetView, string>(sheet =>
			{
				sheet.Title = "Pick a fruit";
				sheet.Subtitle = "INativeSheetService";
				sheet.Detents = [SheetDetent.FitContent, SheetDetent.Large];
				sheet.ShowNativeCloseButton = true;
			});
			Status = fruit is null ? "Service sheet dismissed without a choice." : $"The service sheet returned \"{fruit}\".";
		});
		OpenNewEntryCommand = new Command(async () => await OpenNewEntryAsync());
	}

	// In-sheet navigation: OpenAsync completes once the sheet is presented; the root view pushes further views.
	public async Task<NewEntryView> OpenNewEntryAsync()
	{
		var entry = new NewEntryView();
		var sheet = await sheets.OpenAsync(entry, sheet =>
		{
			// Only the close button or Save close it: no swipe down, tap outside or system back at the root.
			sheet.IsDismissible = false;
			sheet.ShowNativeCloseButton = true;
			sheet.Detents = [SheetDetent.Large];
		});
		sheet.Dismissed += (_, _) => Status = entry.Saved is { } saved ? $"Saved {saved}." : "New entry closed without saving.";
		return entry;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public IReadOnlyList<string> Items { get; } = Enumerable.Range(1, 60).Select(i => $"Row {i}").ToArray();

	public ICommand OpenFormCommand { get; }
	public ICommand OpenListCommand { get; }
	public ICommand OpenPassThroughCommand { get; }
	public ICommand SaveCommand { get; }
	public ICommand ExpandListCommand { get; }
	public ICommand ShowServiceSheetCommand { get; }
	public ICommand OpenNewEntryCommand { get; }

	public string Name
	{
		get => name;
		set
		{
			if (Set(ref name, value))
				OnPropertyChanged(nameof(CanSave));
		}
	}

	// Drives IsPrimaryActionEnabled: "Save" is only enabled once a name was typed.
	public bool CanSave => !string.IsNullOrWhiteSpace(Name);

	public string Status
	{
		get => status;
		set => Set(ref status, value);
	}

	public bool IsFormOpen
	{
		get => isFormOpen;
		set => Set(ref isFormOpen, value);
	}

	public bool IsListOpen
	{
		get => isListOpen;
		set => Set(ref isListOpen, value);
	}

	public bool IsPassThroughOpen
	{
		get => isPassThroughOpen;
		set => Set(ref isPassThroughOpen, value);
	}

	public SheetDetent? ListDetent
	{
		get => listDetent;
		set => Set(ref listDetent, value);
	}

	bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

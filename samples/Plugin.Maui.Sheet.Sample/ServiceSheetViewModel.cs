using System.Windows.Input;

namespace Plugin.Maui.Sheet.Sample;

public sealed class ServiceSheetViewModel(INativeSheetService sheets)
{
	public DateTime OpenedAt { get; } = DateTime.Now;

	// The result completes the caller's ShowSheetAsync<ServiceSheetView, string>().
	public ICommand PickCommand { get; } = new Command<string>(async fruit => await sheets.CloseSheetAsync(fruit));
}

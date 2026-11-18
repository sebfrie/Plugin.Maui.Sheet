using System.Windows.Input;

namespace Plugin.Maui.Sheet.Sample;

public sealed class ServiceSheetViewModel(INativeSheetService sheets)
{
	public DateTime OpenedAt { get; } = DateTime.Now;

	public ICommand CloseCommand { get; } = new Command(async () => await sheets.CloseSheetAsync());
}

namespace Plugin.Maui.Sheet.Sample;

public partial class ServiceSheetView : ContentView
{
	public ServiceSheetView(ServiceSheetViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}

#if ANDROID || IOS || MACCATALYST
using Microsoft.Maui.Handlers;
#if ANDROID
using PlatformView = Android.Views.View;
#else
using PlatformView = UIKit.UIView;
#endif

namespace Plugin.Maui.Sheet;

/// <summary>
/// Presents a <see cref="NativeSheet"/> with the platform's own sheet container and header.
/// Its <see cref="ViewHandler{TVirtualView, TPlatformView}.PlatformView"/> is an invisible anchor in the page;
/// the sheet itself lives in its own native presentation.
/// </summary>
public partial class NativeSheetHandler : ViewHandler<NativeSheet, PlatformView>
{
	/// <summary>Maps every <see cref="NativeSheet"/> property to the native sheet, also while it is open.</summary>
	public static readonly IPropertyMapper<NativeSheet, NativeSheetHandler> Mapper = new PropertyMapper<NativeSheet, NativeSheetHandler>
	{
		[nameof(NativeSheet.Content)] = (h, _) => h.UpdateContent(),
		[nameof(NativeSheet.Detents)] = (h, _) => h.UpdateDetents(),
		[nameof(NativeSheet.SelectedDetent)] = (h, _) => h.UpdateSelectedDetent(),
		[nameof(NativeSheet.PassThroughInteraction)] = (h, _) => h.UpdatePassThroughInteraction(),
		[nameof(NativeSheet.IsDismissible)] = (h, _) => h.UpdateIsDismissible(),
		[nameof(NativeSheet.ShowGrabber)] = (h, _) => h.UpdateShowGrabber(),
		[nameof(NativeSheet.CornerRadius)] = (h, _) => h.UpdateCornerRadius(),
		[nameof(NativeSheet.Title)] = (h, _) => h.UpdateTitle(),
		[nameof(NativeSheet.Subtitle)] = (h, _) => h.UpdateSubtitle(),
		[nameof(NativeSheet.PrimaryActionText)] = (h, _) => h.UpdateActions(),
		[nameof(NativeSheet.SecondaryActionText)] = (h, _) => h.UpdateActions(),
		[nameof(NativeSheet.ShowNativeCloseButton)] = (h, _) => h.UpdateActions(),
		[nameof(NativeSheet.IsPrimaryActionEnabled)] = (h, _) => h.UpdateIsPrimaryActionEnabled(),
		[nameof(NativeSheet.IsOpen)] = MapIsOpen,
	};

	/// <summary>Commands understood by the handler.</summary>
	public static readonly CommandMapper<NativeSheet, NativeSheetHandler> CommandMapper = new(ViewCommandMapper)
	{
		[nameof(NativeSheet.InvalidateDetents)] = MapInvalidateDetents,
	};

	bool invalidateDetentsPending;

	/// <summary>Creates the handler with the default mappers.</summary>
	public NativeSheetHandler() : base(Mapper, CommandMapper)
	{
	}

	/// <summary>Creates the handler with custom mappers.</summary>
	public NativeSheetHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper)
	{
	}

	/// <summary>Opens or closes the native sheet.</summary>
	public static void MapIsOpen(NativeSheetHandler handler, NativeSheet sheet)
	{
		if (sheet.IsOpen)
			handler.Present();
		else
			handler.Close();
	}

	/// <summary>Re-measures <see cref="SheetDetent.FitContent"/>; calls in the same frame are coalesced.</summary>
	public static void MapInvalidateDetents(NativeSheetHandler handler, NativeSheet sheet, object? args)
	{
		if (handler.invalidateDetentsPending)
			return;
		handler.invalidateDetentsPending = true;
		sheet.Dispatcher.Dispatch(() =>
		{
			handler.invalidateDetentsPending = false;
			handler.InvalidateDetentsCore();
		});
	}

	// Native views may only be touched on the UI thread; bindings may update from anywhere.
	/// <inheritdoc />
	public override void UpdateValue(string property)
	{
		if (VirtualView?.Dispatcher is { IsDispatchRequired: true } dispatcher)
			dispatcher.Dispatch(() => base.UpdateValue(property));
		else
			base.UpdateValue(property);
	}

	/// <inheritdoc />
	public override void Invoke(string command, object? args = null)
	{
		if (VirtualView?.Dispatcher is { IsDispatchRequired: true } dispatcher)
			dispatcher.Dispatch(() => base.Invoke(command, args));
		else
			base.Invoke(command, args);
	}

	/// <inheritdoc />
	protected override void DisconnectHandler(PlatformView platformView)
	{
		VirtualView.Loaded -= OnVirtualViewLoaded;
		// Content is a logical child, so MAUI's DisconnectHandlers() reaches it after this.
		Close();
		base.DisconnectHandler(platformView);
	}

	// IsOpen was set before the page reached a window (e.g. IsOpen="True" in XAML): present once it is loaded.
	void PresentWhenLoaded()
	{
		VirtualView.Loaded -= OnVirtualViewLoaded;
		VirtualView.Loaded += OnVirtualViewLoaded;
	}

	void OnVirtualViewLoaded(object? sender, EventArgs e)
	{
		VirtualView.Loaded -= OnVirtualViewLoaded;
		if (VirtualView.IsOpen)
			Present();
	}

	/// <summary>Distinct detents, smallest first, resolved with the given maximum and content heights.</summary>
	static List<(SheetDetent Detent, double Height)> Resolve(IEnumerable<SheetDetent> detents, double maxHeight, double contentHeight) =>
		detents.Distinct()
			.Select(d => (d, d.Resolve(maxHeight, contentHeight)))
			.OrderBy(d => d.Item2)
			.ToList();

	/// <summary>The largest detent that keeps the page below interactive, or <see langword="null"/>.</summary>
	static SheetDetent? LargestUndimmed(NativeSheet sheet, List<(SheetDetent Detent, double Height)> sorted) =>
		sheet.PassThroughInteraction ? sorted.LastOrDefault(d => d.Detent.Kind != SheetDetentKind.Large).Detent : null;
}
#endif

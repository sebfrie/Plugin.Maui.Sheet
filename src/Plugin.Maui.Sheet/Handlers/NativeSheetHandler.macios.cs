using System.Runtime.InteropServices;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace Plugin.Maui.Sheet;

public partial class NativeSheetHandler
{
	// .NET binds these properties to an enum that only knows Medium/Large; KVC also accepts custom identifiers.
	static readonly NSString SelectedDetentKey = new("selectedDetentIdentifier");
	static readonly NSString LargestUndimmedDetentKey = new("largestUndimmedDetentIdentifier");

	UINavigationController? navigation;
	SheetContentViewController? contentController;
	SheetDelegate? sheetDelegate;
	UIView? platformContent;
	List<(SheetDetent Detent, double Height)> sorted = [];
	NFloat defaultCornerRadius;
	double presenterWidth;
	double presenterHeight;
	double fitContentWidth;
	bool isDismissing;

	/// <inheritdoc />
	protected override UIView CreatePlatformView() => new() { Hidden = true, UserInteractionEnabled = false };

	UISheetPresentationController? Sheet => navigation?.SheetPresentationController;

	void Present()
	{
		if (navigation is not null)
			return;
		if (VirtualView.Window?.Handler?.PlatformView is not UIWindow { RootViewController: { } presenter })
		{
			PresentWhenLoaded();
			return;
		}
		while (presenter.PresentedViewController is { IsBeingDismissed: false } presented)
			presenter = presented;

		presenterWidth = presenter.View?.Bounds.Width ?? 0;
		presenterHeight = presenter.View?.Bounds.Height ?? 0;
		contentController = new SheetContentViewController(this);
		navigation = new UINavigationController(contentController) { ModalPresentationStyle = UIModalPresentationStyle.PageSheet };
		sheetDelegate = new SheetDelegate(this);
		var sheet = Sheet!;
		sheet.Delegate = sheetDelegate;
		sheet.PrefersScrollingExpandsWhenScrolledToEdge = true;
		defaultCornerRadius = sheet.PreferredCornerRadius;

		VirtualView.CreateTemplatedContent();
		UpdateContent();
		UpdateTitle();
		UpdateSubtitle();
		UpdateActions();
		UpdateIsDismissible();
		UpdateShowGrabber();
		UpdateBackground();
		UpdateDetents();

		presenter.PresentViewController(navigation, true, () => VirtualView?.SendOpened(CurrentDetent()));
	}

	void Close()
	{
		if (navigation is null || isDismissing)
			return;
		isDismissing = true;
		navigation.DismissViewController(true, OnDismissed);
	}

	void OnDismissed()
	{
		TearDown();
		VirtualView?.SendDismissed();
	}

	void TearDown()
	{
		if (Sheet is { } sheet)
			sheet.Delegate = null;
		platformContent?.RemoveFromSuperview();
		VirtualView?.ReleaseTemplatedContent();
		navigation = null;
		contentController = null;
		sheetDelegate = null;
		platformContent = null;
		sorted = [];
		fitContentWidth = 0;
		isDismissing = false;
	}

	void Animate(Action changes)
	{
		if (Sheet is not { } sheet)
			return;
		if (navigation?.PresentingViewController is null)
			changes();
		else
			sheet.AnimateChanges(changes);
	}

	void UpdateContent()
	{
		if (contentController?.View is not { } container)
			return;
		platformContent?.RemoveFromSuperview();
		platformContent = VirtualView.PresentedContent?.ToPlatform(MauiContext!);
		if (platformContent is not null)
		{
			platformContent.RemoveFromSuperview();
			container.AddSubview(platformContent);
		}
		// Lets a ScrollView/CollectionView drag the sheet up when scrolled to its edge.
		contentController.SetContentScrollView(platformContent is null ? null : FindScrollView(platformContent), NSDirectionalRectEdge.All);
		container.SetNeedsLayout();
		InvalidateDetentsCore();
	}

	static UIScrollView? FindScrollView(UIView view) =>
		view as UIScrollView ?? view.Subviews.Select(FindScrollView).FirstOrDefault(s => s is not null);

	// Called from ViewDidLayoutSubviews: the MAUI content fills the safe area below the navigation bar.
	internal void LayoutContent(UIView container)
	{
		if (VirtualView?.PresentedContent is not IView content)
			return;
		var insets = container.SafeAreaInsets;
		var bounds = container.Bounds;
		var frame = new Rect(insets.Left, insets.Top,
			Math.Max(0, bounds.Width - insets.Left - insets.Right),
			Math.Max(0, bounds.Height - insets.Top - insets.Bottom));
		content.Measure(frame.Width, frame.Height);
		content.Arrange(frame);

		// FitContent was measured with a guessed width before the sheet had its real size (e.g. iPad form sheets).
		if (frame.Width > 0 && frame.Width != fitContentWidth && VirtualView.Detents.Contains(SheetDetent.FitContent))
		{
			fitContentWidth = frame.Width;
			InvalidateDetentsCore();
		}
	}

	double MeasureFitContent()
	{
		if (navigation is null || contentController?.View is not { } container)
			return 0;
		var insets = container.SafeAreaInsets;
		var width = (container.Bounds.Width > 0 ? container.Bounds.Width : presenterWidth) - insets.Left - insets.Right;
		var header = navigation.NavigationBarHidden ? 0 : navigation.NavigationBar.Frame.Height;
		var content = VirtualView?.PresentedContent is IView view && width > 0 ? view.Measure(width, double.PositiveInfinity).Height : 0;
		return header + content;
	}

	void InvalidateDetentsCore()
	{
		if (!VirtualView.Detents.Contains(SheetDetent.FitContent) || Sheet is not { } sheet)
			return;
		if (OperatingSystem.IsIOSVersionAtLeast(16) || OperatingSystem.IsMacCatalystVersionAtLeast(16))
			Animate(sheet.InvalidateDetents);
	}

	void UpdateDetents()
	{
		if (Sheet is not { } sheet)
			return;
		// Only used to order the detents; UIKit resolves the real heights.
		var maxHeight = presenterHeight;
		sorted = Resolve(VirtualView.Detents, maxHeight, MeasureFitContent());
		var detents = sorted.Select(d => CreateDetent(d.Detent, maxHeight)).ToArray();
		Animate(() =>
		{
			sheet.Detents = detents;
			SetIdentifier(sheet, SelectedDetentKey, IdentifierOf(VirtualView.SelectedDetent));
			SetIdentifier(sheet, LargestUndimmedDetentKey, IdentifierOf(LargestUndimmed(VirtualView, sorted)));
		});
	}

	UISheetPresentationControllerDetent CreateDetent(SheetDetent detent, double maxHeight)
	{
		if (detent.Kind == SheetDetentKind.Medium)
			return UISheetPresentationControllerDetent.CreateMediumDetent();
		if (detent.Kind == SheetDetentKind.Large)
			return UISheetPresentationControllerDetent.CreateLargeDetent();
		if (!OperatingSystem.IsIOSVersionAtLeast(16) && !OperatingSystem.IsMacCatalystVersionAtLeast(16))
		{
			// iOS 15 has no custom detents: use the closer system detent.
			return detent.Resolve(maxHeight, MeasureFitContent()) <= maxHeight * 0.75
				? UISheetPresentationControllerDetent.CreateMediumDetent()
				: UISheetPresentationControllerDetent.CreateLargeDetent();
		}

		return CreateCustomDetent(detent);
	}

	[System.Runtime.Versioning.SupportedOSPlatform("ios16.0")]
	[System.Runtime.Versioning.SupportedOSPlatform("maccatalyst16.0")]
	UISheetPresentationControllerDetent CreateCustomDetent(SheetDetent detent)
	{
		var weakHandler = new WeakReference<NativeSheetHandler>(this);
		return UISheetPresentationControllerDetent.Create(IdentifierOf(detent), context =>
		{
			var content = detent.Kind == SheetDetentKind.FitContent && weakHandler.TryGetTarget(out var handler) ? handler.MeasureFitContent() : 0;
			return (NFloat)detent.Resolve(context.MaximumDetentValue, content);
		});
	}

	static string? IdentifierOf(SheetDetent? detent) => detent?.Kind switch
	{
		null => null,
		SheetDetentKind.Medium => UISheetPresentationControllerDetentIdentifier.Medium.GetConstant()?.ToString(),
		SheetDetentKind.Large => UISheetPresentationControllerDetentIdentifier.Large.GetConstant()?.ToString(),
		_ when !OperatingSystem.IsIOSVersionAtLeast(16) && !OperatingSystem.IsMacCatalystVersionAtLeast(16) => null,
		_ => $"Plugin.Maui.Sheet.{detent}",
	};

	static void SetIdentifier(UISheetPresentationController sheet, NSString key, string? identifier)
	{
		if (sheet.ValueForKey(key)?.ToString() != identifier)
			sheet.SetValueForKey(identifier is null ? null! : new NSString(identifier), key);
	}

	SheetDetent? CurrentDetent()
	{
		if (Sheet is not { } sheet || sorted.Count == 0)
			return null;
		var identifier = sheet.ValueForKey(SelectedDetentKey)?.ToString();
		// nil means the sheet rests at its smallest detent.
		return identifier is null ? sorted[0].Detent : sorted.Find(d => IdentifierOf(d.Detent) == identifier).Detent;
	}

	void UpdateSelectedDetent()
	{
		if (Sheet is { } sheet && CurrentDetent() != VirtualView.SelectedDetent)
			Animate(() => SetIdentifier(sheet, SelectedDetentKey, IdentifierOf(VirtualView.SelectedDetent)));
	}

	void UpdatePassThroughInteraction()
	{
		if (Sheet is { } sheet)
			Animate(() => SetIdentifier(sheet, LargestUndimmedDetentKey, IdentifierOf(LargestUndimmed(VirtualView, sorted))));
	}

	void UpdateIsDismissible()
	{
		if (navigation is not null)
			navigation.ModalInPresentation = !VirtualView.IsDismissible;
	}

	void UpdateShowGrabber()
	{
		if (Sheet is { } sheet)
			Animate(() => sheet.PrefersGrabberVisible = VirtualView.ShowGrabber);
	}

	void UpdateBackground()
	{
		if (contentController?.View is { } view)
			view.BackgroundColor = VirtualView.BackgroundColor?.ToPlatform() ?? UIColor.SystemBackground;
		if (Sheet is { } sheet)
			Animate(() => sheet.PreferredCornerRadius = VirtualView.CornerRadius >= 0 ? (NFloat)VirtualView.CornerRadius : defaultCornerRadius);
	}

	void UpdateTitle()
	{
		if (contentController is null)
			return;
		contentController.NavigationItem.Title = VirtualView.Title;
		UpdateHeaderVisibility();
	}

	void UpdateSubtitle()
	{
		if (contentController is null)
			return;
		if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
			contentController.NavigationItem.Subtitle = VirtualView.Subtitle;
		UpdateHeaderVisibility();
	}

	void UpdateActions()
	{
		if (contentController is null)
			return;
		var item = contentController.NavigationItem;

		item.RightBarButtonItem = string.IsNullOrEmpty(VirtualView.PrimaryActionText) ? null
			: new UIBarButtonItem(VirtualView.PrimaryActionText, UIBarButtonItemStyle.Done, (_, _) => VirtualView?.SendPrimaryAction());

		var leading = new List<UIBarButtonItem>();
		if (VirtualView.ShowNativeCloseButton)
			leading.Add(new UIBarButtonItem(UIBarButtonSystemItem.Close, (_, _) =>
			{
				if (VirtualView is not null)
					VirtualView.IsOpen = false;
			}));
		if (!string.IsNullOrEmpty(VirtualView.SecondaryActionText))
			leading.Add(new UIBarButtonItem(VirtualView.SecondaryActionText, UIBarButtonItemStyle.Plain, (_, _) => VirtualView?.SendSecondaryAction()));
		item.LeftBarButtonItems = [.. leading];

		UpdateIsPrimaryActionEnabled();
		UpdateHeaderVisibility();
	}

	void UpdateIsPrimaryActionEnabled()
	{
		if (contentController?.NavigationItem.RightBarButtonItem is { } primary)
			primary.Enabled = VirtualView.IsPrimaryActionEnabled;
	}

	void UpdateHeaderVisibility()
	{
		if (navigation is null || navigation.NavigationBarHidden == !VirtualView.HasHeader)
			return;
		navigation.SetNavigationBarHidden(!VirtualView.HasHeader, navigation.PresentingViewController is not null);
		InvalidateDetentsCore();
	}

	void OnSelectedDetentChanged()
	{
		if (CurrentDetent() is { } detent)
			VirtualView?.SendDetentChanged(detent);
	}

	// Only asked when IsDismissible is true (ModalInPresentation is false).
	bool ShouldDismiss() => VirtualView?.SendDismissAttempted() != true;

	// Called when ModalInPresentation blocked a swipe, or ShouldDismiss returned false.
	void OnDismissAttempted()
	{
		if (VirtualView is { IsDismissible: false } sheet && !sheet.SendDismissAttempted())
			sheet.IsOpen = false;
	}

	sealed class SheetContentViewController(NativeSheetHandler handler) : UIViewController
	{
		readonly WeakReference<NativeSheetHandler> owner = new(handler);

		public override void ViewDidLayoutSubviews()
		{
			base.ViewDidLayoutSubviews();
			if (owner.TryGetTarget(out var target))
				target.LayoutContent(View!);
		}
	}

	sealed class SheetDelegate(NativeSheetHandler handler) : UISheetPresentationControllerDelegate
	{
		readonly WeakReference<NativeSheetHandler> owner = new(handler);

		public override void DidChangeSelectedDetentIdentifier(UISheetPresentationController sheetPresentationController)
		{
			if (owner.TryGetTarget(out var target))
				target.OnSelectedDetentChanged();
		}

		public override bool ShouldDismiss(UIPresentationController presentationController) =>
			!owner.TryGetTarget(out var target) || target.ShouldDismiss();

		public override void DidAttemptToDismiss(UIPresentationController presentationController)
		{
			if (owner.TryGetTarget(out var target))
				target.OnDismissAttempted();
		}

		public override void DidDismiss(UIPresentationController presentationController)
		{
			if (owner.TryGetTarget(out var target))
				target.OnDismissed();
		}
	}
}

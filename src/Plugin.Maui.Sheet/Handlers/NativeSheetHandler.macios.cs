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
	static readonly ToolbarItem[] NoItems = [];

	SheetNavigationController? navigation;
	SheetDelegate? sheetDelegate;
	NavigationDelegate? navigationDelegate;
	// One view controller per view of the navigation stack, root first.
	readonly List<LevelViewController> levels = [];
	TaskCompletionSource? pendingNavigation;
	List<(SheetDetent Detent, double Height)> sorted = [];
	NFloat defaultCornerRadius;
	double presenterWidth;
	double presenterHeight;
	double fitContentWidth;
	bool isDismissing;

	/// <inheritdoc />
	protected override UIView CreatePlatformView() => new() { Hidden = true, UserInteractionEnabled = false };

	UISheetPresentationController? Sheet => navigation?.SheetPresentationController;

	LevelViewController? TopLevel => levels.Count > 0 ? levels[^1] : null;

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
		ModalPresentationRedirect.Install();

		presenterWidth = presenter.View?.Bounds.Width ?? 0;
		presenterHeight = presenter.View?.Bounds.Height ?? 0;
		VirtualView.CreateTemplatedContent();
		foreach (var view in VirtualView.NavigationStack)
			levels.Add(new LevelViewController(this, view));
		if (levels.Count == 0)
			levels.Add(new LevelViewController(this, null));
		navigation = new SheetNavigationController(levels[0]) { ModalPresentationStyle = UIModalPresentationStyle.PageSheet };
		if (levels.Count > 1)
			navigation.ViewControllers = [.. levels];
		navigationDelegate = new NavigationDelegate(this);
		navigation.Delegate = navigationDelegate;
		sheetDelegate = new SheetDelegate(this);
		var sheet = Sheet!;
		sheet.Delegate = sheetDelegate;
		sheet.PrefersScrollingExpandsWhenScrolledToEdge = true;
		defaultCornerRadius = sheet.PreferredCornerRadius;

		UpdateHeader();
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
		if (navigation is not null)
			navigation.Delegate = null!;
		foreach (var level in levels)
			level.Host(null);
		levels.Clear();
		pendingNavigation?.TrySetResult();
		VirtualView?.ReleaseTemplatedContent();
		navigation = null;
		sheetDelegate = null;
		navigationDelegate = null;
		pendingNavigation = null;
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

	// The root view changed.
	void UpdateContent()
	{
		if (levels.Count == 0)
			return;
		levels[0].Host(VirtualView.PresentedContent);
		UpdateBackground();
		InvalidateDetentsCore();
	}

	void Navigate(SheetNavigationRequest request)
	{
		if (navigation is null)
		{
			request.Done.TrySetResult();
			return;
		}
		pendingNavigation?.TrySetResult();
		pendingNavigation = request.Done;
		if (request.Pushed is { } view)
		{
			var level = new LevelViewController(this, view);
			levels.Add(level);
			UpdateHeader();
			UpdateBackground();
			navigation.PushViewController(level, request.Animated);
		}
		else
		{
			var popped = levels.GetRange(levels.Count - request.Popped.Count, request.Popped.Count);
			levels.RemoveRange(levels.Count - popped.Count, popped.Count);
			UpdateHeader();
			navigation.PopToViewController(levels[^1], request.Animated);
		}
		if (!request.Animated)
			CompletePendingNavigation();
	}

	// After every push and pop, animated or not, and after the user went back (back button or edge swipe).
	void OnDidShowViewController()
	{
		var shown = navigation?.ViewControllers?.Length ?? 0;
		while (levels.Count > shown && levels.Count > 1)
		{
			levels[^1].Host(null);
			levels.RemoveAt(levels.Count - 1);
			VirtualView?.SendUserPopped();
		}
		CompletePendingNavigation();
		UpdateHeader();
		InvalidateDetentsCore();
	}

	void CompletePendingNavigation()
	{
		pendingNavigation?.TrySetResult();
		pendingNavigation = null;
	}

	static UIScrollView? FindScrollView(UIView view) =>
		view as UIScrollView ?? view.Subviews.Select(FindScrollView).FirstOrDefault(s => s is not null);

	// Called from ViewDidLayoutSubviews: the level's MAUI view fills the safe area below the navigation bar.
	void LayoutLevel(LevelViewController level)
	{
		if (level.View is not { } container || level.Content is not IView content)
			return;
		var insets = container.SafeAreaInsets;
		var bounds = container.Bounds;
		var frame = new Rect(insets.Left, insets.Top,
			Math.Max(0, bounds.Width - insets.Left - insets.Right),
			Math.Max(0, bounds.Height - insets.Top - insets.Bottom));
		content.Measure(frame.Width, frame.Height);
		content.Arrange(frame);

		// FitContent was measured with a guessed width before the sheet had its real size (e.g. iPad form sheets).
		if (level == TopLevel && frame.Width > 0 && frame.Width != fitContentWidth && VirtualView?.Detents.Contains(SheetDetent.FitContent) == true)
		{
			fitContentWidth = frame.Width;
			InvalidateDetentsCore();
		}
	}

	// FitContent sizes the sheet to the view on top of the navigation stack.
	double MeasureFitContent()
	{
		if (navigation is null || TopLevel?.View is not { } container)
			return 0;
		var insets = container.SafeAreaInsets;
		var width = (container.Bounds.Width > 0 ? container.Bounds.Width : presenterWidth) - insets.Left - insets.Right;
		var header = navigation.NavigationBarHidden ? 0 : navigation.NavigationBar.Frame.Height;
		var content = TopLevel.Content is IView view && width > 0 ? view.Measure(width, double.PositiveInfinity).Height : 0;
		return header + content;
	}

	void InvalidateDetentsCore()
	{
		if (VirtualView?.Detents.Contains(SheetDetent.FitContent) != true || Sheet is not { } sheet)
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

	// Blocks swipe down and tap outside; UIKit then asks DidAttemptToDismiss.
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
		var color = VirtualView.BackgroundColor?.ToPlatform() ?? UIColor.SystemBackground;
		foreach (var level in levels)
			level.View!.BackgroundColor = color;
		if (Sheet is { } sheet)
			Animate(() => sheet.PreferredCornerRadius = VirtualView.CornerRadius >= 0 ? (NFloat)VirtualView.CornerRadius : defaultCornerRadius);
	}

	// Every level's navigationItem: the title, close/back and actions UIKit shows while that level is on top.
	void UpdateHeader()
	{
		if (navigation is null || VirtualView is not { } sheet)
			return;
		for (var i = 0; i < levels.Count; i++)
		{
			var header = sheet.HeaderAt(i);
			// Rebuilt bar buttons, e.g. the root's while a push slides it away, flash in the tint colour: keep unchanged ones.
			var state = HeaderState(header);
			if (levels[i].AppliedHeader?.SequenceEqual(state) == true)
				continue;
			levels[i].AppliedHeader = state;
			ApplyHeader(levels[i].NavigationItem, header);
		}
		var hidden = sheet.HeaderAt(levels.Count - 1).IsEmpty;
		if (navigation.NavigationBarHidden != hidden)
		{
			navigation.SetNavigationBarHidden(hidden, navigation.PresentingViewController is not null);
			InvalidateDetentsCore();
		}
	}

	// Everything ApplyHeader reads, as values: toolbar items change in place.
	object[] HeaderState(SheetHeader header) =>
	[
		header with { Items = NoItems, OverflowItems = NoItems },
		VirtualView.ShowOverflowIcons,
		.. header.Items.Concat(header.OverflowItems).Select(i => (object)(i, i.Order, i.Text, i.IsEnabled, i.IconImageSource, i.AutomationId)),
	];

	void ApplyHeader(UINavigationItem item, SheetHeader header)
	{
		item.Title = header.Title;
		if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
			item.Subtitle = header.Subtitle;

		// Pushed levels keep UIKit's own back button (and its edge swipe); the root gets close and the secondary action.
		var leading = new List<UIBarButtonItem>();
		if (header.ShowClose)
			leading.Add(new UIBarButtonItem(UIBarButtonSystemItem.Close, (_, _) =>
			{
				if (VirtualView is not null)
					VirtualView.IsOpen = false;
			}));
		if (!string.IsNullOrEmpty(header.SecondaryActionText))
			leading.Add(new UIBarButtonItem(header.SecondaryActionText, UIBarButtonItemStyle.Plain, (_, _) => VirtualView?.SendSecondaryAction()));
		item.LeftBarButtonItems = leading.Count > 0 ? [.. leading] : null!;

		// RightBarButtonItems run from the trailing edge inwards.
		var trailing = new List<UIBarButtonItem>();
		if (!string.IsNullOrEmpty(header.PrimaryActionText))
			trailing.Add(new UIBarButtonItem(header.PrimaryActionText, UIBarButtonItemStyle.Done, (_, _) => VirtualView?.SendPrimaryAction())
			{
				Enabled = header.IsPrimaryActionEnabled,
			});
		if (header.OverflowItems.Count > 0)
			trailing.Add(OverflowButton(header.OverflowItems));
		trailing.AddRange(header.Items.Select(BarButton));
		item.RightBarButtonItems = [.. trailing];
	}

	UIBarButtonItem BarButton(ToolbarItem toolbarItem)
	{
		var button = new UIBarButtonItem(toolbarItem.Text ?? string.Empty, UIBarButtonItemStyle.Plain, (_, _) => ((IMenuItemController)toolbarItem).Activate())
		{
			Enabled = toolbarItem.IsEnabled,
			AccessibilityIdentifier = toolbarItem.AutomationId,
			AccessibilityLabel = toolbarItem.Text,
		};
		toolbarItem.IconImageSource?.LoadImage(MauiContext!, result =>
		{
			if (result?.Value is { } image)
				button.Image = image;
		});
		return button;
	}

	UIBarButtonItem OverflowButton(IReadOnlyList<ToolbarItem> toolbarItems)
	{
		var images = new UIImage?[toolbarItems.Count];
		var button = new UIBarButtonItem(UIImage.GetSystemImage("ellipsis"), Menu())
		{
			AccessibilityIdentifier = "NativeSheetOverflow",
		};
		// A UIMenu is immutable, so it is rebuilt as each icon arrives. Bar buttons draw Automatic images as templates
		// but menus draw them as they are (MAUI's glyphs are white): make them templates, as on the bar.
		for (var i = 0; VirtualView.ShowOverflowIcons && i < toolbarItems.Count; i++)
		{
			var index = i;
			toolbarItems[i].IconImageSource?.LoadImage(MauiContext!, result =>
			{
				var image = result?.Value;
				images[index] = image?.RenderingMode == UIImageRenderingMode.Automatic ? image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate) : image;
				button.Menu = Menu();
			});
		}
		return button;

		UIMenu Menu() => UIMenu.Create([.. toolbarItems.Select((toolbarItem, i) =>
		{
			var action = UIAction.Create(toolbarItem.Text ?? string.Empty, images[i], toolbarItem.AutomationId, _ => ((IMenuItemController)toolbarItem).Activate());
			if (!toolbarItem.IsEnabled)
				action.Attributes = UIMenuElementAttributes.Disabled;
			return (UIMenuElement)action;
		})]);
	}

	void OnSelectedDetentChanged()
	{
		if (CurrentDetent() is { } detent)
			VirtualView?.SendDetentChanged(detent);
	}

	// Only asked when IsDismissible is true (ModalInPresentation is false).
	bool ShouldDismiss() => VirtualView?.SendDismissAttempted() != true;

	// Called when ModalInPresentation blocked a swipe or tap outside, or ShouldDismiss returned false. With
	// IsDismissible false only the close button or code closes the sheet; this just reports the attempt.
	void OnDismissAttempted()
	{
		if (VirtualView is { IsDismissible: false } sheet)
			sheet.SendDismissAttempted();
	}

	// Hosts one view of the navigation stack below the navigation bar.
	sealed class LevelViewController : UIViewController
	{
		readonly WeakReference<NativeSheetHandler> owner;
		UIView? platformView;

		public LevelViewController(NativeSheetHandler handler, View? content)
		{
			owner = new(handler);
			Host(content);
		}

		public View? Content { get; private set; }

		// HeaderState of the header on this level's navigationItem.
		public object[]? AppliedHeader { get; set; }

		// Shows a view (null: none); its platform view may only have one superview.
		public void Host(View? content)
		{
			platformView?.RemoveFromSuperview();
			Content = content;
			if (!owner.TryGetTarget(out var handler) || content is null)
			{
				platformView = null;
				return;
			}
			platformView = content.ToPlatform(handler.MauiContext!);
			platformView.RemoveFromSuperview();
			View!.AddSubview(platformView);
			// Lets a ScrollView/CollectionView drag the sheet up when scrolled to its edge.
			SetContentScrollView(FindScrollView(platformView), NSDirectionalRectEdge.All);
			View.SetNeedsLayout();
		}

		public override void ViewDidLayoutSubviews()
		{
			base.ViewDidLayoutSubviews();
			if (owner.TryGetTarget(out var handler))
				handler.LayoutLevel(this);
		}
	}

	// Marks the sheet's controller for ModalPresentationRedirect.
	internal sealed class SheetNavigationController(UIViewController root) : UINavigationController(root);

	sealed class NavigationDelegate(NativeSheetHandler handler) : UINavigationControllerDelegate
	{
		readonly WeakReference<NativeSheetHandler> owner = new(handler);

		public override void DidShowViewController(UINavigationController navigationController, UIViewController viewController, bool animated)
		{
			if (owner.TryGetTarget(out var target))
				target.OnDidShowViewController();
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

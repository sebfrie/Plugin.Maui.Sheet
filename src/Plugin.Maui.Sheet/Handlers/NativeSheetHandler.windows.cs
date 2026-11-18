using System.Numerics;
using Microsoft.Maui.Platform;
using UI = Microsoft.UI.Xaml;
using UIC = Microsoft.UI.Xaml.Controls;

namespace Plugin.Maui.Sheet;

// Windows has no bottom sheet. Like a sheet on Mac Catalyst, it is shown as a card centred over the window, in a
// Popup with a WinUI header over a dimmed backdrop. Clicking the backdrop or pressing Esc dismisses it. That light
// dismiss is the handler's own: Popup.IsLightDismissEnabled also closes on window resize and deactivation (Alt+Tab)
// and cannot be cancelled. Detents only choose the card's height; there is no dragging or grabber.
public partial class NativeSheetHandler
{
	const double SheetMaxWidth = 540;
	const double WindowMargin = 24;

	UIC.Primitives.Popup? popup;
	UIC.Grid? host;
	UIC.Border? backdrop;
	UIC.Border? card;
	UIC.Grid? layout;
	UIC.Grid? header;
	UIC.TextBlock? titleText;
	UIC.TextBlock? subtitleText;
	UIC.Button? closeButton;
	UIC.Button? secondaryButton;
	UIC.Button? primaryButton;
	UI.FrameworkElement? platformContent;
	UI.XamlRoot? xamlRoot;
	List<(SheetDetent Detent, double Height)> sorted = [];

	/// <inheritdoc />
	protected override UI.FrameworkElement CreatePlatformView() => new UIC.Border { IsHitTestVisible = false };

	void Present()
	{
		if (popup is not null)
			return;
		if (VirtualView.Window?.Handler?.PlatformView is not UI.Window { Content.XamlRoot: { } root })
		{
			PresentWhenLoaded();
			return;
		}

		xamlRoot = root;
		titleText = new UIC.TextBlock
		{
			Style = ThemeResource<UI.Style>("BodyStrongTextBlockStyle"),
			TextTrimming = UI.TextTrimming.CharacterEllipsis,
			HorizontalAlignment = UI.HorizontalAlignment.Center,
		};
		subtitleText = new UIC.TextBlock
		{
			Style = ThemeResource<UI.Style>("CaptionTextBlockStyle"),
			TextTrimming = UI.TextTrimming.CharacterEllipsis,
			HorizontalAlignment = UI.HorizontalAlignment.Center,
		};
		closeButton = new UIC.Button { Content = new UIC.FontIcon { Glyph = "", FontSize = 12 } };
		UI.Automation.AutomationProperties.SetName(closeButton, "Close");
		secondaryButton = new UIC.Button();
		primaryButton = new UIC.Button { Style = ThemeResource<UI.Style>("AccentButtonStyle") };
		closeButton.Click += OnCloseClick;
		secondaryButton.Click += OnSecondaryClick;
		primaryButton.Click += OnPrimaryClick;

		var leading = new UIC.StackPanel { Orientation = UIC.Orientation.Horizontal, Spacing = 8, Children = { closeButton, secondaryButton } };
		var title = new UIC.StackPanel { VerticalAlignment = UI.VerticalAlignment.Center, Children = { titleText, subtitleText } };
		primaryButton.HorizontalAlignment = UI.HorizontalAlignment.Right;
		UIC.Grid.SetColumn(title, 1);
		UIC.Grid.SetColumn(primaryButton, 2);
		// Equal outer columns keep the title centred, as in a navigation bar.
		header = new UIC.Grid
		{
			Padding = new UI.Thickness(12),
			ColumnSpacing = 12,
			ColumnDefinitions =
			{
				new UIC.ColumnDefinition { Width = new UI.GridLength(1, UI.GridUnitType.Star) },
				new UIC.ColumnDefinition { Width = UI.GridLength.Auto },
				new UIC.ColumnDefinition { Width = new UI.GridLength(1, UI.GridUnitType.Star) },
			},
			Children = { leading, title, primaryButton },
		};

		layout = new UIC.Grid
		{
			RowDefinitions =
			{
				new UIC.RowDefinition { Height = UI.GridLength.Auto },
				new UIC.RowDefinition { Height = new UI.GridLength(1, UI.GridUnitType.Star) },
			},
			Children = { header },
		};
		card = new UIC.Border
		{
			Child = layout,
			BorderThickness = new UI.Thickness(1),
			BorderBrush = ThemeResource<UI.Media.Brush>("SurfaceStrokeColorDefaultBrush"),
			Shadow = new UI.Media.ThemeShadow(),
			Translation = new Vector3(0, 0, 32),
			HorizontalAlignment = UI.HorizontalAlignment.Center,
			VerticalAlignment = UI.VerticalAlignment.Center,
		};
		backdrop = new UIC.Border { Background = ThemeResource<UI.Media.Brush>("SmokeFillColorDefaultBrush") };
		backdrop.Tapped += OnBackdropTapped;
		// No background on the host itself: with the backdrop collapsed (pass-through), clicks reach the page.
		host = new UIC.Grid { Children = { backdrop, card } };
		host.KeyDown += OnHostKeyDown;

		popup = new UIC.Primitives.Popup { XamlRoot = root, Child = host };
		popup.Opened += OnPopupOpened;
		popup.Closed += OnPopupClosed;
		root.Changed += OnXamlRootChanged;

		VirtualView.CreateTemplatedContent();
		UpdateContent();
		UpdateTitle();
		UpdateSubtitle();
		UpdateActions();
		UpdateBackground();
		UpdateDetents();
		popup.IsOpen = true;
	}

	void Close()
	{
		if (popup is not null)
			popup.IsOpen = false;
	}

	void OnPopupOpened(object? sender, object e)
	{
		// Focus moves into the sheet, so Esc reaches it and keyboard users land on its first control.
		if (card is not null && UI.Input.FocusManager.FindFirstFocusableElement(card) is UI.UIElement first)
			first.Focus(UI.FocusState.Programmatic);
		VirtualView?.SendOpened(CurrentDetent());
	}

	void OnPopupClosed(object? sender, object e)
	{
		if (!ReferenceEquals(sender, popup))
			return;
		TearDown();
		VirtualView?.SendDismissed();
	}

	void OnBackdropTapped(object sender, UI.Input.TappedRoutedEventArgs e) => LightDismiss();

	void OnHostKeyDown(object sender, UI.Input.KeyRoutedEventArgs e)
	{
		if (e.Key != Windows.System.VirtualKey.Escape)
			return;
		e.Handled = true;
		LightDismiss();
	}

	void LightDismiss()
	{
		if (VirtualView?.SendDismissAttempted() == false)
			Close();
	}

	void TearDown()
	{
		if (popup is null)
			return;
		popup.Opened -= OnPopupOpened;
		popup.Closed -= OnPopupClosed;
		if (xamlRoot is not null)
			xamlRoot.Changed -= OnXamlRootChanged;
		backdrop!.Tapped -= OnBackdropTapped;
		host!.KeyDown -= OnHostKeyDown;
		closeButton!.Click -= OnCloseClick;
		secondaryButton!.Click -= OnSecondaryClick;
		primaryButton!.Click -= OnPrimaryClick;
		// The MAUI content keeps its platform view for the next presentation; it may only have one parent.
		if (platformContent is not null)
			layout!.Children.Remove(platformContent);
		popup.Child = null;
		VirtualView?.ReleaseTemplatedContent();

		popup = null;
		host = null;
		backdrop = null;
		card = null;
		layout = null;
		header = null;
		titleText = null;
		subtitleText = null;
		closeButton = null;
		secondaryButton = null;
		primaryButton = null;
		platformContent = null;
		xamlRoot = null;
		sorted = [];
	}

	void UpdateContent()
	{
		if (layout is null)
			return;
		if (platformContent is not null)
			layout.Children.Remove(platformContent);
		platformContent = VirtualView.PresentedContent?.ToPlatform(MauiContext!);
		if (platformContent is null)
			return;
		(platformContent.Parent as UIC.Panel)?.Children.Remove(platformContent);
		UIC.Grid.SetRow(platformContent, 1);
		layout.Children.Add(platformContent);
		InvalidateDetentsCore();
	}

	void InvalidateDetentsCore() => UpdateDetents();

	void UpdateDetents()
	{
		if (host is null || card is null || xamlRoot is null)
			return;
		host.Width = xamlRoot.Size.Width;
		host.Height = xamlRoot.Size.Height;
		var width = Math.Min(SheetMaxWidth, Math.Max(0, xamlRoot.Size.Width - 2 * WindowMargin));
		var maxHeight = Math.Max(0, xamlRoot.Size.Height - 2 * WindowMargin);
		// Only orders FitContent among the other detents; the card itself sizes to its content natively.
		var fitHeight = VirtualView.Detents.Contains(SheetDetent.FitContent) && VirtualView.PresentedContent is IView content
			? (header?.ActualHeight ?? 0) + content.Measure(width, double.PositiveInfinity).Height
			: 0;
		sorted = Resolve(VirtualView.Detents, maxHeight, fitHeight);

		var current = CurrentDetent()!;
		card.Width = width;
		card.MaxHeight = maxHeight;
		card.Height = current.Kind == SheetDetentKind.FitContent ? double.NaN : current.Resolve(maxHeight, 0);
		UpdatePassThroughInteraction();
	}

	// No dragging on Windows: the sheet rests at the selected detent, or the smallest one.
	SheetDetent? CurrentDetent() =>
		sorted.Count == 0 ? null : sorted.Find(d => d.Detent == VirtualView.SelectedDetent).Detent ?? sorted[0].Detent;

	void UpdateSelectedDetent() => UpdateDetents();

	void OnXamlRootChanged(UI.XamlRoot sender, UI.XamlRootChangedEventArgs e) => UpdateDetents();

	void UpdatePassThroughInteraction()
	{
		if (backdrop is null)
			return;
		var passThrough = LargestUndimmed(VirtualView, sorted) is not null && CurrentDetent()?.Kind != SheetDetentKind.Large;
		backdrop.Visibility = passThrough ? UI.Visibility.Collapsed : UI.Visibility.Visible;
	}

	void UpdateBackground()
	{
		if (card is null)
			return;
		card.Background = VirtualView.BackgroundColor?.ToPlatform()
			?? ThemeResource<UI.Media.Brush>("ContentDialogBackground")
			?? ThemeResource<UI.Media.Brush>("SolidBackgroundFillColorBaseBrush");
		card.CornerRadius = VirtualView.CornerRadius >= 0
			? new UI.CornerRadius(VirtualView.CornerRadius)
			: ThemeResource<object>("OverlayCornerRadius") as UI.CornerRadius? ?? new UI.CornerRadius(8);
	}

	void UpdateTitle()
	{
		if (titleText is null)
			return;
		titleText.Text = VirtualView.Title ?? string.Empty;
		titleText.Visibility = Visible(VirtualView.Title);
		UpdateHeaderVisibility();
	}

	void UpdateSubtitle()
	{
		if (subtitleText is null)
			return;
		subtitleText.Text = VirtualView.Subtitle ?? string.Empty;
		subtitleText.Visibility = Visible(VirtualView.Subtitle);
		UpdateHeaderVisibility();
	}

	void UpdateActions()
	{
		if (closeButton is null || secondaryButton is null || primaryButton is null)
			return;
		closeButton.Visibility = VirtualView.ShowNativeCloseButton ? UI.Visibility.Visible : UI.Visibility.Collapsed;
		secondaryButton.Content = VirtualView.SecondaryActionText;
		secondaryButton.Visibility = Visible(VirtualView.SecondaryActionText);
		primaryButton.Content = VirtualView.PrimaryActionText;
		primaryButton.Visibility = Visible(VirtualView.PrimaryActionText);
		UpdateIsPrimaryActionEnabled();
		UpdateHeaderVisibility();
	}

	void UpdateIsPrimaryActionEnabled()
	{
		if (primaryButton is not null)
			primaryButton.IsEnabled = VirtualView.IsPrimaryActionEnabled;
	}

	void UpdateHeaderVisibility()
	{
		if (header is not null)
			header.Visibility = VirtualView.HasHeader ? UI.Visibility.Visible : UI.Visibility.Collapsed;
	}

	void OnPrimaryClick(object sender, UI.RoutedEventArgs e) => VirtualView?.SendPrimaryAction();

	void OnSecondaryClick(object sender, UI.RoutedEventArgs e) => VirtualView?.SendSecondaryAction();

	void OnCloseClick(object sender, UI.RoutedEventArgs e)
	{
		if (VirtualView is not null)
			VirtualView.IsOpen = false;
	}

	static UI.Visibility Visible(string? text) => string.IsNullOrEmpty(text) ? UI.Visibility.Collapsed : UI.Visibility.Visible;

	static T? ThemeResource<T>(string key) where T : class =>
		UI.Application.Current.Resources.TryGetValue(key, out var value) ? value as T : null;
}

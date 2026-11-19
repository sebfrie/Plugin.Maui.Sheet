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
	UIC.Grid? header;
	UIC.TextBlock? titleText;
	UIC.TextBlock? subtitleText;
	UIC.Button? backButton;
	UIC.Button? closeButton;
	UIC.Button? secondaryButton;
	UIC.Button? primaryButton;
	UIC.StackPanel? trailing;
	UIC.Grid? contentHost;
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
		backButton = new UIC.Button { Content = new UIC.FontIcon { Glyph = "\uE72B", FontSize = 12 } };
		UI.Automation.AutomationProperties.SetName(backButton, "Back");
		closeButton = new UIC.Button { Content = new UIC.FontIcon { Glyph = "\uE711", FontSize = 12 } };
		UI.Automation.AutomationProperties.SetName(closeButton, "Close");
		secondaryButton = new UIC.Button();
		primaryButton = new UIC.Button { Style = ThemeResource<UI.Style>("AccentButtonStyle") };
		backButton.Click += OnBackClick;
		closeButton.Click += OnCloseClick;
		secondaryButton.Click += OnSecondaryClick;
		primaryButton.Click += OnPrimaryClick;

		var leading = new UIC.StackPanel { Orientation = UIC.Orientation.Horizontal, Spacing = 8, Children = { backButton, closeButton, secondaryButton } };
		var title = new UIC.StackPanel { VerticalAlignment = UI.VerticalAlignment.Center, Children = { titleText, subtitleText } };
		trailing = new UIC.StackPanel { Orientation = UIC.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = UI.HorizontalAlignment.Right };
		UIC.Grid.SetColumn(title, 1);
		UIC.Grid.SetColumn(trailing, 2);
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
			Children = { leading, title, trailing },
		};

		// Each level of the navigation stack is swapped in here and slides in (see ShowTop).
		contentHost = new UIC.Grid();
		UIC.Grid.SetRow(contentHost, 1);
		var layout = new UIC.Grid
		{
			RowDefinitions =
			{
				new UIC.RowDefinition { Height = UI.GridLength.Auto },
				new UIC.RowDefinition { Height = new UI.GridLength(1, UI.GridUnitType.Star) },
			},
			Children = { header, contentHost },
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
		ShowTop(false, false);
		UpdateBackground();
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
		// Like the system back on Android: Esc goes back a level, and only tries to dismiss at the root.
		if (VirtualView?.NavigationStack.Count > 1)
			VirtualView.GoBack();
		else
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
		backButton!.Click -= OnBackClick;
		closeButton!.Click -= OnCloseClick;
		secondaryButton!.Click -= OnSecondaryClick;
		primaryButton!.Click -= OnPrimaryClick;
		// The MAUI views keep their platform views for the next presentation; each may only have one parent.
		contentHost!.Children.Clear();
		popup.Child = null;
		VirtualView?.ReleaseTemplatedContent();

		popup = null;
		host = null;
		backdrop = null;
		card = null;
		header = null;
		titleText = null;
		subtitleText = null;
		backButton = null;
		closeButton = null;
		secondaryButton = null;
		primaryButton = null;
		trailing = null;
		contentHost = null;
		xamlRoot = null;
		sorted = [];
	}

	// The root view changed; only visible while nothing is pushed.
	void UpdateContent()
	{
		if (VirtualView.NavigationStack.Count <= 1)
			ShowTop(false, false);
	}

	void Navigate(SheetNavigationRequest request)
	{
		ShowTop(request.Animated, request.Pushed is not null);
		request.Done.TrySetResult();
	}

	// Shows the top of the navigation stack; a push slides it in from the right, a pop from the left.
	void ShowTop(bool animated, bool forward)
	{
		if (contentHost is null)
			return;
		contentHost.ChildrenTransitions = animated
			? new UI.Media.Animation.TransitionCollection
			{
				new UI.Media.Animation.EntranceThemeTransition { FromHorizontalOffset = forward ? 80 : -80, FromVerticalOffset = 0 },
			}
			: null;
		contentHost.Children.Clear();
		if (VirtualView.NavigationStack is [.., var top] && top.ToPlatform(MauiContext!) is { } content)
		{
			(content.Parent as UIC.Panel)?.Children.Remove(content);
			contentHost.Children.Add(content);
		}
		UpdateHeader();
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
		var fitHeight = VirtualView.Detents.Contains(SheetDetent.FitContent) && VirtualView.NavigationStack is [.., IView content]
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

	// Shows the header of the top of the navigation stack.
	void UpdateHeader()
	{
		if (header is null)
			return;
		var model = VirtualView.HeaderAt(Math.Max(0, VirtualView.NavigationStack.Count - 1));
		titleText!.Text = model.Title ?? string.Empty;
		titleText.Visibility = Visible(model.Title);
		subtitleText!.Text = model.Subtitle ?? string.Empty;
		subtitleText.Visibility = Visible(model.Subtitle);
		backButton!.Visibility = model.ShowBack ? UI.Visibility.Visible : UI.Visibility.Collapsed;
		closeButton!.Visibility = model.ShowClose ? UI.Visibility.Visible : UI.Visibility.Collapsed;
		secondaryButton!.Content = model.SecondaryActionText;
		secondaryButton.Visibility = Visible(model.SecondaryActionText);

		trailing!.Children.Clear();
		foreach (var item in model.Items)
			trailing.Children.Add(ToolbarButton(item));
		if (model.OverflowItems.Count > 0)
			trailing.Children.Add(OverflowButton(model.OverflowItems));
		if (!string.IsNullOrEmpty(model.PrimaryActionText))
		{
			primaryButton!.Content = model.PrimaryActionText;
			primaryButton.IsEnabled = model.IsPrimaryActionEnabled;
			trailing.Children.Add(primaryButton);
		}
		header.Visibility = model.IsEmpty ? UI.Visibility.Collapsed : UI.Visibility.Visible;
	}

	UIC.Button ToolbarButton(ToolbarItem item)
	{
		var button = new UIC.Button { Content = item.Text, IsEnabled = item.IsEnabled };
		Describe(button, item);
		button.Click += (_, _) => ((IMenuItemController)item).Activate();
		switch (item.IconImageSource)
		{
			// A font glyph takes the header's text colour unless the source sets its own, like a template image on iOS.
			// A TextBlock, unlike FontIcon, falls back to other fonts for glyphs the font lacks.
			case FontImageSource { Glyph: { } glyph } font:
				var fontManager = MauiContext!.Services.GetRequiredService<IFontManager>();
				var text = new UIC.TextBlock
				{
					Text = glyph,
					FontFamily = fontManager.GetFontFamily(Microsoft.Maui.Font.OfSize(font.FontFamily, 16)),
					FontSize = 16,
				};
				if (font.Color is { } color)
					text.Foreground = color.ToPlatform();
				button.Content = text;
				break;
			case { } source:
				source.LoadImage(MauiContext!, result =>
				{
					if (result?.Value is { } image)
						button.Content = new UIC.Image { Source = image, Width = 16, Height = 16 };
				});
				break;
		}
		if (item.IconImageSource is not null)
			UIC.ToolTipService.SetToolTip(button, item.Text);
		return button;
	}

	static UIC.Button OverflowButton(IReadOnlyList<ToolbarItem> items)
	{
		var menu = new UIC.MenuFlyout();
		foreach (var item in items)
		{
			var entry = new UIC.MenuFlyoutItem { Text = item.Text, IsEnabled = item.IsEnabled };
			Describe(entry, item);
			entry.Click += (_, _) => ((IMenuItemController)item).Activate();
			menu.Items.Add(entry);
		}
		var button = new UIC.Button { Content = new UIC.FontIcon { Glyph = "\uE712", FontSize = 16 }, Flyout = menu };
		UI.Automation.AutomationProperties.SetName(button, "More options");
		UI.Automation.AutomationProperties.SetAutomationId(button, "NativeSheetOverflow");
		return button;
	}

	static void Describe(UI.DependencyObject element, ToolbarItem item)
	{
		UI.Automation.AutomationProperties.SetName(element, item.Text ?? string.Empty);
		if (!string.IsNullOrEmpty(item.AutomationId))
			UI.Automation.AutomationProperties.SetAutomationId(element, item.AutomationId);
	}

	void OnBackClick(object sender, UI.RoutedEventArgs e) => VirtualView?.GoBack();

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

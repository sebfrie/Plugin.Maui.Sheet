using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.AppBar;
using Google.Android.Material.BottomSheet;
using AndroidX.Core.View;
using Google.Android.Material.Shape;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using Behavior = Google.Android.Material.BottomSheet.BottomSheetBehavior;

namespace Plugin.Maui.Sheet;

public partial class NativeSheetHandler
{
	const int SecondaryItemId = 1;
	const int PrimaryItemId = 2;

	SheetDialog? dialog;
	LinearLayout? root;
	MaterialToolbar? toolbar;
	BottomSheetDragHandleView? grabber;
	AView? platformContent;
	SheetCallback? sheetCallback;
	Drawable? defaultSheetBackground;
	List<(SheetDetent Detent, double Height)> resolved = [];
	List<(SheetDetent Detent, double Height)> stops = [];
	bool passThroughActive;

	/// <inheritdoc />
	protected override AView CreatePlatformView() => new(Context);

	FrameLayout? SheetView => root?.Parent as FrameLayout;

	void Present()
	{
		if (dialog is not null)
			return;
		if (VirtualView.Window?.Handler?.PlatformView is not Activity activity)
		{
			PresentWhenLoaded();
			return;
		}

		root = new LinearLayout(activity) { Orientation = Android.Widget.Orientation.Vertical };
		// MAUI's theme makes action text white for its coloured navigation bar; the sheet is a surface.
		toolbar = new MaterialToolbar(new ContextThemeWrapper(activity, Resource.Style.ThemeOverlay_MaterialComponents_Toolbar_Surface));
		toolbar.MenuItemClick += OnMenuItemClick;
		toolbar.NavigationClick += OnNavigationClick;
		root.AddView(toolbar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

		dialog = new SheetDialog(activity, this);
		dialog.SetContentView(root, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
		dialog.ShowEvent += OnDialogShown;
		dialog.DismissEvent += OnDialogDismissed;
		sheetCallback = new SheetCallback(this);
		dialog.Behavior.AddBottomSheetCallback(sheetCallback);

		VirtualView.CreateTemplatedContent();
		UpdateShowGrabber();
		UpdateTitle();
		UpdateSubtitle();
		UpdateActions();
		UpdateContent();
		UpdateIsDismissible();
		UpdateIsDraggable();
		UpdateDetents();
		dialog.Show();

		// Posted after Show() it runs after the first layout pass: the real parent height is known and
		// BottomSheetBehavior has installed its own background, which a custom corner radius replaces.
		SheetView?.Post(() =>
		{
			UpdateBackground();
			UpdateDetents();
		});
	}

	void Close()
	{
		if (dialog is null)
			return;
		// Dismiss first: the window leaves with its content still in place.
		dialog.Dismiss();
		TearDown();
		VirtualView?.SendDismissed();
	}

	void OnDialogShown(object? sender, EventArgs e) => VirtualView?.SendOpened(CurrentDetent());

	// Back button, tap outside or swipe away, after SheetDialog.Cancel() allowed it.
	void OnDialogDismissed(object? sender, EventArgs e)
	{
		if (sender != dialog)
			return;
		TearDown();
		VirtualView?.SendDismissed();
	}

	void TearDown()
	{
		if (dialog is null)
			return;
		dialog.ShowEvent -= OnDialogShown;
		dialog.DismissEvent -= OnDialogDismissed;
		if (sheetCallback is not null)
			dialog.Behavior.RemoveBottomSheetCallback(sheetCallback);
		if (toolbar is not null)
		{
			toolbar.MenuItemClick -= OnMenuItemClick;
			toolbar.NavigationClick -= OnNavigationClick;
		}
		// The MAUI content keeps its platform view for the next presentation; it may only have one parent.
		(platformContent?.Parent as ViewGroup)?.RemoveView(platformContent);
		VirtualView?.ReleaseTemplatedContent();

		dialog = null;
		root = null;
		toolbar = null;
		grabber = null;
		platformContent = null;
		sheetCallback = null;
		defaultSheetBackground = null;
		resolved = [];
		stops = [];
		passThroughActive = false;
	}

	void UpdateContent()
	{
		if (root is null)
			return;
		(platformContent?.Parent as ViewGroup)?.RemoveView(platformContent);
		platformContent = VirtualView.PresentedContent?.ToPlatform(MauiContext!);
		if (platformContent is null)
			return;
		(platformContent.Parent as ViewGroup)?.RemoveView(platformContent);
		// Weight 1: fills the sheet when its height is fixed, wraps the content when the sheet wraps (FitContent).
		root.AddView(platformContent, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
		InvalidateDetentsCore();
	}

	void InvalidateDetentsCore()
	{
		if (VirtualView.Detents.Contains(SheetDetent.FitContent))
			UpdateDetents();
	}

	void UpdateDetents()
	{
		if (dialog is null || SheetView is not { } sheetView || root is null)
			return;

		var behavior = dialog.Behavior;
		var parentHeight = (sheetView.Parent as AView)?.Height is > 0 and var h ? h : ActivityHeight(dialog);
		var width = sheetView.Width > 0 ? sheetView.Width : (sheetView.Parent as AView)?.Width ?? 0;
		var contentHeight = 0;
		if (width > 0 && VirtualView.Detents.Contains(SheetDetent.FitContent))
		{
			root.Measure(
				AView.MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly),
				AView.MeasureSpec.MakeMeasureSpec(parentHeight, MeasureSpecMode.AtMost));
			// The sheet pads itself for the navigation bar (edge-to-edge); a fixed height must include that.
			contentHeight = root.MeasuredHeight + sheetView.PaddingTop + sheetView.PaddingBottom;
		}

		var maxDp = Context.FromPixels(parentHeight);
		resolved = Resolve(VirtualView.Detents, maxDp, Context.FromPixels(contentHeight))
			.Select(d => (d.Detent, Height: Math.Round(Context.ToPixels(d.Height))))
			.ToList();
		var distinct = resolved.DistinctBy(d => d.Height).ToList();
		// BottomSheetBehavior has three resting states: collapsed, half expanded and expanded.
		stops = distinct.Count <= 3 ? distinct : [distinct[0], distinct[distinct.Count / 2], distinct[^1]];

		var largest = stops[^1];
		var layout = sheetView.LayoutParameters!;
		layout.Height = largest.Detent.Kind == SheetDetentKind.Large ? ViewGroup.LayoutParams.MatchParent
			: stops.Count == 1 && largest.Detent.Kind == SheetDetentKind.FitContent ? ViewGroup.LayoutParams.WrapContent
			: (int)largest.Height;
		sheetView.LayoutParameters = layout;

		behavior.FitToContents = stops.Count < 3;
		// A single stop: collapsed and expanded must be the same position.
		behavior.PeekHeight = stops.Count == 1 ? parentHeight : (int)stops[0].Height;
		if (stops.Count == 3)
		{
			behavior.ExpandedOffset = Math.Max(0, parentHeight - (int)largest.Height);
			behavior.HalfExpandedRatio = Math.Clamp((float)(stops[1].Height / parentHeight), 0.01f, 0.99f);
		}

		UpdateSelectedDetent();
	}

	void UpdateSelectedDetent()
	{
		if (dialog is null || stops.Count == 0)
			return;
		var state = SelectedState();
		if (dialog.Behavior.State != state)
			dialog.Behavior.State = state;
		UpdatePassThroughInteraction();
	}

	int SelectedState()
	{
		var index = IndexOf(VirtualView.SelectedDetent);
		return stops.Count == 1 || index == stops.Count - 1 ? Behavior.StateExpanded
			: index == 0 ? Behavior.StateCollapsed
			: Behavior.StateHalfExpanded;
	}

	// Detents sharing a height were merged into one stop, extra detents snap to the next larger stop,
	// and null (or an unknown detent) is the smallest stop.
	int IndexOf(SheetDetent? detent)
	{
		var height = resolved.Find(r => r.Detent == detent).Height;
		var index = stops.FindIndex(s => s.Height >= height);
		return index >= 0 ? index : stops.Count - 1;
	}

	SheetDetent? CurrentDetent()
	{
		if (dialog is null || stops.Count == 0)
			return null;
		return dialog.Behavior.State switch
		{
			Behavior.StateExpanded => stops[^1].Detent,
			Behavior.StateHalfExpanded when stops.Count == 3 => stops[1].Detent,
			Behavior.StateCollapsed => stops[0].Detent,
			_ => null,
		};
	}

	void OnSheetStateChanged(int state)
	{
		if (state is Behavior.StateHidden or Behavior.StateDragging or Behavior.StateSettling || CurrentDetent() is not { } detent)
			return;
		VirtualView?.SendDetentChanged(detent);
		UpdatePassThroughInteraction();
	}

	void UpdatePassThroughInteraction()
	{
		if (dialog?.Window is not { } window)
			return;
		var undimmed = LargestUndimmed(VirtualView, stops);
		passThroughActive = undimmed is not null && IndexOf(CurrentDetent()) <= IndexOf(undimmed);
		if (passThroughActive)
			window.ClearFlags(WindowManagerFlags.DimBehind);
		else
			window.AddFlags(WindowManagerFlags.DimBehind);
		dialog.SetCanceledOnTouchOutside(!passThroughActive);
	}

	bool ShouldPassThrough(MotionEvent e)
	{
		if (!passThroughActive || SheetView is not { } sheetView)
			return false;
		var bounds = new Android.Graphics.Rect();
		return !sheetView.GetGlobalVisibleRect(bounds) || !bounds.Contains((int)e.RawX, (int)e.RawY);
	}

	// The dialog window covers the whole screen, so touches outside the sheet are re-dispatched to the activity.
	void PassThrough(MotionEvent e)
	{
		if (VirtualView?.Window?.Handler?.PlatformView is not Activity { Window.DecorView: { } decor } activity)
			return;
		var origin = new int[2];
		decor.GetLocationOnScreen(origin);
		var copy = MotionEvent.Obtain(e)!;
		copy.SetLocation(e.RawX - origin[0], e.RawY - origin[1]);
		activity.DispatchTouchEvent(copy);
		copy.Recycle();
	}

	void UpdateIsDraggable()
	{
		if (dialog is not null)
			dialog.Behavior.Draggable = VirtualView.IsDraggable;
	}

	void UpdateIsDismissible()
	{
		if (dialog is not null)
			dialog.Behavior.Hideable = VirtualView.IsDismissible;
	}

	// SheetDialog.Cancel() is the single path for every user dismissal.
	bool ShouldStayOpen()
	{
		if (VirtualView?.SendDismissAttempted() != true)
			return false;
		// Swiped away: slide back to where it rested, which SelectedDetent still tracks.
		if (dialog?.Behavior is { State: Behavior.StateHidden } behavior)
			behavior.State = SelectedState();
		return true;
	}

	void UpdateShowGrabber()
	{
		if (root is null || dialog is null)
			return;
		if (VirtualView.ShowGrabber && grabber is null)
		{
			grabber = new BottomSheetDragHandleView(Material3Context(dialog.Context));
			// Material's handle view is 48dp tall with 20dp bottom padding: the handle sits 12dp from the top and
			// 32dp of empty space follow, which above a toolbar left a ~45dp gap. 28dp without padding keeps the
			// handle at 12dp and puts the header right below it, as in Material 3 sheets.
			grabber.SetPadding(0, 0, 0, 0);
			root.AddView(grabber, 0, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, (int)Context.ToPixels(28)));
		}
		else if (!VirtualView.ShowGrabber && grabber is not null)
		{
			root.RemoveView(grabber);
			grabber = null;
		}
		InvalidateDetentsCore();
	}

	// CornerRadius and BackgroundColor both replace the sheet's background with one MaterialShapeDrawable.
	void UpdateBackground()
	{
		if (SheetView is not { } sheetView)
			return;
		var color = VirtualView.BackgroundColor;
		if (VirtualView.CornerRadius < 0 && color is null)
		{
			if (defaultSheetBackground is not null)
				sheetView.Background = defaultSheetBackground;
			defaultSheetBackground = null;
			return;
		}

		defaultSheetBackground ??= sheetView.Background;
		var original = defaultSheetBackground as MaterialShapeDrawable;
		var radius = Context.ToPixels(VirtualView.CornerRadius);
		var shape = new MaterialShapeDrawable(VirtualView.CornerRadius < 0 && original is not null
			? original.ShapeAppearanceModel
			: ShapeAppearanceModel.InvokeBuilder()
				.SetTopLeftCorner(CornerFamily.Rounded, Math.Max(0, radius))
				.SetTopRightCorner(CornerFamily.Rounded, Math.Max(0, radius))
				.Build());
		if (color is not null)
		{
			shape.FillColor = ColorStateList.ValueOf(color.ToPlatform());
		}
		else if (original is not null)
		{
			shape.FillColor = original.FillColor;
			shape.InitializeElevationOverlay(sheetView.Context);
			shape.Elevation = original.Elevation;
		}
		else
		{
			shape.FillColor = ColorStateList.ValueOf(ThemeColor(sheetView.Context!, Resource.Attribute.colorSurface));
		}
		sheetView.Background = shape;
	}

	void UpdateTitle()
	{
		if (toolbar is null)
			return;
		toolbar.Title = VirtualView.Title;
		UpdateHeaderVisibility();
	}

	void UpdateSubtitle()
	{
		if (toolbar is null)
			return;
		toolbar.Subtitle = VirtualView.Subtitle;
		UpdateHeaderVisibility();
	}

	void UpdateActions()
	{
		if (toolbar?.Menu is not { } menu)
			return;

		toolbar.NavigationIcon = VirtualView.ShowNativeCloseButton
			? AndroidX.AppCompat.Content.Res.AppCompatResources.GetDrawable(toolbar.Context!, Resource.Drawable.abc_ic_clear_material)
			: null;
		toolbar.NavigationContentDescription = VirtualView.ShowNativeCloseButton
			? toolbar.Context!.GetString(Android.Resource.String.Cancel)
			: null;

		menu.Clear();
		if (!string.IsNullOrEmpty(VirtualView.SecondaryActionText))
			menu.Add(0, SecondaryItemId, 0, VirtualView.SecondaryActionText)!.SetShowAsAction(ShowAsAction.Always);
		if (!string.IsNullOrEmpty(VirtualView.PrimaryActionText))
			menu.Add(0, PrimaryItemId, 1, VirtualView.PrimaryActionText)!.SetShowAsAction(ShowAsAction.Always);

		UpdateIsPrimaryActionEnabled();
		UpdateHeaderVisibility();
	}

	void UpdateIsPrimaryActionEnabled() =>
		toolbar?.Menu?.FindItem(PrimaryItemId)?.SetEnabled(VirtualView.IsPrimaryActionEnabled);

	void UpdateHeaderVisibility()
	{
		if (toolbar is null)
			return;
		var visibility = VirtualView.HasHeader ? ViewStates.Visible : ViewStates.Gone;
		if (toolbar.Visibility == visibility)
			return;
		toolbar.Visibility = visibility;
		InvalidateDetentsCore();
	}

	void OnMenuItemClick(object? sender, AndroidX.AppCompat.Widget.Toolbar.MenuItemClickEventArgs e)
	{
		if (e.Item?.ItemId == PrimaryItemId)
			VirtualView?.SendPrimaryAction();
		else if (e.Item?.ItemId == SecondaryItemId)
			VirtualView?.SendSecondaryAction();
	}

	void OnNavigationClick(object? sender, AndroidX.AppCompat.Widget.Toolbar.NavigationClickEventArgs e)
	{
		if (VirtualView is not null)
			VirtualView.IsOpen = false;
	}

	static int ActivityHeight(Dialog dialog) =>
		dialog.OwnerActivity?.Window?.DecorView?.Height is > 0 and var height
			? height
			: dialog.Context.Resources!.DisplayMetrics!.HeightPixels;

	// The drag handle gets its icon and size only from the theme's bottomSheetDragHandleStyle, which Material 3
	// themes define and MAUI's default MaterialComponents theme does not (the handle would be 0x0).
	static Context Material3Context(Context context) =>
		context.Theme!.ResolveAttribute(Resource.Attribute.bottomSheetDragHandleStyle, new TypedValue(), true)
			? context
			: new ContextThemeWrapper(context, Resource.Style.Theme_Material3_DayNight);

	static Android.Graphics.Color ThemeColor(Context context, int attribute)
	{
		var value = new TypedValue();
		return context.Theme!.ResolveAttribute(attribute, value, true)
			? new Android.Graphics.Color(value.Data)
			: Android.Graphics.Color.White;
	}

	sealed class SheetDialog(Context context, NativeSheetHandler handler) : BottomSheetDialog(context)
	{
		bool passingThrough;

		public override void OnAttachedToWindow()
		{
			// The focused window decides the status bar icon color, and the dialog is focused while open. Copy the
			// activity's runtime choice (e.g. light icons over a dark page); the theme default may be the opposite.
			// Done before base so Material's edge-to-edge handling restores this value when the sheet leaves the top.
			if (OwnerActivity?.Window is { DecorView: { } activityDecor } activityWindow
				&& Window is { DecorView: { } decor } window
				&& WindowCompat.GetInsetsController(activityWindow, activityDecor) is { } activityBars
				&& WindowCompat.GetInsetsController(window, decor) is { } sheetBars)
			{
				sheetBars.AppearanceLightStatusBars = activityBars.AppearanceLightStatusBars;
			}
			base.OnAttachedToWindow();
		}

		public override void Cancel()
		{
			if (handler.dialog == this && handler.ShouldStayOpen())
				return;
			base.Cancel();
		}

		public override bool DispatchTouchEvent(MotionEvent e)
		{
			if (e.ActionMasked == MotionEventActions.Down)
				passingThrough = handler.dialog == this && handler.ShouldPassThrough(e);
			if (!passingThrough)
				return base.DispatchTouchEvent(e);
			handler.PassThrough(e);
			return true;
		}
	}

	sealed class SheetCallback(NativeSheetHandler handler) : Behavior.BottomSheetCallback
	{
		public override void OnStateChanged(AView bottomSheet, int newState) => handler.OnSheetStateChanged(newState);

		// Required by the abstract base class; sliding needs no handling.
		public override void OnSlide(AView bottomSheet, float slideOffset)
		{
		}
	}
}

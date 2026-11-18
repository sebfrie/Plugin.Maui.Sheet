using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace Plugin.Maui.Sheet;

/// <summary>
/// A native bottom sheet: <c>UISheetPresentationController</c> + <c>UINavigationBar</c> on iOS/Mac Catalyst,
/// Material <c>BottomSheetDialog</c> + <c>MaterialToolbar</c> on Android.
/// </summary>
/// <remarks>
/// Declare it anywhere inside a page's layout; it takes no space there. Its <see cref="Content"/> is shown
/// inside the native sheet below the native header while <see cref="IsOpen"/> is <see langword="true"/>.
/// </remarks>
[ContentProperty(nameof(Content))]
public class NativeSheet : View
{
	static readonly IList<SheetDetent> DefaultDetents = Array.AsReadOnly([SheetDetent.Medium, SheetDetent.Large]);

	/// <summary>Bindable property for <see cref="IsOpen"/>.</summary>
	public static readonly BindableProperty IsOpenProperty =
		BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(NativeSheet), false, BindingMode.TwoWay);

	/// <summary>Bindable property for <see cref="IsDismissible"/>.</summary>
	public static readonly BindableProperty IsDismissibleProperty =
		BindableProperty.Create(nameof(IsDismissible), typeof(bool), typeof(NativeSheet), true);

	/// <summary>Bindable property for <see cref="ShowGrabber"/>.</summary>
	public static readonly BindableProperty ShowGrabberProperty =
		BindableProperty.Create(nameof(ShowGrabber), typeof(bool), typeof(NativeSheet), true);

	/// <summary>Bindable property for <see cref="CornerRadius"/>.</summary>
	public static readonly BindableProperty CornerRadiusProperty =
		BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(NativeSheet), -1d);

	/// <summary>Bindable property for <see cref="PassThroughInteraction"/>.</summary>
	public static readonly BindableProperty PassThroughInteractionProperty =
		BindableProperty.Create(nameof(PassThroughInteraction), typeof(bool), typeof(NativeSheet), false);

	/// <summary>Bindable property for <see cref="Detents"/>.</summary>
	public static readonly BindableProperty DetentsProperty =
		BindableProperty.Create(nameof(Detents), typeof(IList<SheetDetent>), typeof(NativeSheet), DefaultDetents,
			validateValue: (_, value) => value is IList<SheetDetent> { Count: > 0 },
			propertyChanged: OnDetentsChanged);

	/// <summary>Bindable property for <see cref="SelectedDetent"/>.</summary>
	public static readonly BindableProperty SelectedDetentProperty =
		BindableProperty.Create(nameof(SelectedDetent), typeof(SheetDetent), typeof(NativeSheet), null, BindingMode.TwoWay);

	/// <summary>Bindable property for <see cref="Title"/>.</summary>
	public static readonly BindableProperty TitleProperty =
		BindableProperty.Create(nameof(Title), typeof(string), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="Subtitle"/>.</summary>
	public static readonly BindableProperty SubtitleProperty =
		BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="PrimaryActionText"/>.</summary>
	public static readonly BindableProperty PrimaryActionTextProperty =
		BindableProperty.Create(nameof(PrimaryActionText), typeof(string), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="PrimaryActionCommand"/>.</summary>
	public static readonly BindableProperty PrimaryActionCommandProperty =
		BindableProperty.Create(nameof(PrimaryActionCommand), typeof(ICommand), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="PrimaryActionCommandParameter"/>.</summary>
	public static readonly BindableProperty PrimaryActionCommandParameterProperty =
		BindableProperty.Create(nameof(PrimaryActionCommandParameter), typeof(object), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="IsPrimaryActionEnabled"/>.</summary>
	public static readonly BindableProperty IsPrimaryActionEnabledProperty =
		BindableProperty.Create(nameof(IsPrimaryActionEnabled), typeof(bool), typeof(NativeSheet), true);

	/// <summary>Bindable property for <see cref="SecondaryActionText"/>.</summary>
	public static readonly BindableProperty SecondaryActionTextProperty =
		BindableProperty.Create(nameof(SecondaryActionText), typeof(string), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="SecondaryActionCommand"/>.</summary>
	public static readonly BindableProperty SecondaryActionCommandProperty =
		BindableProperty.Create(nameof(SecondaryActionCommand), typeof(ICommand), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="SecondaryActionCommandParameter"/>.</summary>
	public static readonly BindableProperty SecondaryActionCommandParameterProperty =
		BindableProperty.Create(nameof(SecondaryActionCommandParameter), typeof(object), typeof(NativeSheet), null);

	/// <summary>Bindable property for <see cref="ShowNativeCloseButton"/>.</summary>
	public static readonly BindableProperty ShowNativeCloseButtonProperty =
		BindableProperty.Create(nameof(ShowNativeCloseButton), typeof(bool), typeof(NativeSheet), false);

	/// <summary>Bindable property for <see cref="Content"/>.</summary>
	public static readonly BindableProperty ContentProperty =
		BindableProperty.Create(nameof(Content), typeof(View), typeof(NativeSheet), null, propertyChanged: OnContentChanged);

	/// <summary>Raised after the sheet finished presenting.</summary>
	public event EventHandler? Opened;

	/// <summary>Raised after the sheet was dismissed, by code or by the user. <see cref="IsOpen"/> is already <see langword="false"/>.</summary>
	public event EventHandler? Dismissed;

	/// <summary>Raised when the user moves the sheet to another detent.</summary>
	public event EventHandler<SheetDetentChangedEventArgs>? DetentChanged;

	/// <summary>
	/// Raised when the user tries to dismiss the sheet (swipe down, tap outside, back button).
	/// <see cref="CancelEventArgs.Cancel"/> starts as <see langword="true"/> when <see cref="IsDismissible"/> is <see langword="false"/>;
	/// set it to keep the sheet open, or clear it to let the sheet close anyway.
	/// </summary>
	public event EventHandler<CancelEventArgs>? DismissAttempted;

	/// <summary>Gets or sets whether the sheet is presented. Updated when the user dismisses the sheet.</summary>
	public bool IsOpen
	{
		get => (bool)GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}

	/// <summary>Gets or sets whether the user can dismiss the sheet interactively. Default <see langword="true"/>.</summary>
	public bool IsDismissible
	{
		get => (bool)GetValue(IsDismissibleProperty);
		set => SetValue(IsDismissibleProperty, value);
	}

	/// <summary>Gets or sets whether the native drag handle is shown. Default <see langword="true"/>.</summary>
	public bool ShowGrabber
	{
		get => (bool)GetValue(ShowGrabberProperty);
		set => SetValue(ShowGrabberProperty, value);
	}

	/// <summary>Gets or sets the top corner radius of the sheet. A negative value (default) keeps the OS default.</summary>
	public double CornerRadius
	{
		get => (double)GetValue(CornerRadiusProperty);
		set => SetValue(CornerRadiusProperty, value);
	}

	/// <summary>
	/// Gets or sets whether the page below stays interactive (and undimmed) while the sheet rests at any detent
	/// other than <see cref="SheetDetent.Large"/>. Default <see langword="false"/>.
	/// </summary>
	public bool PassThroughInteraction
	{
		get => (bool)GetValue(PassThroughInteractionProperty);
		set => SetValue(PassThroughInteractionProperty, value);
	}

	/// <summary>
	/// Gets or sets the heights the sheet can rest at. Default <c>Medium, Large</c>. An empty list is ignored.
	/// Order does not matter. Android supports at most three distinct heights (smallest, middle and largest are kept).
	/// </summary>
	[TypeConverter(typeof(SheetDetentsTypeConverter))]
	public IList<SheetDetent> Detents
	{
		get => (IList<SheetDetent>)GetValue(DetentsProperty);
		set => SetValue(DetentsProperty, value);
	}

	/// <summary>
	/// Gets or sets the detent the sheet rests at. <see langword="null"/> opens at the smallest detent.
	/// Updated when the user drags the sheet.
	/// </summary>
	public SheetDetent? SelectedDetent
	{
		get => (SheetDetent?)GetValue(SelectedDetentProperty);
		set => SetValue(SelectedDetentProperty, value);
	}

	/// <summary>Gets or sets the native header title.</summary>
	public string? Title
	{
		get => (string?)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <summary>Gets or sets the native header subtitle (Android, iOS/Mac Catalyst 26+).</summary>
	public string? Subtitle
	{
		get => (string?)GetValue(SubtitleProperty);
		set => SetValue(SubtitleProperty, value);
	}

	/// <summary>Gets or sets the text of the native confirm action (trailing). No text, no button.</summary>
	public string? PrimaryActionText
	{
		get => (string?)GetValue(PrimaryActionTextProperty);
		set => SetValue(PrimaryActionTextProperty, value);
	}

	/// <summary>Gets or sets the command run by the primary action. Without a command the action closes the sheet.</summary>
	public ICommand? PrimaryActionCommand
	{
		get => (ICommand?)GetValue(PrimaryActionCommandProperty);
		set => SetValue(PrimaryActionCommandProperty, value);
	}

	/// <summary>Gets or sets the parameter passed to <see cref="PrimaryActionCommand"/>.</summary>
	public object? PrimaryActionCommandParameter
	{
		get => GetValue(PrimaryActionCommandParameterProperty);
		set => SetValue(PrimaryActionCommandParameterProperty, value);
	}

	/// <summary>Gets or sets whether the primary action can be tapped. Default <see langword="true"/>.</summary>
	public bool IsPrimaryActionEnabled
	{
		get => (bool)GetValue(IsPrimaryActionEnabledProperty);
		set => SetValue(IsPrimaryActionEnabledProperty, value);
	}

	/// <summary>Gets or sets the text of the native cancel action (leading). No text, no button.</summary>
	public string? SecondaryActionText
	{
		get => (string?)GetValue(SecondaryActionTextProperty);
		set => SetValue(SecondaryActionTextProperty, value);
	}

	/// <summary>Gets or sets the command run by the secondary action. Without a command the action closes the sheet.</summary>
	public ICommand? SecondaryActionCommand
	{
		get => (ICommand?)GetValue(SecondaryActionCommandProperty);
		set => SetValue(SecondaryActionCommandProperty, value);
	}

	/// <summary>Gets or sets the parameter passed to <see cref="SecondaryActionCommand"/>.</summary>
	public object? SecondaryActionCommandParameter
	{
		get => GetValue(SecondaryActionCommandParameterProperty);
		set => SetValue(SecondaryActionCommandParameterProperty, value);
	}

	/// <summary>Gets or sets whether the native close (X) button is shown at the leading edge. It always closes the sheet.</summary>
	public bool ShowNativeCloseButton
	{
		get => (bool)GetValue(ShowNativeCloseButtonProperty);
		set => SetValue(ShowNativeCloseButtonProperty, value);
	}

	/// <summary>Gets or sets the MAUI view shown in the sheet body, below the native header.</summary>
	public View? Content
	{
		get => (View?)GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	/// <summary>
	/// Re-measures <see cref="SheetDetent.FitContent"/>. Called automatically when the root of <see cref="Content"/>
	/// invalidates its measure; call it yourself after size changes deep inside the content.
	/// </summary>
	public void InvalidateDetents() => Handler?.Invoke(nameof(InvalidateDetents));

	/// <summary>The sheet takes no space in the page's layout; its content lives in the native sheet.</summary>
	protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => Size.Zero;

	internal bool HasHeader =>
		!string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Subtitle) ||
		!string.IsNullOrEmpty(PrimaryActionText) || !string.IsNullOrEmpty(SecondaryActionText) || ShowNativeCloseButton;

	internal void SendOpened(SheetDetent? current)
	{
		if (current is not null)
			SelectedDetent = current;
		Opened?.Invoke(this, EventArgs.Empty);
	}

	internal void SendDismissed()
	{
		IsOpen = false;
		Dismissed?.Invoke(this, EventArgs.Empty);
	}

	internal void SendDetentChanged(SheetDetent detent)
	{
		if (Equals(SelectedDetent, detent))
			return;
		SelectedDetent = detent;
		DetentChanged?.Invoke(this, new SheetDetentChangedEventArgs(detent));
	}

	/// <returns><see langword="true"/> when the sheet must stay open.</returns>
	internal bool SendDismissAttempted()
	{
		var args = new CancelEventArgs(!IsDismissible);
		DismissAttempted?.Invoke(this, args);
		return args.Cancel;
	}

	internal void SendPrimaryAction() => Execute(PrimaryActionCommand, PrimaryActionCommandParameter);

	internal void SendSecondaryAction() => Execute(SecondaryActionCommand, SecondaryActionCommandParameter);

	void Execute(ICommand? command, object? parameter)
	{
		if (command is null)
			IsOpen = false;
		else if (command.CanExecute(parameter))
			command.Execute(parameter);
	}

	static void OnDetentsChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var sheet = (NativeSheet)bindable;
		if (oldValue is INotifyCollectionChanged oldList)
			oldList.CollectionChanged -= sheet.OnDetentsCollectionChanged;
		if (newValue is INotifyCollectionChanged newList)
			newList.CollectionChanged += sheet.OnDetentsCollectionChanged;
	}

	void OnDetentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
		Handler?.UpdateValue(nameof(Detents));

	static void OnContentChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var sheet = (NativeSheet)bindable;
		if (oldValue is View oldView)
		{
			oldView.MeasureInvalidated -= sheet.OnContentMeasureInvalidated;
			sheet.RemoveLogicalChild(oldView);
		}
		if (newValue is View newView)
		{
			sheet.AddLogicalChild(newView);
			newView.MeasureInvalidated += sheet.OnContentMeasureInvalidated;
		}
	}

	void OnContentMeasureInvalidated(object? sender, EventArgs e)
	{
		if (IsOpen && Detents.Contains(SheetDetent.FitContent))
			InvalidateDetents();
	}
}

/// <summary>Event data for <see cref="NativeSheet.DetentChanged"/>.</summary>
/// <param name="detent">The detent the sheet now rests at.</param>
public sealed class SheetDetentChangedEventArgs(SheetDetent detent) : EventArgs
{
	/// <summary>Gets the detent the sheet now rests at.</summary>
	public SheetDetent Detent { get; } = detent;
}

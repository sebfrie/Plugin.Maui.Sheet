using System.ComponentModel;
using System.Globalization;

namespace Plugin.Maui.Sheet;

/// <summary>
/// The kind of height a <see cref="SheetDetent"/> describes.
/// </summary>
public enum SheetDetentKind
{
	/// <summary>About half of the available height.</summary>
	Medium,
	/// <summary>The full available height.</summary>
	Large,
	/// <summary>A fraction (0.1 – 1.0) of the available height.</summary>
	Fraction,
	/// <summary>A fixed height in device-independent units (dp on Android, pt on iOS).</summary>
	Height,
	/// <summary>The height the header plus the sheet content needs.</summary>
	FitContent,
}

/// <summary>
/// A height at which a <see cref="NativeSheet"/> can rest.
/// </summary>
/// <remarks>
/// In XAML a detent is written as <c>Medium</c>, <c>Large</c>, <c>FitContent</c>, <c>Fraction:0.3</c> or <c>Height:250</c>;
/// a list of detents is a comma separated string, for example <c>Detents="FitContent, Large"</c>.
/// </remarks>
[TypeConverter(typeof(SheetDetentTypeConverter))]
public sealed record SheetDetent
{
	SheetDetent(SheetDetentKind kind, double value)
	{
		Kind = kind;
		Value = value;
	}

	/// <summary>Gets the kind of this detent.</summary>
	public SheetDetentKind Kind { get; }

	/// <summary>Gets the fraction for <see cref="SheetDetentKind.Fraction"/>, the height for <see cref="SheetDetentKind.Height"/>, otherwise 0.</summary>
	public double Value { get; }

	/// <summary>About half of the available height.</summary>
	public static SheetDetent Medium { get; } = new(SheetDetentKind.Medium, 0);

	/// <summary>The full available height.</summary>
	public static SheetDetent Large { get; } = new(SheetDetentKind.Large, 0);

	/// <summary>Sizes the sheet to the native header plus the measured MAUI content.</summary>
	public static SheetDetent FitContent { get; } = new(SheetDetentKind.FitContent, 0);

	/// <summary>A fraction of the available height.</summary>
	/// <param name="fraction">A value between 0.1 and 1.0.</param>
	public static SheetDetent Fraction(double fraction) =>
		fraction is >= 0.1 and <= 1.0
			? new(SheetDetentKind.Fraction, fraction)
			: throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "A sheet fraction must be between 0.1 and 1.0.");

	/// <summary>A fixed height in device-independent units (dp on Android, pt on iOS).</summary>
	/// <param name="height">A positive height.</param>
	public static SheetDetent Height(double height) =>
		height > 0 && double.IsFinite(height)
			? new(SheetDetentKind.Height, height)
			: throw new ArgumentOutOfRangeException(nameof(height), height, "A sheet height must be a positive number.");

	/// <summary>
	/// Resolves this detent to a height, capped at <paramref name="maxHeight"/>.
	/// Both arguments and the result use the same unit.
	/// </summary>
	internal double Resolve(double maxHeight, double contentHeight) => Math.Min(maxHeight, Kind switch
	{
		SheetDetentKind.Medium => maxHeight / 2,
		SheetDetentKind.Large => maxHeight,
		SheetDetentKind.Fraction => maxHeight * Value,
		SheetDetentKind.Height => Value,
		_ => contentHeight,
	});

	/// <inheritdoc />
	public override string ToString() => Kind is SheetDetentKind.Fraction or SheetDetentKind.Height
		? $"{Kind}:{Value.ToString(CultureInfo.InvariantCulture)}"
		: Kind.ToString();

	/// <summary>Parses the format produced by <see cref="ToString"/>.</summary>
	/// <exception cref="FormatException">The text is not a valid detent.</exception>
	public static SheetDetent Parse(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);

		return parts[0].ToLowerInvariant() switch
		{
			"medium" => Medium,
			"large" => Large,
			"fitcontent" => FitContent,
			"fraction" => Fraction(Number()),
			"height" => Height(Number()),
			_ => throw Invalid(),
		};

		double Number() =>
			parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
				? value
				: throw Invalid();

		FormatException Invalid() =>
			new($"'{text}' is not a valid sheet detent. Use Medium, Large, FitContent, Fraction:<0.1-1.0> or Height:<number>.");
	}
}

/// <summary>Converts a string such as <c>Medium</c> or <c>Fraction:0.3</c> to a <see cref="SheetDetent"/>.</summary>
public sealed class SheetDetentTypeConverter : TypeConverter
{
	/// <inheritdoc />
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

	/// <inheritdoc />
	public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

	/// <inheritdoc />
	public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
		SheetDetent.Parse(value as string ?? throw new NotSupportedException($"Cannot convert {value.GetType()} to {nameof(SheetDetent)}."));

	/// <inheritdoc />
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
		value?.ToString();
}

/// <summary>Converts a comma separated string such as <c>FitContent, Large</c> to a list of <see cref="SheetDetent"/>.</summary>
public sealed class SheetDetentsTypeConverter : TypeConverter
{
	/// <inheritdoc />
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

	/// <inheritdoc />
	public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

	/// <inheritdoc />
	public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
		(value as string ?? throw new NotSupportedException($"Cannot convert {value.GetType()} to a list of {nameof(SheetDetent)}."))
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(SheetDetent.Parse)
			.ToArray();

	/// <inheritdoc />
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
		value is IEnumerable<SheetDetent> detents ? string.Join(", ", detents) : value?.ToString();
}

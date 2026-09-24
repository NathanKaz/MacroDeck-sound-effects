using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using SoundEffects.Audio;

namespace SoundEffects.Actions;

/// <summary>
/// Parameter shapes and result mapping shared by the sound actions.
/// </summary>
internal static class SoundActions
{
	public const string FileParameter = "file";
	public const string VolumeParameter = "volume";

	public static IReadOnlyList<ActionParameter> FileAndVolumeParameters() =>
	[
		ActionParameter.File(
			FileParameter,
			label: Strings.Parameters.File.Label(),
			description: Strings.Parameters.File.Description(),
			fileExtensions: AudioFormats.Extensions,
			required: true),
		ActionParameter.Slider(
			VolumeParameter,
			0,
			100,
			label: Strings.Parameters.Volume.Label(),
			description: Strings.Parameters.Volume.Description(),
			defaultValue: 100),
	];

	public static string? ReadFile(this IReadOnlyDictionary<string, object> parameters) =>
		parameters.TryGetValue(FileParameter, out var value) ? value as string : null;

	public static double ReadVolume(this IReadOnlyDictionary<string, object> parameters)
	{
		var raw = parameters.TryGetValue(VolumeParameter, out var value) ? value : 100.0;
		return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
	}

	public static ActionResult Map(SoundPlayOutcome outcome) => outcome switch
	{
		SoundPlayOutcome.Started => ActionResult.Success(),
		SoundPlayOutcome.FileNotFound => ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.FileNotFound()),
		SoundPlayOutcome.NoAudioDevice => ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.NoAudioDevice()),
		_ => ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.UnsupportedFormat()),
	};

	public static ActionResult FailedValidation() =>
		ActionResult.Failed(ActionErrorCodes.InvalidParameter, MacroDeckStrings.Validation.Required(Strings.Parameters.File.Label()));
}
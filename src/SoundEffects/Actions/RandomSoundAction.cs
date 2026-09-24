using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using SoundEffects.Audio;

namespace SoundEffects.Actions;

/// <summary>
/// Picks a random supported audio file from the chosen folder and plays it once, stopping any other
/// sound, like <see cref="PlaySoundAction"/>.
/// </summary>
public sealed class RandomSoundAction(SoundManager sound) : IActionDefinition
{
	private const string FolderParameter = "folder";

	public string Id => "random";

	public LocalizedText Name => Strings.Actions.Random.Name();

	public LocalizedText Description => Strings.Actions.Random.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Folder(
			FolderParameter,
			label: Strings.Parameters.Folder.Label(),
			description: Strings.Parameters.Folder.Description(),
			required: true),
		ActionParameter.Slider(
			SoundActions.VolumeParameter,
			0,
			100,
			label: Strings.Parameters.Volume.Label(),
			description: Strings.Parameters.Volume.Description(),
			defaultValue: 100),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.All;

	public IActionExecutor CreateExecutor() => new Executor(sound);

	private sealed class Executor(SoundManager sound) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var folder = context.Parameters.TryGetValue(FolderParameter, out var value) ? value as string : null;
			if (string.IsNullOrWhiteSpace(folder))
			{
				return Task.FromResult(ActionResult.Failed(
					ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Parameters.Folder.Label())));
			}

			string[] candidates;
			try
			{
				candidates = Directory.GetFiles(folder).Where(AudioFormats.IsSupported).ToArray();
			}
			catch (Exception)
			{
				candidates = [];
			}

			if (candidates.Length == 0)
			{
				return Task.FromResult(ActionResult.Failed(
					ActionErrorCodes.NotFound,
					Strings.Errors.FolderEmpty()));
			}

			var file = candidates[Random.Shared.Next(candidates.Length)];
			var volume = Convert.ToDouble(
				context.Parameters.TryGetValue(SoundActions.VolumeParameter, out var raw) ? raw : 100.0,
				CultureInfo.InvariantCulture);

			return Task.FromResult(SoundActions.Map(
				sound.StartSound(file, volume, overlap: false, loop: false)));
		}
	}
}
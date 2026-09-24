using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using SoundEffects.Audio;

namespace SoundEffects.Actions;

/// <summary>
/// Plays an audio file once. Any other sound the plugin is playing stops first; pressing the button
/// again replays the file from the start.
/// </summary>
public sealed class PlaySoundAction(SoundManager sound) : IActionDefinition
{
	public string Id => "play";

	public LocalizedText Name => Strings.Actions.Play.Name();

	public LocalizedText Description => Strings.Actions.Play.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = SoundActions.FileAndVolumeParameters();

	public MacroDeckPlatform Platforms => MacroDeckPlatform.All;

	public IActionExecutor CreateExecutor() => new Executor(sound);

	private sealed class Executor(SoundManager sound) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var file = context.Parameters.ReadFile();
			if (file is null)
			{
				return Task.FromResult(SoundActions.FailedValidation());
			}

			return Task.FromResult(SoundActions.Map(
				sound.StartSound(file, context.Parameters.ReadVolume(), overlap: false, loop: false)));
		}
	}
}
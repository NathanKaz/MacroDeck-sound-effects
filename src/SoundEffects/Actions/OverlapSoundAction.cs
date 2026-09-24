using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using SoundEffects.Audio;

namespace SoundEffects.Actions;

/// <summary>
/// Plays an audio file without stopping anything else, so several sounds can play on top of each
/// other. It plays once.
/// </summary>
public sealed class OverlapSoundAction(SoundManager sound) : IActionDefinition
{
	public string Id => "overlap";

	public LocalizedText Name => Strings.Actions.Overlap.Name();

	public LocalizedText Description => Strings.Actions.Overlap.Description();

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
				sound.StartSound(file, context.Parameters.ReadVolume(), overlap: true, loop: false)));
		}
	}
}
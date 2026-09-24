using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using SoundEffects.Audio;

namespace SoundEffects.Actions;

/// <summary>
/// Plays an audio file on repeat. Pressing the same button again stops the loop; a sound started by
/// another action also stops it, because it plays non-overlapping.
/// </summary>
public sealed class LoopSoundAction(SoundManager sound) : IActionDefinition
{
	public string Id => "loop";

	public LocalizedText Name => Strings.Actions.Loop.Name();

	public LocalizedText Description => Strings.Actions.Loop.Description();

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

			if (sound.IsLooping(file))
			{
				sound.StopSound(file);
				return ActionResult.SucceededTask;
			}

			return Task.FromResult(SoundActions.Map(
				sound.StartSound(file, context.Parameters.ReadVolume(), overlap: false, loop: true)));
		}
	}
}
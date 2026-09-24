using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using SoundEffects.Audio;

namespace SoundEffects.Actions;

/// <summary>
/// Plays an audio file; pressing the same button again stops it. Everything else about playback is
/// like <see cref="PlaySoundAction"/>: it stops other sounds and plays once.
/// </summary>
public sealed class PlayStopSoundAction(SoundManager sound) : IActionDefinition
{
	public string Id => "play-stop";

	public LocalizedText Name => Strings.Actions.PlayStop.Name();

	public LocalizedText Description => Strings.Actions.PlayStop.Description();

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

			if (sound.IsPlaying(file))
			{
				sound.StopSound(file);
				return ActionResult.SucceededTask;
			}

			return Task.FromResult(SoundActions.Map(
				sound.StartSound(file, context.Parameters.ReadVolume(), overlap: false, loop: false)));
		}
	}
}
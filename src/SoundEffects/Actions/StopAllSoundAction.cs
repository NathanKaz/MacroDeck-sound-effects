using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using SoundEffects.Audio;

namespace SoundEffects.Actions;

/// <summary>
/// Stops every sound the plugin is currently playing.
/// </summary>
public sealed class StopAllSoundAction(SoundManager sound) : IActionDefinition
{
	public string Id => "stop-all";

	public LocalizedText Name => Strings.Actions.StopAll.Name();

	public LocalizedText Description => Strings.Actions.StopAll.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.All;

	public IActionExecutor CreateExecutor() => new Executor(sound);

	private sealed class Executor(SoundManager sound) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			sound.StopAll();
			return ActionResult.SucceededTask;
		}
	}
}
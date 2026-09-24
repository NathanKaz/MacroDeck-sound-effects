using MacroDeck.Plugin.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SoundEffects.Audio;

namespace SoundEffects.Tests;

/// <summary>
/// Behaviour tests through <see cref="PluginTestHarness"/>: the plugin's own capability handlers run,
/// but nothing crosses a socket. This is where you test what your integration does.
/// </summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	private const string MissingFile = "/does/not/exist/sound.wav";

	private static PluginTestHarness CreateHarness() =>
		PluginTestHarness.Create(builder => builder
			.ConfigureServices((_, services) => services.AddSingleton<SoundManager>())
			.UseLocalization(Strings.LocalizationCatalog)
			.RegisterIntegration<PluginIntegration>());

	[Test]
	public async Task The_plugin_builds_and_initializes()
	{
		await using var harness = CreateHarness();

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}

	[Test]
	public async Task Stop_all_succeeds_when_nothing_is_playing()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync("stop-all", new Dictionary<string, object?>());

		Assert.That(outcome.Succeeded, Is.True);
	}

	[Test]
	public async Task Play_fails_when_the_file_is_missing()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"play",
			new Dictionary<string, object?> { ["file"] = MissingFile, ["volume"] = 100.0 });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Play_fails_when_the_file_is_blank()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"play",
			new Dictionary<string, object?> { ["file"] = "   ", ["volume"] = 100.0 });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Random_fails_when_the_folder_is_missing()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"random",
			new Dictionary<string, object?> { ["folder"] = "/does/not/exist", ["volume"] = 100.0 });

		Assert.That(outcome.Succeeded, Is.False);
	}

	/// <summary>
	/// Proves first-play engine initialization and playback on a machine with an audio device. Run with
	/// <c>dotnet test --filter FullyQualifiedName~Playback</c>; excluded from normal runs on purpose.
	/// </summary>
	[Test]
	[Explicit("Requires an audio output device.")]
	public async Task Play_with_a_generated_wave_starts_playback()
	{
		var wave = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "od-sound-check.wav");
		await System.IO.File.WriteAllBytesAsync(wave, MakeToneWav(44100, 220, 0.25));
		try
		{
			await using var harness = CreateHarness();
			await harness.InitializeIntegrationsAsync();

			var outcome = await harness.Actions.ExecuteAsync(
				"play",
				new Dictionary<string, object?> { ["file"] = wave, ["volume"] = 20.0 });

			Assert.That(outcome.Succeeded, Is.True);

			var stopped = await harness.Actions.ExecuteAsync("stop-all", new Dictionary<string, object?>());
			Assert.That(stopped.Succeeded, Is.True);
		}
		finally
		{
			System.IO.File.Delete(wave);
		}
	}

	private static byte[] MakeToneWav(int sampleRate, double frequency, double seconds)
	{
		var sampleCount = (int)(sampleRate * seconds);
		using var buffer = new MemoryStream();
		using var writer = new BinaryWriter(buffer);
		writer.Write("RIFF"u8.ToArray());
		writer.Write(36 + sampleCount * 2);
		writer.Write("WAVE"u8.ToArray());
		writer.Write("fmt "u8.ToArray());
		writer.Write(16);
		writer.Write((short)1);
		writer.Write((short)1);
		writer.Write(sampleRate);
		writer.Write(sampleRate * 2);
		writer.Write((short)2);
		writer.Write((short)16);
		writer.Write("data"u8.ToArray());
		writer.Write(sampleCount * 2);
		for (var i = 0; i < sampleCount; i++)
		{
			var sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * 0.5);
			writer.Write(sample);
		}

		writer.Flush();
		return buffer.ToArray();
	}
}

/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.opencode.sound-effects"));
	}

	[Test]
	public void English_is_the_default_culture_and_russian_is_registered()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("ru"));
	}

	[Test]
	public void The_action_strings_come_from_the_catalog()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Actions.Play.Name"));
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Actions.Random.Description"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}

	[Test]
	public void Every_russian_key_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("ru"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("ru", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}
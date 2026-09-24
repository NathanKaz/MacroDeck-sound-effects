namespace SoundEffects.Actions;

/// <summary>
/// The file extensions the plugin can play, kept in one place so the file picker filter and the
/// random-folder scan agree with each other.
/// </summary>
internal static class AudioFormats
{
	public static IReadOnlyList<string> Extensions { get; } =
		["wav", "mp3", "flac", "ogg", "oga", "opus", "m4a", "aac", "aif", "aiff", "wma"];

	private static readonly HashSet<string> Supported = new(Extensions, StringComparer.OrdinalIgnoreCase);

	public static bool IsSupported(string path) =>
		Supported.Contains(Path.GetExtension(path).TrimStart('.'));
}
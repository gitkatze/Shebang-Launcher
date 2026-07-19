using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

return await ShebangLauncher.RunAsync(args);

internal static class ShebangLauncher
{
    private const int ErrorExitCode = 1;
    private const string DefaultEditor = "notepad.exe";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("使い方: Shebang <ファイルパス> [引数...]");
        }

        var target = args[0];
        if (!File.Exists(target))
        {
            return Fail($"ファイルが見つかりません: {target}");
        }

        string? shebang;
        try
        {
            shebang = await ReadShebangAsync(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Fail($"ファイルを読み込めません: {target}\n{ex.Message}");
        }

        if (shebang is null)
        {
            var editor = ReadEditorCommand();
            return await StartAndWaitAsync(editor, args, "設定されたエディタ");
        }

        var commandLine = ParseCommandLine(shebang);
        if (commandLine.Count == 0)
        {
            return Fail("Shebang に実行プログラムが指定されていません。");
        }

        var commandIndex = 0;
        if (string.Equals(Path.GetFileName(commandLine[0]), "env", StringComparison.OrdinalIgnoreCase))
        {
            commandIndex = 1;
            if (commandIndex < commandLine.Count && commandLine[commandIndex] == "-S")
            {
                commandIndex++;
            }

            if (commandIndex >= commandLine.Count)
            {
                return Fail("Shebang の env に実行プログラムが指定されていません。");
            }
        }

        var command = Path.GetFileName(commandLine[commandIndex].Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(command))
        {
            return Fail("Shebang の実行プログラムが不正です。");
        }

        var executable = FindExecutable(command);
        if (executable is null)
        {
            return Fail($"実行プログラムが見つかりません: {command}");
        }

        var childArguments = commandLine.Skip(commandIndex + 1).Concat(args).ToArray();
        return await StartAndWaitAsync(executable, childArguments, command);
    }

    private static async Task<string?> ReadShebangAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var firstLine = await reader.ReadLineAsync();
        return firstLine is not null && firstLine.StartsWith("#!", StringComparison.Ordinal)
            ? firstLine[2..].Trim()
            : null;
    }

    private static string ReadEditorCommand()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SheBang",
            "config.json");

        try
        {
            if (File.Exists(configPath))
            {
                var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(configPath));
                if (!string.IsNullOrWhiteSpace(config?.Editor))
                {
                    return config.Editor.Trim();
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                var defaultConfig = new LauncherConfig { Editor = DefaultEditor };
                var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
                File.WriteAllText(configPath, json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"設定ファイルを解析できないため、{DefaultEditor}を使用します: {configPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"設定ファイルを作成または読み込めないため、{DefaultEditor}を使用します: {configPath}");
        }

        return DefaultEditor;
    }

    private static string? FindExecutable(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var basePath = Path.Combine(directory.Trim().Trim('"'), command);
            foreach (var extension in extensions)
            {
                var candidate = basePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                    ? basePath
                    : basePath + extension;
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static async Task<int> StartAndWaitAsync(string executable, IEnumerable<string> arguments, string description)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = string.Join(' ', arguments.Select(QuoteArgument)),
                UseShellExecute = false,
            });

            if (process is null)
            {
                return Fail($"{description}を起動できません: {executable}");
            }

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or FileNotFoundException)
        {
            return Fail($"{description}を起動できません: {ex.Message}");
        }
    }

    private static string QuoteArgument(string value)
    {
        if (value.Length > 0 && value.All(c => !char.IsWhiteSpace(c) && c != '"'))
        {
            return value;
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static List<string> ParseCommandLine(string value)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        foreach (var c in value)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        return result;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        if (!Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Enterキーを押すと終了します。");
            Console.ReadKey(intercept: true);
        }
        return ErrorExitCode;
    }

    private sealed class LauncherConfig
    {
        public string? Editor { get; set; }
    }
}

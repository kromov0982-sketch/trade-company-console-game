using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradeCompany;

public static class Updater
{
    private const string LatestRelease = "https://api.github.com/repos/kromov0982-sketch/trade-company-console-game/releases/latest";
    private const string PackageName = "TradeCompany-Windows-x64.zip";
    private const string ChecksumName = PackageName + ".sha256";
    public static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static bool CheckAndInstall()
    {
        ConsoleWindow.ClearViewport();
        Ink.Line("ПРОВЕРКА ОБНОВЛЕНИЙ", Ink.Heading);
        Ink.Line($"Установлена версия {CurrentVersion}. Связь с GitHub…", Ink.Muted);
        try
        {
            if (!string.Equals(Path.GetFileName(Environment.ProcessPath), "TradeCompany.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Обновление доступно только в автономной версии TradeCompany.exe.");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TradeCompany", CurrentVersion));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = client.GetAsync(LatestRelease).GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException("Публичные версии пока не опубликованы.");
            response.EnsureSuccessStatusCode();
            var release = JsonSerializer.Deserialize<Release>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("GitHub вернул пустой ответ.");
            if (!TryVersion(release.TagName, out var latest)) throw new InvalidOperationException("У опубликованной версии некорректный номер.");
            if (latest <= Version.Parse(CurrentVersion))
            {
                Ink.Line($"У вас уже последняя версия: {latest}.", Ink.Success);
                WaitForMenu();
                return false;
            }
            var package = release.Assets.SingleOrDefault(a => a.Name == PackageName);
            var checksum = release.Assets.SingleOrDefault(a => a.Name == ChecksumName);
            if (package is null || checksum is null) throw new InvalidOperationException("В релизе нет архива или контрольной суммы обновления.");
            string staging = Path.Combine(Path.GetTempPath(), "TradeCompany-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            string zip = Path.Combine(staging, PackageName);
            Ink.Line($"Найдена версия {latest}. Загрузка…", Ink.Parameter);
            File.WriteAllBytes(zip, client.GetByteArrayAsync(package.BrowserDownloadUrl).GetAwaiter().GetResult());
            string[] checksumParts = client.GetStringAsync(checksum.BrowserDownloadUrl).GetAwaiter().GetResult().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (checksumParts.Length == 0 || checksumParts[0].Length != 64) throw new InvalidOperationException("Файл контрольной суммы повреждён.");
            string expected = checksumParts[0];
            string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Контрольная сумма архива не совпала. Установка отменена.");
            string files = Path.Combine(staging, "files");
            ZipFile.ExtractToDirectory(zip, files);
            string helper = Path.Combine(files, "TradeCompany.exe");
            if (!File.Exists(helper)) throw new InvalidOperationException("В архиве обновления нет TradeCompany.exe.");
            var start = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = files };
            start.ArgumentList.Add("--apply-update");
            start.ArgumentList.Add(files);
            start.ArgumentList.Add(AppPaths.Root);
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(start);
            Ink.Line("Обновление проверено. Игра перезапустится автоматически.", Ink.Success);
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or JsonException or CryptographicException or UnauthorizedAccessException or OperationCanceledException)
        {
            Logger.Error("Не удалось обновить игру.", e);
            Ink.Line("Не удалось обновить игру: " + e.Message, Ink.Error);
            WaitForMenu();
            return false;
        }
    }

    public static void Apply(string files, string target, int oldProcessId)
    {
        try { Process.GetProcessById(oldProcessId).WaitForExit(30_000); } catch (ArgumentException) { }
        Directory.CreateDirectory(target);
        foreach (string name in new[] { "TradeCompany.exe", "КАК ЗАПУСТИТЬ.txt" })
        {
            string source = Path.Combine(files, name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(target, name), true);
        }
        Process.Start(new ProcessStartInfo(Path.Combine(target, "TradeCompany.exe")) { UseShellExecute = true, WorkingDirectory = target });
    }

    internal static bool TryVersion(string tag, out Version version) => Version.TryParse(tag.Trim().TrimStart('v', 'V'), out version!);
    private static void WaitForMenu()
    {
        Ink.Line("Нажмите любую клавишу, чтобы вернуться в меню.", Ink.Muted);
        Console.ReadKey(true);
    }
    private sealed class Release
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";
        public List<Asset> Assets { get; set; } = [];
    }
    private sealed class Asset
    {
        public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}

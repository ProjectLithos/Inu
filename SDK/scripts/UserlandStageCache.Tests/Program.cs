// Explicit host-side regression test; never included in generated OS startup.
string root = Path.Combine(Path.GetTempPath(), "InuCacheTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string input = Path.Combine(root, "input.cs"), output = Path.Combine(root, "output.obj");
    string cache = Path.Combine(root, "missing", "StageCache", "Userland");
    File.WriteAllText(input, "v1");
    int builds = 0;
    int Run(bool force = false, int exitCode = 0) => UserlandStageCache.Run(
        "assembly", cache, [input], [output], ["win64"], force, () =>
        {
            builds++;
            if (exitCode == 0) File.WriteAllText(output, "built:" + File.ReadAllText(input));
            return exitCode;
        });
    void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    Check(!Directory.Exists(cache), "Fixture must start without a cache directory.");
    Check(Run() == 0 && builds == 1, "First run must execute and create cache.");
    Check(Directory.GetFiles(cache, "*.json").Length == 1, "First run must publish its record.");
    Check(Run() == 0 && builds == 1, "Unchanged inputs/outputs must hit cache.");
    File.WriteAllText(output, "damaged");
    Check(Run() == 0 && builds == 2, "Damaged output must rebuild.");
    Check(Run(force: true, exitCode: 7) == 7 && builds == 3, "Failed action must return its exit code.");
    Check(Directory.GetFiles(cache, "*.json").Length == 0, "Failure must invalidate old record.");
    Check(Run() == 0 && builds == 4, "Retry must execute.");
    Directory.Delete(Path.Combine(root, "missing"), recursive: true);
    Check(Run() == 0 && builds == 5, "Cleared cache directory must be recreated.");
    File.WriteAllText(Directory.GetFiles(cache, "*.json").Single(), "{damaged");
    Check(Run() == 0 && builds == 6, "Corrupt record must rebuild.");
    File.WriteAllText(input, "v2");
    Check(Run() == 0 && builds == 7, "Changed source must rebuild.");
    int changed = UserlandStageCache.Run("assembly", cache, [input], [output], ["win64"], true, () =>
    {
        File.WriteAllText(input, "v3"); File.WriteAllText(output, "v3"); return 0;
    });
    Check(changed == 0 && Directory.GetFiles(cache, "*.json").Length == 0, "Input race must not publish a record.");
    Console.WriteLine("Userland cache first-run, hit, corruption, failure/retry, cleared directory and input-race tests passed.");
}
finally { Directory.Delete(root, recursive: true); }

using System.Diagnostics;

namespace MyGraphRagV5.Tests.Integration;

/// <summary>
/// A <see cref="FactAttribute"/> that skips the test instead of failing when Docker isn't
/// reachable. The check runs once (cached) at attribute construction time, i.e. during test
/// discovery - before the collection fixture (which would otherwise try to start containers)
/// is ever instantiated, so a Docker-less environment gets clean skips, not hard failures.
/// </summary>
public sealed class DockerAvailableFactAttribute : FactAttribute
{
    public DockerAvailableFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            this.Skip = "Docker is not available in this environment.";
        }
    }
}

internal static class DockerAvailability
{
    public static readonly bool IsAvailable = Check();

    private static bool Check()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "version --format {{.Server.Version}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                return false;
            }

            return process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

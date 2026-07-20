using System.Diagnostics;

namespace MyGraphRagV5.Tests.Integration;

/// <summary>
/// Checks Docker availability once (cached at first access). Integration test classes use a
/// <c>[Before(Test)]</c> hook that calls <see cref="TUnit.Core.Skip.Test(string)"/> when this is
/// false, instead of the xUnit-era custom <c>FactAttribute</c> that used to short-circuit
/// discovery before the collection fixture could try to start containers.
/// </summary>
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

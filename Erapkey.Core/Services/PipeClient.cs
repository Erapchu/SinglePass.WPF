using System.Collections.Generic;
using System.Linq;

namespace Erapkey.Core.Services
{
    /// <summary>
    /// The program connected to a pipe. <see cref="Chain"/> starts with the program itself and goes up
    /// through the programs that started it, for example <c>ssh.exe</c>, <c>git.exe</c>, <c>Code.exe</c>.
    /// </summary>
    public sealed record PipeClient(int ProcessId, IReadOnlyList<PipeClientProcess> Chain)
    {
        public static PipeClient Unknown { get; } = new(0, []);

        public string DisplayName => Chain.Count == 0 ? "?" : string.Join(" ← ", Chain.Select(p => p.Name));
    }

    public sealed record PipeClientProcess(int Id, string Name, string Path);
}

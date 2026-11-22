using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.Core.Services
{
    /// <summary>
    /// Serves <see cref="SshAgent"/> on a named pipe, the way OpenSSH for Windows talks to an agent.
    /// The pipe has its own name, so it does not conflict with the Windows ssh-agent service; clients
    /// find it through <c>IdentityAgent</c> or <c>SSH_AUTH_SOCK</c>. Only the current user can connect,
    /// and not over the network.
    /// </summary>
    public sealed class SshAgentServer : IDisposable
    {
        private readonly SshAgent _agent;
        private readonly ILogger<SshAgentServer> _logger;
        private readonly object _sync = new();
        private CancellationTokenSource _stop;
        private Task _listening;

        /// <summary>Pipe name, unique per Windows user: pipe names are shared by all sessions of the machine.</summary>
        public string PipeName { get; }

        /// <summary>The path for <c>SSH_AUTH_SOCK</c>: <c>\\.\pipe\name</c>.</summary>
        public string PipePath => @"\\.\pipe\" + PipeName;

        /// <summary>
        /// The path for the <c>IdentityAgent</c> option of ssh: <c>//./pipe/name</c>. The ssh option parser
        /// treats backslashes as escapes, so <see cref="PipePath"/> does not work there; Windows accepts both.
        /// </summary>
        public string IdentityAgentPath => "//./pipe/" + PipeName;

        public bool IsRunning
        {
            get { lock (_sync) return _stop != null; }
        }

        public SshAgentServer(SshAgent agent, ILogger<SshAgentServer> logger)
            : this(agent, logger, "singlepass-ssh-agent-" + WindowsIdentity.GetCurrent().User!.Value)
        {
        }

        internal SshAgentServer(SshAgent agent, ILogger<SshAgentServer> logger, string pipeName)
        {
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _logger = logger;
            PipeName = pipeName;
        }

        /// <summary>
        /// Starts listening. Throws <see cref="IOException"/> when the pipe already exists: another
        /// program took the name, and its clients must not be served by mistake (or by it).
        /// </summary>
        public void Start()
        {
            lock (_sync)
            {
                if (_stop != null)
                    return;

                NamedPipeServerStream first;
                try
                {
                    first = CreatePipe(firstInstance: true);
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Windows reports a pipe that already exists as access denied.
                    throw new IOException($"The pipe {PipePath} is already in use by another program.", ex);
                }

                _stop = new CancellationTokenSource();
                _listening = ListenAsync(first, _stop.Token);
            }
            _logger?.LogInformation("SSH agent listens on {Pipe}", PipePath);
        }

        public void Stop()
        {
            CancellationTokenSource stop;
            Task listening;
            lock (_sync)
            {
                stop = _stop;
                listening = _listening;
                _stop = null;
                _listening = null;
            }

            if (stop == null)
                return;

            stop.Cancel();
            // Let the listening pipe close, so that Start can create it again right away.
            try
            {
                listening?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }
            stop.Dispose();
            _logger?.LogInformation("SSH agent stopped");
        }

        public void Dispose() => Stop();

        private async Task ListenAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    return;
                }
                catch (IOException ex)
                {
                    _logger?.LogWarning(ex, "SSH agent connection failed");
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    pipe = CreatePipeOrNull();
                    if (pipe == null)
                        return;
                    continue;
                }

                var connected = pipe;
                _ = Task.Run(() => ServeAsync(connected, cancellationToken), CancellationToken.None);

                pipe = CreatePipeOrNull();
                if (pipe == null)
                    return;
            }

            await pipe.DisposeAsync().ConfigureAwait(false);
        }

        private NamedPipeServerStream CreatePipeOrNull()
        {
            try
            {
                return CreatePipe(firstInstance: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger?.LogError(ex, "SSH agent cannot accept more connections");
                return null;
            }
        }

        private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            await using (pipe)
            {
                var client = GetClient(pipe);
                try
                {
                    await _agent.ServeAsync(pipe, client, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
                {
                    // The client went away.
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "SSH agent request from {Client} failed", client.DisplayName);
                }
            }
        }

        private NamedPipeServerStream CreatePipe(bool firstInstance)
        {
            var user = WindowsIdentity.GetCurrent().User!;
            var security = new PipeSecurity();
            security.SetOwner(user);
            security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
                PipeAccessRights.FullControl, AccessControlType.Deny));

            var options = PipeOptions.Asynchronous | (firstInstance ? PipeOptions.FirstPipeInstance : PipeOptions.None);
            return NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, options, 0, 0, security);
        }

        /// <summary>The connected program and the programs that started it, for the confirmation prompt.</summary>
        private static SshAgentClient GetClient(NamedPipeServerStream pipe)
        {
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId))
                return SshAgentClient.Unknown;

            var parents = GetParentProcessIds();
            var chain = new List<SshAgentClientProcess>();
            var id = (int)processId;
            var childStart = DateTime.MaxValue;
            while (chain.Count < 4 && id > 4)
            {
                // A parent that exited may have left its id to a newer, unrelated process.
                var process = DescribeProcess(id, out var start);
                if (process == null || start > childStart)
                    break;

                chain.Add(process);
                childStart = start ?? DateTime.MinValue;
                if (!parents.TryGetValue(id, out id))
                    break;
            }
            return new SshAgentClient((int)processId, chain);
        }

        private static SshAgentClientProcess DescribeProcess(int id, out DateTime? start)
        {
            start = null;
            try
            {
                using var process = Process.GetProcessById(id);
                start = TryGet(() => process.StartTime);
                string path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Elevated or protected process: the name is all there is.
                }
                return new SshAgentClientProcess(id, process.ProcessName + ".exe", path);
            }
            catch (ArgumentException)
            {
                return null;
            }

            static T? TryGet<T>(Func<T> get) where T : struct
            {
                try { return get(); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { return null; }
            }
        }

        private static Dictionary<int, int> GetParentProcessIds()
        {
            var parents = new Dictionary<int, int>();
            var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
            if (snapshot == InvalidHandle)
                return parents;

            try
            {
                var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
                if (!Process32First(snapshot, ref entry))
                    return parents;

                do
                    parents[(int)entry.ProcessId] = (int)entry.ParentProcessId;
                while (Process32Next(snapshot, ref entry));
            }
            finally
            {
                CloseHandle(snapshot);
            }
            return parents;
        }

        private const uint SnapshotProcesses = 0x00000002;
        private static readonly IntPtr InvalidHandle = new(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint clientProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}

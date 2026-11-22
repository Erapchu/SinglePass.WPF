using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.Core.Services
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

    /// <summary>
    /// A named pipe for programs of the current user on this machine: the ACL allows the current user and denies
    /// network logons, and the first instance is created with <c>FirstPipeInstance</c>, so a program that took
    /// the name first is not shared with. Each connection is served by <see cref="ServeAsync"/> on its own task.
    /// </summary>
    public abstract class NamedPipeServer : IDisposable
    {
        private readonly object _sync = new();
        private CancellationTokenSource _stop;
        private Task _listening;

        protected ILogger Logger { get; }

        /// <summary>Pipe name, unique per Windows user: pipe names are shared by all sessions of the machine.</summary>
        public string PipeName { get; }

        /// <summary><c>\\.\pipe\name</c>.</summary>
        public string PipePath => @"\\.\pipe\" + PipeName;

        /// <summary>What the pipe is for, in log messages: "SSH agent", "Browser server".</summary>
        protected abstract string DisplayName { get; }

        public bool IsRunning
        {
            get { lock (_sync) return _stop != null; }
        }

        protected NamedPipeServer(string pipeName, ILogger logger)
        {
            PipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
            Logger = logger;
        }

        /// <summary><paramref name="prefix"/>, the SID of the current user and <see cref="Helpers.Constants.InstanceSuffix"/>.</summary>
        protected static string UserPipeName(string prefix) =>
            prefix + WindowsIdentity.GetCurrent().User!.Value + Helpers.Constants.InstanceSuffix;

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
            Logger?.LogInformation("{Server} listens on {Pipe}", DisplayName, PipePath);
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
            OnStopped();
            Logger?.LogInformation("{Server} stopped", DisplayName);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        /// <summary>Serves one connection until the client goes away or <paramref name="cancellationToken"/> stops the server.</summary>
        protected abstract Task ServeAsync(Stream pipe, PipeClient client, CancellationToken cancellationToken);

        /// <summary>Called after <see cref="Stop"/>, when no new connections come.</summary>
        protected virtual void OnStopped() { }

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
                    Logger?.LogWarning(ex, "{Server} connection failed", DisplayName);
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    pipe = CreatePipeOrNull();
                    if (pipe == null)
                        return;
                    continue;
                }

                var connected = pipe;
                _ = Task.Run(() => ServeConnectionAsync(connected, cancellationToken), CancellationToken.None);

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
                Logger?.LogError(ex, "{Server} cannot accept more connections", DisplayName);
                return null;
            }
        }

        private async Task ServeConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            await using (pipe)
            {
                var client = GetClient(pipe);
                try
                {
                    await ServeAsync(pipe, client, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or EndOfStreamException)
                {
                    // The client went away.
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "{Server} request from {Client} failed", DisplayName, client.DisplayName);
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

        /// <summary>The connected program and the programs that started it.</summary>
        private static PipeClient GetClient(NamedPipeServerStream pipe)
        {
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId))
                return PipeClient.Unknown;

            var parents = GetParentProcessIds();
            var chain = new List<PipeClientProcess>();
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
            return new PipeClient((int)processId, chain);
        }

        private static PipeClientProcess DescribeProcess(int id, out DateTime? start)
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
                return new PipeClientProcess(id, process.ProcessName + ".exe", path);
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

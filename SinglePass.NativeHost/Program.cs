using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.NativeHost
{
    /// <summary>
    /// Relays Native Messaging (stdin/stdout, 32-bit little-endian length + UTF-8 JSON) to the pipe of SinglePass
    /// (32-bit big-endian length + the same JSON) and back. The messages are not read: after <c>hello</c> they are
    /// encrypted for SinglePass. The browser passes arguments of its own (the extension origin, the parent window);
    /// they are ignored. See docs/browser-extension.md.
    /// </summary>
    internal static class Program
    {
        private const int MaxMessageSize = 1024 * 1024;
        // A running SinglePass always has a free pipe instance waiting, so a short wait is enough.
        private const int ConnectTimeoutMilliseconds = 1000;

#if DEBUG
        private const string InstanceSuffix = "-dev";
#else
        private const string InstanceSuffix = "";
#endif

        private static async Task<int> Main()
        {
            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();

            var pipeName = "singlepass-browser-" + WindowsIdentity.GetCurrent().User!.Value + InstanceSuffix;
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(ConnectTimeoutMilliseconds).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                // SinglePass is not running or the integration is off. The extension connects again on the next user action.
                await WriteNativeAsync(stdout, Encoding.UTF8.GetBytes("{\"event\":\"app_not_running\"}"), CancellationToken.None).ConfigureAwait(false);
                return 1;
            }

            using var stop = new CancellationTokenSource();
            var toApp = RelayAsync(() => ReadNativeAsync(stdin, stop.Token), payload => WritePipeAsync(pipe, payload, stop.Token));
            var toBrowser = RelayAsync(() => ReadPipeAsync(pipe, stop.Token), payload => WriteNativeAsync(stdout, payload, stop.Token));

            // Either side closing ends the host: the browser closes stdin when the extension disconnects,
            // SinglePass closes the pipe when it exits or the integration is turned off.
            await Task.WhenAny(toApp, toBrowser).ConfigureAwait(false);
            stop.Cancel();
            return 0;
        }

        private static async Task RelayAsync(Func<Task<byte[]?>> read, Func<byte[], Task> write)
        {
            try
            {
                while (await read().ConfigureAwait(false) is { } payload)
                    await write(payload).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or InvalidDataException)
            {
            }
        }

        private static Task<byte[]?> ReadNativeAsync(Stream stdin, CancellationToken cancellationToken) =>
            ReadFrameAsync(stdin, bigEndian: false, cancellationToken);

        private static Task<byte[]?> ReadPipeAsync(Stream pipe, CancellationToken cancellationToken) =>
            ReadFrameAsync(pipe, bigEndian: true, cancellationToken);

        private static Task WriteNativeAsync(Stream stdout, byte[] payload, CancellationToken cancellationToken) =>
            WriteFrameAsync(stdout, payload, bigEndian: false, cancellationToken);

        private static Task WritePipeAsync(Stream pipe, byte[] payload, CancellationToken cancellationToken) =>
            WriteFrameAsync(pipe, payload, bigEndian: true, cancellationToken);

        /// <summary>Null at the end of the stream; <see cref="InvalidDataException"/> for an empty or too large frame.</summary>
        private static async Task<byte[]?> ReadFrameAsync(Stream stream, bool bigEndian, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            try
            {
                await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                return null;
            }

            var length = bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(header) : BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (length == 0 || length > MaxMessageSize)
                throw new InvalidDataException($"A message of {length} bytes.");

            var payload = new byte[length];
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            return payload;
        }

        private static async Task WriteFrameAsync(Stream stream, byte[] payload, bool bigEndian, CancellationToken cancellationToken)
        {
            var frame = new byte[4 + payload.Length];
            if (bigEndian)
                BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)payload.Length);
            payload.CopyTo(frame, 4);
            await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

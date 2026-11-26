using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Services
{
    /// <summary>
    /// Serves <see cref="SshAgent"/> on a named pipe, the way OpenSSH for Windows talks to an agent.
    /// The pipe has its own name, so it does not conflict with the Windows ssh-agent service; clients
    /// find it through <c>IdentityAgent</c> or <c>SSH_AUTH_SOCK</c>. Only the current user can connect,
    /// and not over the network.
    /// </summary>
    public sealed class SshAgentServer : NamedPipeServer
    {
        private readonly SshAgent _agent;

        /// <summary>
        /// The path for the <c>IdentityAgent</c> option of ssh: <c>//./pipe/name</c>. The ssh option parser
        /// treats backslashes as escapes, so <see cref="NamedPipeServer.PipePath"/> does not work there; Windows accepts both.
        /// </summary>
        public string IdentityAgentPath => "//./pipe/" + PipeName;

        protected override string DisplayName => "SSH agent";

        public SshAgentServer(SshAgent agent, ILogger<SshAgentServer> logger)
            : this(agent, logger, UserPipeName("erapkey-ssh-agent-"))
        {
        }

        internal SshAgentServer(SshAgent agent, ILogger<SshAgentServer> logger, string pipeName)
            : base(pipeName, logger)
        {
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        protected override Task ServeAsync(Stream pipe, PipeClient client, CancellationToken cancellationToken) =>
            _agent.ServeAsync(pipe, client, cancellationToken);
    }
}

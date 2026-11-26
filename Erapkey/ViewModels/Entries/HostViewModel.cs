using Erapkey.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Erapkey.ViewModels.Entries
{
    /// <summary>An SSH server: address, user and the SSH key entry to sign in with.</summary>
    public sealed class HostViewModel : CredentialDataViewModel
    {
        public HostData Model { get; }

        public HostViewModel(HostData model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public string Hostname
        {
            get => Model.Hostname;
            set
            {
                if (Set(Model.Hostname, value?.Trim(), v => Model.Hostname = v))
                    OnPropertiesChanged(nameof(HasHostname), nameof(Destination), nameof(Subtitle), nameof(SubtitleToolTip));
            }
        }

        public string Username
        {
            get => Model.Username;
            set
            {
                if (Set(Model.Username, value?.Trim(), v => Model.Username = v))
                    OnPropertiesChanged(nameof(HasUsername), nameof(Destination), nameof(Subtitle), nameof(SubtitleToolTip));
            }
        }

        /// <summary>
        /// The port as typed in the editor. Empty means the default port; text that is not a valid port
        /// keeps the stored port and shows <see cref="IsPortInvalid"/>.
        /// </summary>
        public string PortText
        {
            get => _portText ?? Model.Port?.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (_portText == value)
                    return;

                _portText = value;
                var text = value?.Trim();
                if (string.IsNullOrEmpty(text))
                    Model.Port = null;
                else if (TryParsePort(text, out var port))
                    Model.Port = port == HostData.DefaultPort ? null : port;

                OnPropertiesChanged(nameof(PortText), nameof(Port), nameof(IsPortInvalid), nameof(Subtitle), nameof(SubtitleToolTip));
            }
        }

        private string _portText;

        public int Port => Model.Port ?? HostData.DefaultPort;

        public bool IsPortInvalid => !string.IsNullOrWhiteSpace(_portText) && !TryParsePort(_portText.Trim(), out _);

        public Guid? SshKeyId
        {
            get => Model.SshKeyId;
            set
            {
                if (Model.SshKeyId == value)
                    return;

                Model.SshKeyId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSshKey));
            }
        }

        public bool HasHostname => !string.IsNullOrWhiteSpace(Hostname);
        public bool HasUsername => !string.IsNullOrWhiteSpace(Username);
        public bool HasSshKey => SshKeyId.HasValue;

        /// <summary><c>user@host</c>, or just the host when there is no user.</summary>
        public string Destination => HasUsername ? $"{Username}@{Hostname}" : Hostname;

        public override string Subtitle => !HasHostname ? null
            : Port == HostData.DefaultPort ? Destination
            : $"{Destination}:{Port}";

        public override string Glyph => "";
        public override IEnumerable<string> SearchableTexts => [Hostname, Username];

        private static bool TryParsePort(string text, out int port) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= 65535;

        private bool Set(string current, string value, Action<string> assign, [CallerMemberName] string propertyName = null)
        {
            if (string.IsNullOrEmpty(value))
                value = null;
            if (current == value)
                return false;

            assign(value);
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertiesChanged(params string[] names)
        {
            foreach (var name in names)
                OnPropertyChanged(name);
        }
    }
}

using Erapkey.Core.Models;
using Erapkey.Core.Services;
using System;
using System.Collections.Generic;

namespace Erapkey.Desktop.ViewModels.Entries
{
    public sealed class SshKeyViewModel : CredentialDataViewModel
    {
        public SshKeyData Model { get; }

        public SshKeyViewModel(SshKeyData model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public string PrivateKey
        {
            get => Model.PrivateKey;
            set
            {
                if (Model.PrivateKey == value)
                    return;

                // Keep the public key in step with the private one, unless the user entered a different one.
                var followsPrivateKey = !HasPublicKey ||
                    (SshKeys.TryGetPublicKey(Model.PrivateKey, out var previous) && SameKey(previous, Model.PublicKey));
                Model.PrivateKey = value;
                OnPropertyChanged();
                if (followsPrivateKey && SshKeys.TryGetPublicKey(value, out var derived) && !SameKey(derived, PublicKey))
                    PublicKey = derived;
                OnPropertyChanged(nameof(HasPrivateKey));
                OnPropertyChanged(nameof(IsEncrypted));
            }
        }

        public string PublicKey
        {
            get => Model.PublicKey;
            set
            {
                if (Model.PublicKey == value)
                    return;

                Model.PublicKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPublicKey));
                OnPropertyChanged(nameof(KeyInfo));
                OnPropertyChanged(nameof(HasKeyInfo));
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(SubtitleToolTip));
            }
        }

        public string Passphrase
        {
            get => Model.Passphrase;
            set
            {
                if (Model.Passphrase == value)
                    return;

                Model.Passphrase = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPassphrase));
            }
        }

        public bool HasPrivateKey => !string.IsNullOrWhiteSpace(PrivateKey);
        public bool HasPublicKey => !string.IsNullOrWhiteSpace(PublicKey);
        public bool HasPassphrase => !string.IsNullOrEmpty(Passphrase);
        public bool IsEncrypted => SshKeys.IsEncrypted(PrivateKey);

        public sealed record KeyDescription(string KeyType, string Fingerprint);

        /// <summary>Key type and fingerprint of <see cref="PublicKey"/>, or null when it cannot be read.</summary>
        public KeyDescription KeyInfo =>
            SshKeys.TryParsePublicKey(PublicKey, out var keyType, out var fingerprint, out _)
                ? new KeyDescription(keyType, fingerprint)
                : null;

        public bool HasKeyInfo => KeyInfo != null;

        public override string Subtitle => KeyInfo?.KeyType;
        public override string SubtitleToolTip => KeyInfo?.Fingerprint;
        public override string Glyph => "";
        public override IEnumerable<string> SearchableTexts => [PublicKey];

        /// <summary>Same key data; the comment after it does not matter.</summary>
        private static bool SameKey(string first, string second)
        {
            static string KeyPart(string line)
            {
                var parts = line?.Trim().Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
                return parts is { Length: >= 2 } ? parts[0] + " " + parts[1] : line?.Trim();
            }

            return KeyPart(first) == KeyPart(second);
        }
    }
}

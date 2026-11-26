using Erapkey.Core.Services;
using Xunit;

namespace Erapkey.Core.Tests
{
    public class SshKeysTests
    {
        // Throwaway keys made with ssh-keygen for these tests only.
        private const string Ed25519 = """
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
QyNTUxOQAAACBMIF9q3Amr/YetLTY6w7j88rzMZURBVp3C+2AhcK0+KAAAAJCQwnWPkMJ1
jwAAAAtzc2gtZWQyNTUxOQAAACBMIF9q3Amr/YetLTY6w7j88rzMZURBVp3C+2AhcK0+KA
AAAEDEHOSDL0zWat33er/lpOa5aR2BaXVfph1jnN9IsC3tH0wgX2rcCav9h60tNjrDuPzy
vMxlREFWncL7YCFwrT4oAAAAB3Rlc3RAZWQBAgMEBQY=
-----END OPENSSH PRIVATE KEY-----
""";

        private const string Ed25519Encrypted = """
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jdHIAAAAGYmNyeXB0AAAAGAAAABC2534Sut
fZA014cL7nrySqAAAAGAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIC5v4WRgluq3cDRW
xuKPZ3+51rymlaAJ/zrSBZiGZcc/AAAAkPyfQhrWRR5/SdREXzGsppk6V1jkg3q+PeMNLo
B/6sJass3+iupbibnH+CDsuiKgj5LqAtsrY8g3WSceIc1vZUQtreT9nWFGSkLExscxykic
hFgz6wRJ0bRMcIic4p2z7R7UDbpL007VWQka3XVIYMN5oiNM4ijobmObQySNxlDxSX8AYX
o94S5ACWql5wa9rw==
-----END OPENSSH PRIVATE KEY-----
""";

        private const string RsaPem = """
-----BEGIN RSA PRIVATE KEY-----
MIICWwIBAAKBgQCw8P/sdK5tgyOQui2Ql5hYAFqB7LQ2Rn+6mCzeXQIvmdnehIg4
TEqw99URLhE8AZk3CpfUfRjWBC2Txffo83/JCBBqJ3JWSxgzsNI8MyfGWrJcs+cy
LBf7wqs3yvEuq7ReZb3vVVy8qtY9esSgaR3L+rUcWVS7DEF8xxlMw5JrgwIDAQAB
AoGAF19uXvy2wc6N1yNQu4Xa4N0DF6B92aGMY8iQPLtKe6E2D1ewasdhImlKYt+T
9To80/F5Rt9TdYnNv3RqIAJyQZKkSKHWBCpXB7zoyCdTuJ+BUCt2Ei0IXyiqZuY/
H7gGqVQZf2FQ605sis0drzr+miof/OYyIRb+ZnFSKuow6JkCQQDcs1u5x3XaLNfO
wuXE087VSEPdAoTfCmd6jy9X3MKvlxiCeN6D0TbAl50LOpDkHxcsXzTtW+fkQNlX
RwCpd3CnAkEAzT3o1uwzM3bQx9+yN68E0lTnZxOWhyUNpEvznK+kssulNB+ODyIr
1IsuNhjhZcW/OSSjIsgkS/Jm0taUAlXNxQJAKwjCKwCMp2U1E3kHapDOdOIpcW/r
Tr5tnQVMevpt7+Urf01oPhXOclFd6vGZjRHO5/UMkffoyKYoew86PtEuYwJANbSW
ge7KsxPLLBIH83Qzwcn/x0Qj5nc2q6BIPd7kLPkG4K3FIYaii3pj/DnkeRoWoKBK
o+ZVQR5L8628iyPitQJAag63KR78hwzGTut6d5jRZ2HeVew6CPaxp7Twe+IVBZHL
mtq8LhCIxvG3TD4FCr7WhknXHd71rFkXh4LRh2l1nQ==
-----END RSA PRIVATE KEY-----
""";

        private const string EcdsaPem = """
-----BEGIN EC PRIVATE KEY-----
MHcCAQEEIF5E8C0vFGyz4WWuxzt3Ur+IVoUoseBjMLrGmMm0KVLhoAoGCCqGSM49
AwEHoUQDQgAEPlKj9vSKZjENoe+cXkhSxAFV07kR8XRRx1vf6JAo3Cepz5NdGx2D
2hTDiSYZlFrIsHlKliZrp7l8qB5xh/QmfA==
-----END EC PRIVATE KEY-----
""";

        private const string Ed25519Public = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIEwgX2rcCav9h60tNjrDuPzyvMxlREFWncL7YCFwrT4o";
        private const string Ed25519EncryptedPublic = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC5v4WRgluq3cDRWxuKPZ3+51rymlaAJ/zrSBZiGZcc/";
        private const string RsaPublic = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAAAgQCw8P/sdK5tgyOQui2Ql5hYAFqB7LQ2Rn+6mCzeXQIvmdnehIg4TEqw99URLhE8AZk3CpfUfRjWBC2Txffo83/JCBBqJ3JWSxgzsNI8MyfGWrJcs+cyLBf7wqs3yvEuq7ReZb3vVVy8qtY9esSgaR3L+rUcWVS7DEF8xxlMw5Jrgw==";
        private const string EcdsaPublic = "ecdsa-sha2-nistp256 AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBD5So/b0imYxDaHvnF5IUsQBVdO5EfF0Ucdb3+iQKNwnqc+TXRsdg9oUw4kmGZRayLB5SpYma6e5fKgecYf0Jnw=";

        [Theory]
        [InlineData(Ed25519, Ed25519Public)]
        [InlineData(Ed25519Encrypted, Ed25519EncryptedPublic)]
        [InlineData(RsaPem, RsaPublic)]
        [InlineData(EcdsaPem, EcdsaPublic)]
        public void PublicKey_IsDerivedFromPrivateKey(string privateKey, string expected)
        {
            Assert.True(SshKeys.TryGetPublicKey(privateKey, out var publicKey));
            Assert.Equal(expected, publicKey);
        }

        [Fact]
        public void PublicKey_IsDerivedFromKeyWithWindowsLineEndings()
        {
            Assert.True(SshKeys.TryGetPublicKey(Ed25519.Replace("\n", "\r\n"), out var publicKey));
            Assert.Equal(Ed25519Public, publicKey);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not a key")]
        [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n-----END OPENSSH PRIVATE KEY-----")]
        public void InvalidPrivateKey_HasNoPublicKey(string privateKey)
        {
            Assert.False(SshKeys.TryGetPublicKey(privateKey, out _));
        }

        [Fact]
        public void Encryption_IsDetected()
        {
            Assert.True(SshKeys.IsEncrypted(Ed25519Encrypted));
            Assert.False(SshKeys.IsEncrypted(Ed25519));
            Assert.False(SshKeys.IsEncrypted(RsaPem));
        }

        [Theory]
        [InlineData(Ed25519Public + " test@ed", "ssh-ed25519", "SHA256:5ZiLFkyGDrHWv8V/63/ekma4WjceBq/8V1b2sLe4veA", "test@ed")]
        [InlineData(RsaPublic, "ssh-rsa", "SHA256:nKLJswdmCJvRmyf53Z4IMprNLuqKxUsWHQzNZsF6Fa0", null)]
        [InlineData(EcdsaPublic + "  ", "ecdsa-sha2-nistp256", "SHA256:lBu3jxOgV82o4TAdNneeClfLFtAfYd6mV6aVUf4Yz6M", null)]
        public void PublicKey_FingerprintMatchesSshKeygen(string publicKey, string type, string fingerprint, string comment)
        {
            Assert.True(SshKeys.TryParsePublicKey(publicKey, out var parsedType, out var parsedFingerprint, out var parsedComment));
            Assert.Equal(type, parsedType);
            Assert.Equal(fingerprint, parsedFingerprint);
            Assert.Equal(comment, parsedComment);
        }

        [Theory]
        [InlineData("ssh-rsa AAAAC3NzaC1lZDI1NTE5AAAAIEwgX2rcCav9h60tNjrDuPzyvMxlREFWncL7YCFwrT4o")]
        [InlineData("ssh-ed25519 not-base64")]
        [InlineData("ssh-ed25519")]
        public void InvalidPublicKey_IsRejected(string publicKey)
        {
            Assert.False(SshKeys.TryParsePublicKey(publicKey, out _, out _, out _));
        }

        [Fact]
        public void GeneratedKey_HasMatchingPublicKey()
        {
            var (privateKey, publicKey) = SshKeys.GenerateEd25519("me@host");

            Assert.StartsWith("-----BEGIN OPENSSH PRIVATE KEY-----\n", privateKey);
            Assert.EndsWith("-----END OPENSSH PRIVATE KEY-----\n", privateKey);
            Assert.False(SshKeys.IsEncrypted(privateKey));
            Assert.True(SshKeys.TryGetPublicKey(privateKey, out var derived));
            Assert.Equal(publicKey, derived + " me@host");
            Assert.True(SshKeys.TryParsePublicKey(publicKey, out var type, out _, out var comment));
            Assert.Equal("ssh-ed25519", type);
            Assert.Equal("me@host", comment);
        }

        [Fact]
        public void PrivateKey_IsNormalizedForFiles()
        {
            Assert.Equal("a\nb\n", SshKeys.NormalizePrivateKey("\r\na\r\nb"));
        }
    }
}

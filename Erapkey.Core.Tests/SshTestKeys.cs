using System.Collections.Generic;

namespace Erapkey.Core.Tests
{
    /// <summary>Throwaway keys made with ssh-keygen (OpenSSH for Windows 9.5) for the agent tests only.</summary>
    public static class SshTestKeys
    {
        internal sealed record TestKey(string Name, string PrivateKey, string Passphrase, string PublicKey);

        // aes256-ctr, bcrypt 4 rounds.
        internal static readonly TestKey Ed25519Ctr = new("Ed25519Ctr", """
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jdHIAAAAGYmNyeXB0AAAAGAAAABC8PWPQhW
0hrq0i/WwdK7cBAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIDG5dU187zISpbII
BiUwWKQ+gREvtTsDXCLbqaLqnoiuAAAAkDhGo3trUecAgJaEz9YuQEitoAF/Ri96k2hjVz
uYLCR4QC4/gVwJcNqTSfE5nWjHG06StpMxbeQnbZCYL6mVwCWPmJSrPkcdeyS17erpVXlg
NaZDEsmC8TddrDHDO8f5+AE7mi5VkrbWvj0hx0WGGqkKtJd0DS0pFNXpdRFSMJ4JeheCzi
p3U15OMEXyb+nsNQ==
-----END OPENSSH PRIVATE KEY-----
""", "test pass",
            "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIDG5dU187zISpbIIBiUwWKQ+gREvtTsDXCLbqaLqnoiu");

        // aes256-ctr, bcrypt 2 rounds.
        internal static readonly TestKey EcdsaP256Ctr = new("EcdsaP256Ctr", """
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jdHIAAAAGYmNyeXB0AAAAGAAAABCpCGN+Xc
mtdEbKUfFFMw0FAAAAAgAAAAEAAABoAAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlz
dHAyNTYAAABBBC548a23K4TvanB3chvNA9ZNtjR7dhSQ+LqxUgvVHVUgAPlnvvODhDFDwz
5oQY3qQwt5PlijJdS8qj8FSXgg7yUAAACgJ0H1bGLOA8gMX82DzwZ72k8zH4xTg+M2X+xW
EqhOWd+rskeWmi2rp73Z0HOCd2qfUOG8eJ7xag36Py1Cl2b+k3x+eB29r2qO8xxFi7hnuM
l5TY/iCKqtIqQnMFrPLUw1LQigUjX53L79v0gTF+1h4mNwjjk/Wimo1YYPVtEbUwrE0wWU
gq/e89afRJRN5NEBK0hMUKxRVp43JXzZ6/ILRw==
-----END OPENSSH PRIVATE KEY-----
""", "secret",
            "ecdsa-sha2-nistp256 AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBC548a23K4TvanB3chvNA9ZNtjR7dhSQ+LqxUgvVHVUgAPlnvvODhDFDwz5oQY3qQwt5PlijJdS8qj8FSXgg7yU=");

        // no passphrase.
        internal static readonly TestKey EcdsaP384 = new("EcdsaP384", """
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAiAAAABNlY2RzYS
1zaGEyLW5pc3RwMzg0AAAACG5pc3RwMzg0AAAAYQT0Z1mdB/rLgkbK8+2Ca8ACPFwLzwhm
y4cEevmZpKmJTKc2o/grWjXCQGv9mhZXWaTSGbihaJWNJwZ4RcxvTtZ8/V2MN8l27jX62S
PMLrinWFCsEawembscnHQdG8ByfGkAAADQ0boardG6Gq0AAAATZWNkc2Etc2hhMi1uaXN0
cDM4NAAAAAhuaXN0cDM4NAAAAGEE9GdZnQf6y4JGyvPtgmvAAjxcC88IZsuHBHr5maSpiU
ynNqP4K1o1wkBr/ZoWV1mk0hm4oWiVjScGeEXMb07WfP1djDfJdu41+tkjzC64p1hQrBGs
Hpm7HJx0HRvAcnxpAAAAMH6qf63HXAJFvmce803uuBy4A8iEc6hlRXo1YIeTCkY/qqtFrZ
Hyn8dH9hpkRtTypwAAAAVlYzM4NAECAw==
-----END OPENSSH PRIVATE KEY-----
""", null,
            "ecdsa-sha2-nistp384 AAAAE2VjZHNhLXNoYTItbmlzdHAzODQAAAAIbmlzdHAzODQAAABhBPRnWZ0H+suCRsrz7YJrwAI8XAvPCGbLhwR6+ZmkqYlMpzaj+CtaNcJAa/2aFldZpNIZuKFolY0nBnhFzG9O1nz9XYw3yXbuNfrZI8wuuKdYUKwRrB6ZuxycdB0bwHJ8aQ==");

        // aes256-gcm@openssh.com.
        internal static readonly TestKey RsaGcm = new("RsaGcm", """
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAAFmFlczI1Ni1nY21Ab3BlbnNzaC5jb20AAAAGYmNyeXB0AA
AAGAAAABB0Pr+dcdLLdkDDOLIxSlaNAAAAGAAAAAEAAAEXAAAAB3NzaC1yc2EAAAADAQAB
AAABAQDZx92QkxbifN6K86RAxh644zR09IUFojrEMq2eWpaBkNaDACf7IYTZqZGvF005zv
65dIeq26bJlXRDYsweTawtRhism2MsZhJkJLp/CmxlTo9wMMit5AcNEftTlIdCR7dT6/br
4xizdb8jeKlUJb4BzJYMZR+P4ILFZRbNtbrzViCk7rN/zwdcYNT0YxKLciPwTWI0j1tuA2
W/drBIDPHUK0ep7HmjZvBliPVfDoJgCDFYyyqcNwjzHhBExdy8CceeCdSlxec6vgJ+e1JK
kuLqSywgUWV576EAA780JBg0xZjNu3Z23flOJG9j6knYS61dhNdvw5S8dstDSNsJa9vrAA
ADwA9AYhMl394FuJyblDhNKzT7L4OhMB8QugeqQYfVbq7UvM4dggkmp8g0kwpq1ax0hHM2
oEqwDsZJjXYEwG25rohZ7wvYmHpiKj5z5AcAuhwuMpa54HXQ5JowwIKxoSP2fA24+BhyD+
ofs/i3pWBhpYJXNxM32nnWn4zvgPeg7JRZ9BxviN7BYXFB9Mhp6frZtUq0ncxheBR8sHBb
TZTh1bFdTWhWFLYoztA/43/gbsGTdzGgn0NUXF4435FIGk9LJ1R2fP0+HtzQ7MSabZMmyQ
uTsrF23LnC+XVjs5a4o303P2Yhg+LNBqiMQzaXaWzyHcxEM7fk4AoYgH0dC1HrXKLY34yp
tzYHC1pnLRapCr47kf9E6HFPKe78QSu0+WEYxB3XNqS2ENdWVuHFDKq9iBnpzqZdFGk0Qg
ciqSKE2n4m5RVPkYrJFjqHau+bPE/iIwDEYgU4D+n66CakX+N4fPXK0iQ7myqQuZFQCf3p
fPxWe68NDQAZNIWx2onlm8XE6tumlDwUMn7YlwIV23z/fb+xX694K5/pvtGDMZwW/wYxZK
kybfcDdCCJ0jJfLRnQPUuCgoxYqkULUgyV6HrUZNYouhn+XCLbB+rzSiNT68uPkhQwLzJz
hcP7zSk6IL3IVn8Kajac1Mwckhl7YyuIhJA3jqQ6Watd1d3sgssPOu8RD3TW57nl2QWR6x
Ke/i56XrXJcuM6ZiqVbDYqBNMkIDbBswZ37wWcNRgMq442jvZWFvAvN978JC4OrmZvWN8M
TCc2ResuMGCVoiSsGncB1QAWKiZUmxdBRh80GGlOrjPoNfzbo1NWgRwYSh/DUX2pDu+UcR
1WQ+zim346jlOMxZv7JndX2VMbXSrgPh4E8UDtwWWeNEcMZayyI+RgXslCfGtsNeA1T6KR
wKn/n9Anb8rwURkB14jWEa8rbJyIqL9GPGXmOpmC3PNMmgp8TYWQJh030ebeu64g0GYoF6
61g28s8oySREtZJts/TmdKWRIH2YO4iLsggWEvA4LgaqA49d4j/H4uZlQH8xDG2M7wqo5i
MiirmjBgDDuct9A/w6GzTE25AzYXtaZOdd+CNIq+T0HlbaWmDZhRwG1V13PxsDKBvRu04F
hGtqv963EJlnmLpPWHlJRyLTPZi2/0Z2Yqg3Q90J5aREHYLoqAVE+TyqJ3CrByCZbWV+IN
1Lpw9gMo2d5BqzXauONH3xhANTKY0zkP33v4Q/A1nL6y8MJSU1g2TYVH00kVHK8GZH8Ojn
PVuoI+jYrSwck6ppTu/SLn7YJQcNino4wneKPrEPHNcA8=
-----END OPENSSH PRIVATE KEY-----
""", "rsapass",
            "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDZx92QkxbifN6K86RAxh644zR09IUFojrEMq2eWpaBkNaDACf7IYTZqZGvF005zv65dIeq26bJlXRDYsweTawtRhism2MsZhJkJLp/CmxlTo9wMMit5AcNEftTlIdCR7dT6/br4xizdb8jeKlUJb4BzJYMZR+P4ILFZRbNtbrzViCk7rN/zwdcYNT0YxKLciPwTWI0j1tuA2W/drBIDPHUK0ep7HmjZvBliPVfDoJgCDFYyyqcNwjzHhBExdy8CceeCdSlxec6vgJ+e1JKkuLqSywgUWV576EAA780JBg0xZjNu3Z23flOJG9j6knYS61dhNdvw5S8dstDSNsJa9vr");

        // PEM, no passphrase.
        internal static readonly TestKey RsaPem = new("RsaPem", """
-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEApUpeik0DlyQiQZgvQi3pX3gYJDsTgInBGAlGlLcDfApN3+uT
3F7UFsf8pEbsmp/BQS/h2TUNKtY+u69IO8hfZMIm3fS1fTiQJzu5f81Q9el09/W1
vMcdO/r4oL95SeillKLn7Sr1jxbDhirJHzRxaw+7V6GzHBnF7C2kPnnfw6l0Ephh
HBwtyCoNzuPhQ10Qz0OPeY1WQrsObvYG/Ek+LhEbdWW0XQbvOXR4ZA4gWCQlo6sl
beIXG17TpdxjQcnDFLzo5D3p4oBHkHMGKn8Fwfdn1XCU1ZoT9d8igmYTB6YLh7lh
p3r4Nw44lgQkexwuGmtzz4wmAnaUmVPAje5VWQIDAQABAoIBADW+SaEUS2+QSyci
0XLmNLRa+xaEsuGfaM5k7bMbEqH5Q7rkAS6tEq7ZlbbgbLwV56NHa1rRmhdsk/c4
lfcCPqonSrXuhDhT9OMc00LPwlxC7UpWdSINZ98BymeR+mDiqTv8AAcoabF9yFNO
hBiGV75k3hgvNmqTorwZVwcNX4lhgJSZPn1nDVxi5TEPFDEmvOoSNijvlINESBxF
trTekAHBUM7w8yVvFAgUNoMTtJKdIgG5YXg0dj9FfcTSpILVVsICWKxzdSDqAikB
kGfB/u/CF8/CaUq6N9Wxblauuks996/7iyKYvF34/uerJQTyT5nHOkihA4y54DNY
3eXWKeUCgYEA23F6WjuFyzm35SZV3htF/PgR+LARMka9CJD0y3x5iTh2IPl/1sr7
A7fqTQfmiIxkpz7QlJQfIXbquWfKhLguSHCnlpQ1USzDISMrQD7HEJwXWesJ266J
LdXKb3C6F6Vp/mabEeAxmGmhU2nfUwwZTTs6cW1k45kCCe7TjRxc8l8CgYEAwNN0
8efWUyM4dkqg29tlP/gfK1KcydXZ6s5ByETSIpe25DtEYFKyE2z4JdF0LzyezAc5
8LaDoT/Uc7FWGmVmXAB3qFI374EkJ5qiqfLyLf/vgleaZ9SGlyX24Y+J4BbF+0Dq
eJlujpyAT5HgEULnrD0d29VKA06cjFVPkQiwA0cCgYEAxl5Xdzf1Y1eit/+mgafv
5ZSuUjt+4A74FMVMtKGqD/Q9POOtv5IvxRW9NLMen3MiDdmHnbgkSBECcGW1ubHx
DMThigoiBGZNYigzkdjuBELGDR+eArtNAGURoRmq73M1VKaWKVucGaXrEsdS/PSj
7p/K3fxAxklrgWUmUZpBH0sCgYBq5pBF8F681qDdy7/kQ3yde0RkczdjGkDm0RRd
H/JTpUKKw2TD9fStmlL0PGuqKHRs7nCrx7Ov+MAip6Txm1elF8k+st451KGjpzIZ
AAxNfj9dRo3elE9NiomVQUuRxW5bPgD/fShyvcbQ1jGfwDTKI1PjZRrUPIfDsSQc
ZvGVzwKBgCZqEPoe/dZJ8fGBvowsuf1nrY/8g0wWnc0CLfZkJAe1ZjC/e/Zo9ybl
TxYeVOejphQzPqNGLHPTB+FifVpvEzqMWzXzwzwqkiP6M3f0JKAVCQzaBObmwj0e
2u+t1+gnZip/mTnSy/9NjscSfZ00AUUJ2uBE+zJGlqMqq6K05GZw
-----END RSA PRIVATE KEY-----
""", null,
            "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQClSl6KTQOXJCJBmC9CLelfeBgkOxOAicEYCUaUtwN8Ck3f65PcXtQWx/ykRuyan8FBL+HZNQ0q1j67r0g7yF9kwibd9LV9OJAnO7l/zVD16XT39bW8xx07+vigv3lJ6KWUouftKvWPFsOGKskfNHFrD7tXobMcGcXsLaQ+ed/DqXQSmGEcHC3IKg3O4+FDXRDPQ495jVZCuw5u9gb8ST4uERt1ZbRdBu85dHhkDiBYJCWjqyVt4hcbXtOl3GNBycMUvOjkPenigEeQcwYqfwXB92fVcJTVmhP13yKCZhMHpguHuWGnevg3DjiWBCR7HC4aa3PPjCYCdpSZU8CN7lVZ");

        // encrypted PKCS#8.
        internal static readonly TestKey EcdsaP521Pkcs8 = new("EcdsaP521Pkcs8", """
-----BEGIN ENCRYPTED PRIVATE KEY-----
MIIBTzBJBgkqhkiG9w0BBQ0wPDAbBgkqhkiG9w0BBQwwDgQITA4bnzYVJqoCAggA
MB0GCWCGSAFlAwQBAgQQ1matEigi70ZTh51rpq0PwQSCAQAvxDdVnsf/md+EuQm8
k9ECFN1J8VAt5zThnKdTrBQCvlvg6WNcYuwvXctLQR4Puyzr9ZU6HkiSg3RYcFcT
2FmtGHIti9eDkbsQALzauKDqBvfyvKUITxU8jW7NO1lRWLYA+pn/2tn6d3J0LKsf
89055zkIpfxxlsD9DPb+08m/tQdM3njRsCaWOAwCliNNelYIMHJ0b4BoCHoQ4Q0g
W7bF0hebkcehRKIsJDKXqHWrB0qx5/zeO+LbbnTLHIi4eN045t2K8yi31snYpKzz
jiurVDrHvqRlhDNfKxDN3EdIj05KaVXT6GrFPygnngXjCKO9Wrz8jytxPH/G7i9W
TG3o
-----END ENCRYPTED PRIVATE KEY-----
""", "pkcs8pw",
            "ecdsa-sha2-nistp521 AAAAE2VjZHNhLXNoYTItbmlzdHA1MjEAAAAIbmlzdHA1MjEAAACFBAEtr+vhuVN4X3FL5Yy2VqJ+C+apIH5mlqFRbi4mDLQYHbwSeEzu6kICCCvlWBb3D98/wJSC9yOW0g/rtufropjMNgBvs3M0IbGV6bN2IHsF2qM9KDFSoiwvbhnpBVntuaFLoNOVzZzUwSEVuT8TaMN+SvvzYv50yU3rPp4pdnbKHHKRgw==");

        public static IEnumerable<object[]> All()
        {
            yield return [Ed25519Ctr.Name];
            yield return [EcdsaP256Ctr.Name];
            yield return [EcdsaP384.Name];
            yield return [RsaGcm.Name];
            yield return [RsaPem.Name];
            yield return [EcdsaP521Pkcs8.Name];
        }

        internal static TestKey Get(string name) => name switch
        {
            nameof(Ed25519Ctr) => Ed25519Ctr,
            nameof(EcdsaP256Ctr) => EcdsaP256Ctr,
            nameof(EcdsaP384) => EcdsaP384,
            nameof(RsaGcm) => RsaGcm,
            nameof(RsaPem) => RsaPem,
            nameof(EcdsaP521Pkcs8) => EcdsaP521Pkcs8,
            _ => throw new System.ArgumentException(name)
        };
    }
}

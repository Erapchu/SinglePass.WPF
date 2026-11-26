using Erapkey.Core.Services;
using System.IO;
using Xunit;

namespace Erapkey.Core.Tests
{
    public class PublicSuffixListTests
    {
        // The examples of the list's own test data (https://github.com/publicsuffix/list/blob/main/tests/test_psl.txt).
        private static readonly PublicSuffixList Sample = PublicSuffixList.Parse(new StringReader("""
            // comment
            com
            uk
            co.uk
            jp
            ac.jp
            kyoto.jp
            ide.kyoto.jp
            *.kobe.jp
            !city.kobe.jp
            *.ck
            !www.ck
            github.io   trailing text is ignored
            公司.cn
            cn
            """));

        [Theory]
        [InlineData("com", null)]
        [InlineData("example.com", "example.com")]
        [InlineData("www.example.com", "example.com")]
        [InlineData("a.b.example.com", "example.com")]
        [InlineData("co.uk", null)]
        [InlineData("example.co.uk", "example.co.uk")]
        [InlineData("login.example.co.uk", "example.co.uk")]
        [InlineData("ac.jp", null)]
        [InlineData("test.ac.jp", "test.ac.jp")]
        [InlineData("www.test.ac.jp", "test.ac.jp")]
        [InlineData("ide.kyoto.jp", null)]
        [InlineData("b.ide.kyoto.jp", "b.ide.kyoto.jp")]
        [InlineData("a.b.ide.kyoto.jp", "b.ide.kyoto.jp")]
        [InlineData("c.kobe.jp", null)]
        [InlineData("b.c.kobe.jp", "b.c.kobe.jp")]
        [InlineData("a.b.c.kobe.jp", "b.c.kobe.jp")]
        [InlineData("city.kobe.jp", "city.kobe.jp")]
        [InlineData("www.city.kobe.jp", "city.kobe.jp")]
        [InlineData("ck", null)]
        [InlineData("test.ck", null)]
        [InlineData("b.test.ck", "b.test.ck")]
        [InlineData("www.ck", "www.ck")]
        [InlineData("www.www.ck", "www.ck")]
        [InlineData("github.io", null)]
        [InlineData("alice.github.io", "alice.github.io")]
        [InlineData("xn--55qx5d.cn", null)]
        [InlineData("shishi.xn--55qx5d.cn", "shishi.xn--55qx5d.cn")]
        // No rule: the last label is the suffix.
        [InlineData("example", null)]
        [InlineData("example.example", "example.example")]
        [InlineData("b.example.example", "example.example")]
        public void RegistrableDomain(string host, string expected)
        {
            Assert.Equal(expected, Sample.GetRegistrableDomain(host));
        }

        [Fact]
        public void EmbeddedList_Loads()
        {
            var list = PublicSuffixList.Default;
            Assert.True(list.RuleCount > 5000);
            Assert.Equal("example.co.uk", list.GetRegistrableDomain("login.example.co.uk"));
            Assert.Null(list.GetRegistrableDomain("github.io"));
            Assert.Equal("alice.github.io", list.GetRegistrableDomain("www.alice.github.io"));
        }
    }

    public class WebsiteMatcherTests
    {
        private static readonly WebsiteMatcher Matcher = new(PublicSuffixList.Default);

        [Theory]
        // Exact
        [InlineData("example.com", "https://example.com/login", WebsiteMatch.Exact)]
        [InlineData("https://Example.COM/", "https://example.com/", WebsiteMatch.Exact)]
        [InlineData("example.com.", "https://example.com/", WebsiteMatch.Exact)]
        [InlineData("//example.com", "https://example.com/", WebsiteMatch.Exact)]
        [InlineData("https://user@example.com/path?q", "https://example.com/other", WebsiteMatch.Exact)]
        // Domain
        [InlineData("example.com", "https://login.example.com/", WebsiteMatch.Domain)]
        [InlineData("login.example.com", "https://example.com/", WebsiteMatch.Domain)]
        [InlineData("a.example.co.uk", "https://b.example.co.uk/", WebsiteMatch.Domain)]
        // Look-alikes and neighbours
        [InlineData("example.com", "https://example.com.evil.net/", WebsiteMatch.None)]
        [InlineData("example.com", "https://notexample.com/", WebsiteMatch.None)]
        [InlineData("example.co.uk", "https://other.co.uk/", WebsiteMatch.None)]
        [InlineData("alice.github.io", "https://bob.github.io/", WebsiteMatch.None)]
        [InlineData("co.uk", "https://example.co.uk/", WebsiteMatch.None)]
        // Scheme: no downgrade, an http entry is fine on https
        [InlineData("https://example.com", "http://example.com/", WebsiteMatch.None)]
        [InlineData("example.com", "http://example.com/", WebsiteMatch.None)]
        [InlineData("http://example.com", "https://example.com/", WebsiteMatch.Exact)]
        [InlineData("http://example.com", "http://example.com/", WebsiteMatch.Exact)]
        // Pages that are not web pages
        [InlineData("example.com", "file:///C:/example.com/index.html", WebsiteMatch.None)]
        [InlineData("example.com", "chrome://settings", WebsiteMatch.None)]
        [InlineData("example.com", "about:blank", WebsiteMatch.None)]
        [InlineData("example.com", "not a url", WebsiteMatch.None)]
        [InlineData("ftp://example.com", "https://example.com/", WebsiteMatch.None)]
        // Ports
        [InlineData("example.com:8443", "https://example.com:8443/", WebsiteMatch.Exact)]
        [InlineData("example.com:8443", "https://example.com/", WebsiteMatch.None)]
        [InlineData("example.com", "https://example.com:8443/", WebsiteMatch.Exact)]
        // IP addresses and single-label hosts match only exactly
        [InlineData("http://192.168.1.10", "http://192.168.1.10/", WebsiteMatch.Exact)]
        [InlineData("http://192.168.1.10", "http://192.168.1.11/", WebsiteMatch.None)]
        [InlineData("http://10.1.1.1", "http://20.1.1.1/", WebsiteMatch.None)]
        [InlineData("http://[::1]:8080", "http://[::1]:8080/", WebsiteMatch.Exact)]
        [InlineData("http://localhost:3000", "http://localhost:3000/", WebsiteMatch.Exact)]
        [InlineData("http://router", "http://other.router/", WebsiteMatch.None)]
        // IDN: Unicode in the entry, punycode in the page address and the other way round
        [InlineData("пример.рф", "https://xn--e1afmkfd.xn--p1ai/", WebsiteMatch.Exact)]
        [InlineData("https://xn--e1afmkfd.xn--p1ai", "https://вход.пример.рф/", WebsiteMatch.Domain)]
        // Empty entries
        [InlineData("", "https://example.com/", WebsiteMatch.None)]
        [InlineData(null, "https://example.com/", WebsiteMatch.None)]
        public void Match(string entrySite, string pageUrl, WebsiteMatch expected)
        {
            Assert.Equal(expected, Matcher.Match(entrySite, pageUrl));
        }

        [Theory]
        [InlineData("example.com", 0, "example.com")]
        [InlineData("https://example.com/a", 8, "example.com")]
        [InlineData("//example.com", 2, "example.com")]
        [InlineData("example.com/https://x", 0, "example.com")]
        public void ParseEntrySite(string site, int authorityStart, string host)
        {
            var uri = WebsiteMatcher.ParseEntrySite(site, out var start);
            Assert.NotNull(uri);
            Assert.Equal(host, uri.Host);
            Assert.Equal(authorityStart, start);
        }
    }
}

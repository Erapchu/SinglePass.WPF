using System;

namespace SinglePass.Core.Services
{
    public enum WebsiteMatch
    {
        None,
        /// <summary>Same registrable domain: <c>login.example.com</c> for an entry of <c>example.com</c>.</summary>
        Domain,
        /// <summary>Same host.</summary>
        Exact,
    }

    /// <summary>
    /// Decides whether the website of a login entry belongs to the page the browser shows (docs/browser-extension.md#url-matching).
    /// A look-alike host such as <c>example.com.evil.net</c> never matches <c>example.com</c>.
    /// </summary>
    public sealed class WebsiteMatcher
    {
        private readonly PublicSuffixList _suffixes;

        public WebsiteMatcher(PublicSuffixList suffixes)
        {
            _suffixes = suffixes ?? throw new ArgumentNullException(nameof(suffixes));
        }

        /// <summary>
        /// Reads the website field of an entry. A missing scheme means <c>https</c>; <paramref name="authorityStart"/>
        /// is where the host part starts in <paramref name="site"/>. Null when there is no host.
        /// </summary>
        public static Uri ParseEntrySite(string site, out int authorityStart)
        {
            authorityStart = 0;
            site = site?.Trim();
            if (string.IsNullOrEmpty(site))
                return null;

            if (site.StartsWith("//", StringComparison.Ordinal))
            {
                authorityStart = 2;
                site = "https:" + site;
            }
            else
            {
                var schemeEnd = site.IndexOf("://", StringComparison.Ordinal);
                var firstSeparator = site.IndexOfAny(['/', '?', '#', '\\']);
                if (schemeEnd >= 0 && firstSeparator == schemeEnd + 1)
                    authorityStart = schemeEnd + 3;
                else
                    site = "https://" + site;
            }

            return Uri.TryCreate(site, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri
                : null;
        }

        public static Uri ParseEntrySite(string site) => ParseEntrySite(site, out _);

        /// <summary>The page address reported by the browser, if it is an http or https page with a host.</summary>
        public static Uri ParsePage(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsWeb(uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri
                : null;
        }

        /// <summary>Lowercase ASCII (punycode) host without a trailing dot; IPv6 addresses keep their brackets.</summary>
        public static string NormalizeHost(Uri uri)
        {
            if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
                return uri.Host.ToLowerInvariant();

            var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
            return host;
        }

        public WebsiteMatch Match(string entrySite, string pageUrl)
        {
            var page = ParsePage(pageUrl);
            return page == null ? WebsiteMatch.None : Match(ParseEntrySite(entrySite), page);
        }

        public WebsiteMatch Match(Uri entry, Uri page)
        {
            if (entry == null || page == null || !IsWeb(entry) || !IsWeb(page))
                return WebsiteMatch.None;

            // A password saved for https is not offered on http: the page could be a downgraded copy.
            if (entry.Scheme == Uri.UriSchemeHttps && page.Scheme != Uri.UriSchemeHttps)
                return WebsiteMatch.None;

            // A port written in the entry must be the page's; an entry without one fits any port.
            if (!entry.IsDefaultPort && entry.Port != page.Port)
                return WebsiteMatch.None;

            var entryHost = NormalizeHost(entry);
            var pageHost = NormalizeHost(page);
            if (entryHost.Length == 0 || pageHost.Length == 0)
                return WebsiteMatch.None;

            if (string.Equals(entryHost, pageHost, StringComparison.Ordinal))
                return WebsiteMatch.Exact;

            // IP addresses and single-label hosts (localhost, router) have no registrable domain.
            if (!HasRegistrableDomain(entry, entryHost) || !HasRegistrableDomain(page, pageHost))
                return WebsiteMatch.None;

            var entryDomain = _suffixes.GetRegistrableDomain(entryHost);
            var pageDomain = _suffixes.GetRegistrableDomain(pageHost);
            return entryDomain != null && string.Equals(entryDomain, pageDomain, StringComparison.Ordinal)
                ? WebsiteMatch.Domain
                : WebsiteMatch.None;
        }

        private static bool IsWeb(Uri uri) => uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;

        private static bool HasRegistrableDomain(Uri uri, string host) =>
            uri.HostNameType == UriHostNameType.Dns && host.Contains('.', StringComparison.Ordinal);
    }
}

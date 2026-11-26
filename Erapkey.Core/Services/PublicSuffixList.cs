using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Erapkey.Core.Services
{
    /// <summary>
    /// The Public Suffix List (https://publicsuffix.org/): suffixes under which anyone can register a name,
    /// such as <c>com</c>, <c>co.uk</c> or <c>github.io</c>. Tells the registrable domain of a host:
    /// <c>example.co.uk</c> for <c>login.example.co.uk</c>. Both the ICANN and the private sections are used.
    /// </summary>
    public sealed class PublicSuffixList
    {
        private const string ResourceName = "Erapkey.Core.public_suffix_list.dat";
        private static readonly Lazy<PublicSuffixList> _default = new(LoadDefault);

        private readonly HashSet<string> _rules = new(StringComparer.Ordinal);
        /// <summary>Wildcard rules <c>*.ck</c>, stored as their parent: <c>ck</c>.</summary>
        private readonly HashSet<string> _wildcards = new(StringComparer.Ordinal);
        /// <summary>Exception rules <c>!www.ck</c>, stored without the mark: <c>www.ck</c>.</summary>
        private readonly HashSet<string> _exceptions = new(StringComparer.Ordinal);

        /// <summary>The list embedded in this assembly (<c>Resources/public_suffix_list.dat</c>).</summary>
        public static PublicSuffixList Default => _default.Value;

        public int RuleCount => _rules.Count + _wildcards.Count + _exceptions.Count;

        private PublicSuffixList() { }

        /// <summary>Reads the list format: one rule per line, <c>//</c> comments, <c>*.</c> wildcards, <c>!</c> exceptions.</summary>
        public static PublicSuffixList Parse(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            var list = new PublicSuffixList();
            var idn = new IdnMapping();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var rule = line.Trim();
                if (rule.Length == 0 || rule.StartsWith("//", StringComparison.Ordinal))
                    continue;

                // Only the text up to the first whitespace is the rule.
                var end = rule.IndexOfAny([' ', '\t']);
                if (end > 0)
                    rule = rule[..end];

                var set = list._rules;
                if (rule.StartsWith('!'))
                {
                    set = list._exceptions;
                    rule = rule[1..];
                }
                else if (rule.StartsWith("*.", StringComparison.Ordinal))
                {
                    set = list._wildcards;
                    rule = rule[2..];
                }

                // Hosts are compared in their ASCII (punycode) form; the list has IDN rules in Unicode.
                string ascii;
                try
                {
                    ascii = idn.GetAscii(rule).ToLowerInvariant();
                }
                catch (ArgumentException)
                {
                    continue;
                }
                set.Add(ascii);
            }
            return list;
        }

        /// <summary>
        /// The public suffix of <paramref name="host"/>: <c>co.uk</c> for <c>login.example.co.uk</c>.
        /// A host that no rule covers has its last label as the suffix (the implicit <c>*</c> rule).
        /// </summary>
        /// <param name="host">Lowercase ASCII host without a trailing dot, see <see cref="WebsiteMatcher.NormalizeHost"/>.</param>
        public string GetPublicSuffix(string host)
        {
            ArgumentException.ThrowIfNullOrEmpty(host);
            var labels = host.Split('.');

            // From the longest candidate to the shortest: the first match is the most specific rule.
            for (var i = 0; i < labels.Length; i++)
            {
                var candidate = string.Join('.', labels, i, labels.Length - i);

                // An exception rule wins over the wildcard that covers it; its suffix is the rule minus the leftmost label.
                if (_exceptions.Contains(candidate))
                    return string.Join('.', labels, i + 1, labels.Length - i - 1);

                if (_rules.Contains(candidate))
                    return candidate;

                if (i + 1 < labels.Length && _wildcards.Contains(string.Join('.', labels, i + 1, labels.Length - i - 1)))
                    return candidate;
            }

            return labels[^1];
        }

        /// <summary>
        /// The registrable domain (eTLD+1) of <paramref name="host"/>: <c>example.co.uk</c> for <c>login.example.co.uk</c>.
        /// Null when the host is itself a public suffix, such as <c>co.uk</c> or <c>github.io</c>.
        /// </summary>
        public string GetRegistrableDomain(string host)
        {
            var suffix = GetPublicSuffix(host);
            // The dot before the suffix; there is none when the host is the suffix.
            var beforeSuffix = host.Length - suffix.Length - 1;
            if (beforeSuffix <= 0)
                return null;

            // The label before the suffix, with the suffix.
            var start = host.LastIndexOf('.', beforeSuffix - 1) + 1;
            return host[start..];
        }

        private static PublicSuffixList LoadDefault()
        {
            using var stream = typeof(PublicSuffixList).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"The resource {ResourceName} is missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader);
        }
    }
}

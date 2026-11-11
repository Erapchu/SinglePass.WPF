using SinglePass.WPF.ViewModels;
using System;
using Unidecode.NET;

namespace SinglePass.WPF.Helpers
{
    internal sealed class CredentialSearch
    {
        private readonly string _query;
        private readonly string _normalizedQuery;

        public CredentialSearch(string query)
        {
            _query = query;
            _normalizedQuery = query.Unidecode();
        }

        public bool Matches(CredentialViewModel credential) =>
            Matches(credential.NameFieldVM.Value)
            || Matches(credential.LoginFieldVM.Value)
            || Matches(credential.SiteFieldVM.Value)
            || Matches(credential.OtherFieldVM.Value);

        private bool Matches(string value) =>
            value is not null
            && (value.Contains(_query, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(_normalizedQuery)
                    && value.Unidecode().Contains(_normalizedQuery, StringComparison.OrdinalIgnoreCase)));
    }
}

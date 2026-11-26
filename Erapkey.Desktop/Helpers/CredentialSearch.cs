using Erapkey.Desktop.ViewModels;
using System;
using System.Linq;
using Unidecode.NET;

namespace Erapkey.Desktop.Helpers
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
            || Matches(credential.OtherFieldVM.Value)
            || credential.Data.SearchableTexts.Any(Matches);

        private bool Matches(string value) =>
            value is not null
            && (value.Contains(_query, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(_normalizedQuery)
                    && value.Unidecode().Contains(_normalizedQuery, StringComparison.OrdinalIgnoreCase)));
    }
}

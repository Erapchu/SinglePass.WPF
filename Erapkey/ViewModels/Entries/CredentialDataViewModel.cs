using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace Erapkey.ViewModels.Entries
{
    /// <summary>
    /// The type-specific part of an entry: one subclass per <see cref="Core.Models.CredentialType"/>.
    /// The details card picks the view and the editor for it by a <c>DataTemplate</c> of the subclass,
    /// and the list and search use only the members below, so a new type needs no changes there.
    /// </summary>
    public abstract class CredentialDataViewModel : ObservableObject
    {
        /// <summary>Second line of the entry in the list.</summary>
        public abstract string Subtitle { get; }

        public virtual string SubtitleToolTip => Subtitle;

        /// <summary>Segoe Fluent Icons glyph shown for the entry; null to show the site icon instead.</summary>
        public virtual string Glyph => null;

        /// <summary>Texts the search looks in, besides the name and notes.</summary>
        public abstract IEnumerable<string> SearchableTexts { get; }
    }
}

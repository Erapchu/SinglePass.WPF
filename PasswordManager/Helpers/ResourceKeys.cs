using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PasswordManager.Helpers
{
    public static class ResourceKeys
    {
        /// <summary>
        /// The resource key for the icon control style.
        /// </summary>
        /// <remarks>
        /// Add a style with this key to your application resources to override the applications icon in the window.
        /// This style will be applied to a control in the top left corner of the window caption to display the applications icon.
        /// Using a control styles enables to use any WPF element to design the icon, not only bitmaps.
        /// </remarks>
        public static readonly ResourceKey IconControlStyle = new ComponentResourceKey(typeof(ResourceKeys), "IconControlStyle");
    }
}

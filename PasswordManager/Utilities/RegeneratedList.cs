using System.Collections.Generic;
using System.Diagnostics;

namespace PasswordManager.Utilities
{
    [DebuggerDisplay("Count = {_list.Count}")]
    public class RegeneratedList<T>
    {
        private readonly object _syncLock = new();
        private List<T> _list = new();

        /// <summary>
        /// Synchroniously add an element to list.
        /// </summary>
        /// <param name="item">Element to add.</param>
        public void Add(T item)
        {
            if (item is null)
                return;

            lock (_syncLock)
            {
                _list.Add(item);
            }
        }

        /// <summary>
        /// Synchroniously removes and returns the whole list.
        /// </summary>
        /// <returns>Current list or <see langword="null"/> if no elements presented.</returns>
        public List<T> PopAll()
        {
            List<T> result = null;

            lock (_syncLock)
            {
                if (_list.Count > 0)
                {
                    result = _list;
                    _list = new List<T>();
                }
            }

            return result;
        }
    }
}

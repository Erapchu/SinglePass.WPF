using System;
using System.Threading;

namespace PasswordManager.Helpers.Threading
{
    internal class DisposableLocker : IDisposable
    {
        private readonly EventWaitHandle _waitHandle;
        private bool _disposedValue;

        public DisposableLocker(object lockObject)
        {
            if (lockObject is null)
                throw new ArgumentNullException(nameof(lockObject));

            var whString = string.Format("{0}-{1}", lockObject.GetType(), lockObject.GetHashCode());
            _waitHandle = new EventWaitHandle(true, EventResetMode.AutoReset, whString, out bool newHandle);
            _waitHandle.WaitOne();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _waitHandle.Set();
                    _waitHandle.Dispose();
                }

                // free unmanaged resources (unmanaged objects)
                // set large fields to null
                _disposedValue = true;
            }
        }

        // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        //~DisposableLocker()
        //{
        //    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //    Dispose(disposing: false);
        //}

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

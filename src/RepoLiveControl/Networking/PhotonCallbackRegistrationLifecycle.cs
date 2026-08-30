using System;

namespace RepoLiveControl.Networking
{
    internal sealed class PhotonCallbackRegistrationLifecycle
    {
        private bool registered;
        private bool disposed;

        internal bool IsRegistered { get { return registered; } }

        internal bool IsDisposed { get { return disposed; } }

        internal void Synchronize(
            bool roomActive,
            Action register,
            Action unregister)
        {
            if (disposed)
                return;

            if (roomActive)
            {
                if (registered)
                    return;
                if (register == null)
                    throw new ArgumentNullException("register");

                register();
                registered = true;
                return;
            }

            if (!registered)
                return;
            if (unregister == null)
                throw new ArgumentNullException("unregister");

            unregister();
            registered = false;
        }

        internal void Dispose(Action unregister)
        {
            if (disposed)
                return;
            if (registered && unregister == null)
                throw new ArgumentNullException("unregister");

            disposed = true;
            if (!registered)
                return;

            try
            {
                unregister();
            }
            finally
            {
                registered = false;
            }
        }
    }
}

﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EFSecondLevelCache.Core
{
    /// <summary>
    /// Asynchronous version of the IEnumerator interface, allowing elements to be retrieved asynchronously.
    /// </summary>
    public sealed class EFAsyncTaskEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly Task<T> _task;
        private bool _moved;

        /// <summary>
        /// Asynchronous version of the IEnumerator interface
        /// </summary>
        public EFAsyncTaskEnumerator(Task<T> task)
        {
            _task = task;
        }

        /// <summary>
        /// Gets the current element in the iteration.
        /// </summary>
        public T Current => !_moved ? default(T) : _task.Result;

#if !NETSTANDARD2_1
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        void IDisposable.Dispose()
        {
        }
#endif

        /// <summary>
        /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
        /// </summary>
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_moved)
            {
                await _task.ConfigureAwait(false);

                _moved = true;

                return _moved;
            }

            return false;
        }

#if NETSTANDARD2_1
        /// <summary>
        /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
        /// </summary>
        public ValueTask<bool> MoveNextAsync()
        {
            return  new ValueTask<bool>(MoveNext(new CancellationToken()));
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            return default;
        }
#endif
    }
}
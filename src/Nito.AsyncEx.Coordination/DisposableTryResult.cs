using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
    public struct DisposableTryResult: IDisposable
    {
        public DisposableTryResult(IDisposable result)
        {
            Result = result;
        }

        public IDisposable Result { get; }

        public bool IsSuccess => Result != null;

        public void Dispose() => Result.Dispose();

        public static implicit operator bool(DisposableTryResult result)
        {
            return result.IsSuccess;
        }
    }
}

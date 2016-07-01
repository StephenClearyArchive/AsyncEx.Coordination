using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
    public struct TryResult<T>
    {
        public TryResult(bool isSuccess, T result)
        {
            IsSuccess = isSuccess;
            Result = result;
        }

        public bool IsSuccess { get; }

        public T Result { get; }

        public static implicit operator bool(TryResult<T> result)
        {
            return result.IsSuccess;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
    public struct TryResult
    {
        public TryResult(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }

        public bool IsSuccess { get; }

        public static implicit operator bool(TryResult result)
        {
            return result.IsSuccess;
        }
    }
}

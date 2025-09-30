using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using SuperSocket.ProtoBase;

namespace SuperSocket.Connection
{
    /// <summary>
    /// Represents a virtual connection with pipe-based data writing capabilities.
    /// </summary>
    public interface IVirtualConnection : IConnection
    {
        /// <summary>
        /// Writes data to the input pipe asynchronously.
        /// </summary>
        /// <param name="memory">The memory buffer containing the data to write.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A UniTask that represents the asynchronous write operation, including the flush result.</returns>
        UniTask<FlushResult> WriteInputPipeDataAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken);
    }
}

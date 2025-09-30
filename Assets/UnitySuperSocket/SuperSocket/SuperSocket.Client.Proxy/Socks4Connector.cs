using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using SuperSocket.Client;

namespace SuperSocket.Client.Proxy
{
    /// <summary>
    /// Represents a connector for SOCKS4 proxy connections.
    /// </summary>
    public class Socks4Connector : ConnectorBase
    {
        /// <summary>
        /// Connects to the specified remote endpoint through the SOCKS4 proxy.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
        /// <param name="state">The connection state.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A UniTask representing the asynchronous connection operation.</returns>
        protected override UniTask<ConnectState> ConnectAsync(EndPoint remoteEndPoint, ConnectState state, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

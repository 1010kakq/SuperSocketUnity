using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using SuperSocket.Log;

namespace SuperSocket.Client
{
    /// <summary>
    /// Represents a connector that establishes TCP socket connections.
    /// </summary>
    public class SocketConnector : ConnectorBase
    {
        /// <summary>
        /// Gets the local endpoint to bind the socket to.
        /// </summary>
        public IPEndPoint LocalEndPoint { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketConnector"/> class with default settings.
        /// </summary>
        public SocketConnector()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketConnector"/> class with the specified local endpoint.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind the socket to.</param>
        public SocketConnector(IPEndPoint localEndPoint)
            : base()
        {
            LocalEndPoint = localEndPoint;
        }

        /// <summary>
        /// Asynchronously connects to a remote endpoint using a TCP socket.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
        /// <param name="state">The connection state object.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A UniTask that represents the asynchronous connection operation.</returns>
        protected override async UniTask<ConnectState> ConnectAsync(EndPoint remoteEndPoint, ConnectState state, CancellationToken cancellationToken)
        {
            var addressFamily = remoteEndPoint.AddressFamily;

            if (addressFamily == AddressFamily.Unspecified)
                addressFamily = AddressFamily.InterNetwork;

            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogDebug($"SocketConnector ConnectAsync addressFamily: {addressFamily}");
            #endif
            var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                var localEndPoint = LocalEndPoint;

                if (localEndPoint != null)
                {
                    socket.ExclusiveAddressUse = false;
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                    socket.Bind(localEndPoint);
                }
#if NET5_0_OR_GREATER
                await socket.ConnectAsync(remoteEndPoint, cancellationToken);
#else
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogDebug($"SocketConnector ConnectAsync remoteEndPoint: {remoteEndPoint}");
                #endif
                // 使用UniTask替代Task和TaskCompletionSource
                UniTask connectTask = socket.ConnectAsync(remoteEndPoint).AsUniTask();
               
                var tcs = new UniTaskCompletionSource<bool>();
                cancellationToken.Register(() => tcs.TrySetResult(false));

                // 使用UniTask.WhenAny替代Task.WhenAny
                var winnerIndex = await UniTask.WhenAny(connectTask, tcs.Task);
                
                if (winnerIndex == 1) // tcs.Task完成
                {
                    // 取消操作完成
                    socket.Close();
                    return new ConnectState
                    {
                        Result = false,
                    };
                }
               
                if (!socket.Connected)
                {
                    socket.Close();

                    return new ConnectState
                    {
                        Result = false,
                    };
                }
#endif
            }
            catch (Exception e)
            {
                return new ConnectState
                {
                    Result = false,
                    Exception = e
                };
            }
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogDebug($"SocketConnector ConnectAsync socket.Connected: {socket.Connected}");
            #endif
            return new ConnectState
            {
                Result = true,
                Socket = socket
            };            
        }
    }
}

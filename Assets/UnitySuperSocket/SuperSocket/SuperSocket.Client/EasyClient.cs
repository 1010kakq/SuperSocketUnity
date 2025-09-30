using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Security.Authentication;
using SuperSocket.Log;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;
using System.IO.Compression;
using System.Linq;
using UnitySuperSocket;

namespace SuperSocket.Client
{
    /// <summary>
    /// Provides functionality for managing client connections and data transmission.
    /// </summary>
    public abstract class EasyClient : IEasyClient
    {
        /// <summary>
        /// Gets the current connection associated with the client.
        /// </summary>
        public IConnection Connection { get; set; }

        /// <summary>
        /// Gets or sets the proxy connector for the client.
        /// </summary>
        public IConnector Proxy { get; set; }

        /// <summary>
        /// Gets or sets the logger for the client.
        /// </summary>
        protected ILogger Logger { get; set; }

        /// <summary>
        /// Gets the connection options for the client.
        /// </summary>
        protected ConnectionOptions Options { get; private set; }

        /// <summary>
        /// Gets or sets the local endpoint for the client.
        /// </summary>
        public IPEndPoint LocalEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the security options for the client.
        /// </summary>
        public SecurityOptions Security { get; set; }

        /// <summary>
        /// Gets or sets the compression level for the client.
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.NoCompression;

        /// <summary>
        /// Gets or sets the size of the socket sender pool.
        /// </summary>
        public static int? SocketSenderPoolSize { get; set; }

        /// <summary>
        /// The default size of the socket sender pool.
        /// </summary>
        internal static readonly int DefaultSocketSenderPoolSize = 10;

        private bool _continuousReceivingStarted = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient"/> class with default settings.
        /// </summary>
        protected EasyClient()
            : this(new UnityLogger())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient"/> class with the specified logger.
        /// </summary>
        /// <param name="logger">The logger to use for the client.</param>
        protected EasyClient(ILogger logger)
            : this(new ConnectionOptions { Logger = logger })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient"/> class with the specified connection options.
        /// </summary>
        /// <param name="options">The connection options to use for the client.</param>
        public EasyClient(ConnectionOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            Options = options;
            Logger = options.Logger;
        }

        /// <summary>
        /// Gets the connector for the client.
        /// </summary>
        /// <returns>The connector to use for the client.</returns>
        protected virtual IConnector GetConnector()
        {
            var connectors = new List<IConnector>();

            if (Proxy is IConnector proxy)
            {
                connectors.Add(proxy);
            }
            else
            {
                connectors.Add(new SocketConnector(LocalEndPoint));
            }

            var security = Security;

            if (security != null)
            {
                if (security.EnabledSslProtocols != SslProtocols.None)
                    connectors.Add(new SslStreamConnector(security));
            }

            if (CompressionLevel != CompressionLevel.NoCompression)
            {
                connectors.Add(new GZipConnector(CompressionLevel));
            }

            return BuildConnectors(connectors);
        }

        /// <summary>
        /// Builds a chain of connectors from the specified collection.
        /// </summary>
        /// <param name="connectors">The collection of connectors to chain together.</param>
        /// <returns>The first connector in the chain.</returns>
        protected IConnector BuildConnectors(IEnumerable<IConnector> connectors)
        {
            var prevConnector = default(ConnectorBase);

            foreach (var connector in connectors)
            {
                if (prevConnector != null)
                {
                    prevConnector.NextConnector = connector;
                }

                prevConnector = connector as ConnectorBase;
            }

            return connectors.First();
        }

        UniTask<bool> IEasyClient.ConnectAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            return ConnectAsync(remoteEndPoint, cancellationToken);
        }

        /// <summary>
        /// Asynchronously connects to a remote endpoint using UniTask.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A UniTask that represents the asynchronous connection operation.</returns>
        protected virtual async UniTask<bool> ConnectAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            var connector = GetConnector();
            var state = await connector.ConnectAsync(remoteEndPoint, null, cancellationToken);
            if (state.Cancelled || cancellationToken.IsCancellationRequested)
            {
                OnError($"The connection to {remoteEndPoint} was cancelled.", state.Exception);
                return false;
            }

            if (!state.Result)
            {
                OnError($"Failed to connect to {remoteEndPoint}", state.Exception);
                return false;
            }

            var socket = state.Socket;

            if (socket == null)
                throw new Exception("Socket is null.");

            SetupConnection(state.CreateConnection(Options));
            return true;
        }

        /// <summary>
        /// Configures the client to use UDP for communication.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to communicate with.</param>
        /// <param name="bufferPool">The buffer pool to use for receiving data.</param>
        /// <param name="bufferSize">The size of the buffer to use for receiving data.</param>
        public void AsUdp(IPEndPoint remoteEndPoint, ArrayPool<byte> bufferPool = null, int bufferSize = 4096)
        {
            var localEndPoint = LocalEndPoint;

            if (localEndPoint == null)
            {
                localEndPoint = new IPEndPoint(remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            }

            var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            // bind the local endpoint
            socket.Bind(localEndPoint);

            var connection = new UdpPipeConnection(socket, this.Options, remoteEndPoint);

            SetupConnection(connection);

            UdpReceive(socket, connection, bufferPool, bufferSize);
        }

        private async void UdpReceive(Socket socket, UdpPipeConnection connection, ArrayPool<byte> bufferPool, int bufferSize)
        {
            if (bufferPool == null)
                bufferPool = ArrayPool<byte>.Shared;

            while (true)
            {
                var buffer = bufferPool.Rent(bufferSize);

                try
                {
                    var result = await socket
                        .ReceiveFromAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), SocketFlags.None, connection.RemoteEndPoint)
                        .ConfigureAwait(false);

                    await connection.WriteInputPipeDataAsync((new ArraySegment<byte>(buffer, 0, result.ReceivedBytes)).AsMemory(), CancellationToken.None);
                }
                catch (NullReferenceException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    OnError($"Failed to receive UDP data.", e);
                }
                finally
                {
                    bufferPool.Return(buffer);
                }
            }
        }

        /// <summary>
        /// Sets up the connection for the client.
        /// </summary>
        /// <param name="connection">The connection to set up.</param>
        protected virtual void SetupConnection(IConnection connection)
        {
            connection.Closed += OnConnectionClosed;
            Connection = connection;
        }

        void IEasyClient.StartReceive()
        {
            StartReceive();
        }

        /// <summary>
        /// Starts receiving data from the server.
        /// </summary>
        protected void StartReceive()
        {
            // 使用UniTask替换ContinueWith模式
            StartReceiveInternalAsync().Forget();
            _continuousReceivingStarted = true;
        }

        private async UniTask StartReceiveInternalAsync()
        {
            try
            {
                await StartReceiveAsync();
            }
            catch (OperationCanceledException)
            {
                OnError("The receive task was cancelled.");
            }
            catch (Exception ex)
            {
                OnError("Failed to start receive.", ex);
            }
        }

        /// <summary>
        /// Starts receiving data asynchronously.
        /// </summary>
        /// <returns>A UniTask that represents the asynchronous receive operation.</returns>
        protected abstract UniTask StartReceiveAsync();

        private void OnConnectionClosed(object sender, EventArgs e)
        {
            Connection.Closed -= OnConnectionClosed;
            OnClosed(this, e);
        }

        /// <summary>
        /// Handles the event when the client is closed.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnClosed(object sender, EventArgs e)
        {
            var handler = Closed;

            if (handler != null)
            {
                if (Interlocked.CompareExchange(ref Closed, null, handler) == handler)
                {
                    handler.Invoke(sender, e);
                }
            }
        }

        /// <summary>
        /// Handles errors that occur during client operations.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception that occurred.</param>
        protected virtual void OnError(string message, Exception exception)
        {
            Logger?.LogError(exception, message);
        }

        /// <summary>
        /// Handles errors that occur during client operations.
        /// </summary>
        /// <param name="message">The error message.</param>
        protected virtual void OnError(string message)
        {
            Logger?.LogError(message);
        }

        UniTask IEasyClient.SendAsync(ReadOnlyMemory<byte> data)
        {
            return SendAsync(data);
        }

        UniTask IEasyClient.SendAsync(ReadOnlySequence<byte> data)
        {
            return SendAsync(data);
        }

        /// <summary>
        /// Asynchronously sends data to the server.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <returns>A UniTask that represents the asynchronous send operation.</returns>
        protected virtual async UniTask SendAsync(ReadOnlyMemory<byte> data)
        {
            await Connection.SendAsync(data);
        }


        /// <summary>
        /// Asynchronously sends data to the server.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <returns>A UniTask that represents the asynchronous send operation.</returns>
        protected virtual async UniTask SendAsync(ReadOnlySequence<byte> data)
        {
            await Connection.SendAsync(data);
        }

        UniTask IEasyClient.SendAsync<TSendPackage>(IPackageEncoder<TSendPackage> packageEncoder, TSendPackage package)
        {
            return SendAsync<TSendPackage>(packageEncoder, package);
        }

        /// <summary>
        /// Asynchronously sends a package to the server.
        /// </summary>
        /// <typeparam name="TSendPackage">The type of the package to send.</typeparam>
        /// <param name="packageEncoder">The encoder to use for sending the package.</param>
        /// <param name="package">The package to send.</param>
        /// <returns>A UniTask that represents the asynchronous send operation.</returns>
        protected virtual async UniTask SendAsync<TSendPackage>(IPackageEncoder<TSendPackage> packageEncoder, TSendPackage package)
        {
            await Connection.SendAsync(packageEncoder, package);
        }

        /// <summary>
        /// Occurs when the client is closed.
        /// </summary>
        public event EventHandler Closed;

        /// <summary>
        /// Asynchronously closes the client connection using UniTask.
        /// </summary>
        /// <returns>A UniTask that represents the asynchronous close operation.</returns>
        public virtual async UniTask CloseAsync()
        {
            if(Connection==null) return;
            var closeTask = Connection.CloseAsync(CloseReason.LocalClosing);

            if (_continuousReceivingStarted)
            {
                await closeTask;
            }
            else
            {
                // 先启动接收处理，确保所有消息都被正确处��?
                var receiveTask = StartReceiveAsync();
                // 等待接收处理完成
                await receiveTask;
                  // 然后关闭连接
                await closeTask;
            }
        }

        public void Dispose()
        {
            // 使用Fire-and-forget模式避免阻塞主线��?
            CloseAsync().Forget();
        }

        public ValueTask DisposeAsync()
        {
            return CloseAsync().AsValueTask();
        }
    }

    /// <summary>
    /// Provides functionality for managing client connections and data transmission with a specific receive package type.
    /// </summary>
    /// <typeparam name="TReceivePackage">The type of the received package.</typeparam>
    public class EasyClient<TReceivePackage> : EasyClient, IEasyClient<TReceivePackage>
        where TReceivePackage : class
    {
        private IPipelineFilter<TReceivePackage> _pipelineFilter;

        IAsyncEnumerator<TReceivePackage> _packageStream;

        /// <summary>
        /// Occurs when a package is received from the server.
        /// </summary>
        public event PackageHandler<TReceivePackage> PackageHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient{TReceivePackage}"/> class with the specified pipeline filter.
        /// </summary>
        /// <param name="pipelineFilter">The pipeline filter to use for processing received packages.</param>
        public EasyClient(IPipelineFilter<TReceivePackage> pipelineFilter)
            : this(pipelineFilter, new UnityLogger())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient{TReceivePackage}"/> class with the specified pipeline filter and logger.
        /// </summary>
        /// <param name="pipelineFilter">The pipeline filter to use for processing received packages.</param>
        /// <param name="logger">The logger to use for the client.</param>
        public EasyClient(IPipelineFilter<TReceivePackage> pipelineFilter, ILogger logger)
            : this(pipelineFilter, new ConnectionOptions { Logger = logger })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient{TReceivePackage}"/> class with the specified pipeline filter and connection options.
        /// </summary>
        /// <param name="pipelineFilter">The pipeline filter to use for processing received packages.</param>
        /// <param name="options">The connection options to use for the client.</param>
        public EasyClient(IPipelineFilter<TReceivePackage> pipelineFilter, ConnectionOptions options)
            : base(options)
        {
            if (pipelineFilter == null)
                throw new ArgumentNullException(nameof(pipelineFilter));

            _pipelineFilter = pipelineFilter;
        }

        /// <summary>
        /// Returns the current client instance as an <see cref="IEasyClient{TReceivePackage}"/>.
        /// </summary>
        /// <returns>The current client instance.</returns>
        public virtual IEasyClient<TReceivePackage> AsClient()
        {
            return this;
        }

        /// <summary>
        /// Sets up the connection for the client.
        /// </summary>
        /// <param name="connection">The connection to set up.</param>
        protected override void SetupConnection(IConnection connection)
        {
            base.SetupConnection(connection);
            _packageStream = connection.GetPackageStream(_pipelineFilter);
        }

        UniTask<TReceivePackage> IEasyClient<TReceivePackage>.ReceiveAsync()
        {
            return ReceiveAsync();
        }

        /// <summary>
        /// Asynchronously receives a package from the server.
        /// </summary>
        /// <returns>A UniTask that represents the asynchronous receive operation.</returns>
        protected virtual async UniTask<TReceivePackage> ReceiveAsync()
        {
            var p = await _packageStream.ReceiveAsync();

            if (p != null)
                return p;

            // 当接收流结束时，通过连接对象触发关闭事件，而不是直接调用OnClosed
            // 这样可以保持事件机制的一致��?
            if (Connection != null && !Connection.IsClosed)
            {
                await Connection.CloseAsync(CloseReason.RemoteClosing);
            }
            return null;
        }

        /// <summary>
        /// Starts receiving data asynchronously.
        /// </summary>
        /// <returns>A UniTask that represents the asynchronous receive operation.</returns>
        protected override async UniTask StartReceiveAsync()
        {
            var enumerator = _packageStream;

            while (await enumerator.MoveNextAsync())
            {
                await OnPackageReceived(enumerator.Current);
            }
        }

        /// <summary>
        /// Handles the event when a package is received from the server.
        /// </summary>
        /// <param name="package">The received package.</param>
        /// <returns>A UniTask that represents the asynchronous handling operation.</returns>
        protected virtual async UniTask OnPackageReceived(TReceivePackage package)
        {
            var handler = PackageHandler;

            try
            {
                await handler.Invoke(this, package);
            }
            catch (Exception e)
            {
                OnError("Unhandled exception happened in PackageHandler.", e);
            }
        }
    }

    /// <summary>
    /// Provides functionality for managing client connections and data transmission with specific receive and send package types.
    /// </summary>
    /// <typeparam name="TPackage">The type of the received package.</typeparam>
    /// <typeparam name="TSendPackage">The type of the package to send.</typeparam>
    public class EasyClient<TPackage, TSendPackage> : EasyClient<TPackage>, IEasyClient<TPackage, TSendPackage>
        where TPackage : class
    {
        private IPackageEncoder<TSendPackage> _packageEncoder;

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient{TPackage, TSendPackage}"/> class with the specified pipeline filter and package encoder.
        /// </summary>
        /// <param name="pipelineFilter">The pipeline filter to use for processing received packages.</param>
        /// <param name="packageEncoder">The encoder to use for sending packages.</param>
        /// <param name="logger">The logger to use for the client.</param>
        public EasyClient(IPipelineFilter<TPackage> pipelineFilter, IPackageEncoder<TSendPackage> packageEncoder, ILogger logger = null)
            : this(pipelineFilter, packageEncoder, new ConnectionOptions { Logger = logger })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyClient{TPackage, TSendPackage}"/> class with the specified pipeline filter, package encoder, and connection options.
        /// </summary>
        /// <param name="pipelineFilter">The pipeline filter to use for processing received packages.</param>
        /// <param name="packageEncoder">The encoder to use for sending packages.</param>
        /// <param name="options">The connection options to use for the client.</param>
        public EasyClient(IPipelineFilter<TPackage> pipelineFilter, IPackageEncoder<TSendPackage> packageEncoder, ConnectionOptions options)
            : base(pipelineFilter, options)
        {
            _packageEncoder = packageEncoder;
        }

        /// <summary>
        /// Asynchronously sends a package to the server.
        /// </summary>
        /// <param name="package">The package to send.</param>
        /// <returns>A UniTask that represents the asynchronous send operation.</returns>
        public virtual async UniTask SendAsync(TSendPackage package)
        {
            await SendAsync(_packageEncoder, package);
        }

        /// <summary>
        /// Returns the current client instance as an <see cref="IEasyClient{TPackage, TSendPackage}"/>.
        /// </summary>
        /// <returns>The current client instance.</returns>
        public new IEasyClient<TPackage, TSendPackage> AsClient()
        {
            return this;
        }
    }
}

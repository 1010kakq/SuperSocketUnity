using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Buffers;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using SuperSocket.Log;

namespace SuperSocket.Connection
{
    /// <summary>
    /// Represents a pipe connection for managing TCP-based connections.
    /// </summary>
    public class TcpPipeConnection : PipeConnection
    {
        private Socket _socket;

        private readonly ObjectPool<SocketSender> _socketSenderPool;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpPipeConnection"/> class with the specified socket, options, and socket sender pool.
        /// </summary>
        /// <param name="socket">The TCP socket.</param>
        /// <param name="options">The connection options.</param>
        /// <param name="socketSenderPool">The pool of socket senders, or <c>null</c> to create new senders as needed.</param>
        public TcpPipeConnection(Socket socket, ConnectionOptions options, ObjectPool<SocketSender> socketSenderPool = null)
            : base(options)
        {
            _socket = socket;
            RemoteEndPoint = socket.RemoteEndPoint;
            LocalEndPoint = socket.LocalEndPoint;

            _socketSenderPool = socketSenderPool;
        }

        /// <summary>
        /// Handles the closure of the connection.
        /// </summary>
        protected override void OnClosed()
        {
            _socket = null;
            base.OnClosed();
        }

        /// <summary>
        /// Fills the pipe with data received from the socket asynchronously.
        /// </summary>
        /// <param name="memory">The memory buffer to fill with data.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The total number of bytes read.</returns>
        protected override async UniTask<int> FillInputPipeWithDataAsync(Memory<byte> memory, CancellationToken cancellationToken)
        {
            var bytesReceived = await ReceiveAsync(_socket, memory, SocketFlags.None, cancellationToken);

#if ENABLE_SUPERSOCKET_LOG
            // Log raw received data from socket
            if (bytesReceived > 0)
            {
                try
                {
                    var arraySegment = GetArrayByMemory(memory.Slice(0, bytesReceived));
                    NetLogUtil.LogRecvRaw(arraySegment.Array, arraySegment.Offset, bytesReceived);
                }
                catch (Exception e)
                {
                    NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Net Recv Raw Log Error] {e.Message}");
                }
            }
#endif

            return bytesReceived;
        }

        private async ValueTask<int> ReceiveAsync(Socket socket, Memory<byte> memory, SocketFlags socketFlags, CancellationToken cancellationToken)
        {
            var bytesReceived = await socket.ReceiveAsync(GetArrayByMemory(memory), socketFlags, cancellationToken);
            
#if ENABLE_SUPERSOCKET_LOG
            // 记录Socket.ReceiveAsync直接返回的原始数据
            if (bytesReceived > 0)
            {
                try
                {
                    var arraySegment = GetArrayByMemory(memory.Slice(0, bytesReceived));
                    string rawDataHex = NetLogUtil.HexDump(arraySegment.Array, arraySegment.Offset, Math.Min(bytesReceived, 64));
                    NetLogUtil.LogReceive($"[TcpPipeConnection.ReceiveAsync] Socket原始接收数据: bytesReceived={bytesReceived}\n{rawDataHex}");
                    
                    // 检查是否包含长度字段（前4字节）
                    if (bytesReceived >= 4)
                    {
                        int packetLength = System.BitConverter.ToInt32(arraySegment.Array, arraySegment.Offset);
                        NetLogUtil.LogReceive($"[TcpPipeConnection.ReceiveAsync] 数据包长度字段: {packetLength} (前4字节)");
                    }
                    
                    // 检查是否包含MsgId字段（第5-6字节）
                    if (bytesReceived >= 6)
                    {
                        ushort msgId = System.BitConverter.ToUInt16(arraySegment.Array, arraySegment.Offset + 4);
                        NetLogUtil.LogReceive($"[TcpPipeConnection.ReceiveAsync] MsgId字段: {msgId} (第5-6字节)");
                        
                        // 特别关注MsgId=499的情况
                        if (msgId == 499)
                        {
                            NetLogUtil.LogReceive($"[TcpPipeConnection.ReceiveAsync] *** 检测到MsgId=499数据包 ***");
                            NetLogUtil.LogReceive($"[TcpPipeConnection.ReceiveAsync] MsgId=499 完整数据 ({bytesReceived} bytes):\n{NetLogUtil.HexDump(arraySegment.Array, arraySegment.Offset, bytesReceived)}");
                        }
                    }
                }
                catch (Exception e)
                {
                    NetLogUtil.LogError($"[TcpPipeConnection.ReceiveAsync] 日志记录错误: {e.Message}");
                }
            }
#endif
            
            return bytesReceived;
        }

        /// <summary>
        /// Sends data over the connection asynchronously.
        /// </summary>
        /// <param name="buffer">The data to send.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The total number of bytes sent.</returns>
        protected override async UniTask<int> SendOverIOAsync(ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
        {
            var socketSenderPool = _socketSenderPool;
            var expectedBytes = (int)buffer.Length;

#if ENABLE_SUPERSOCKET_LOG
            // Debug: 记录接收到的buffer数据
            try
            {
                byte[] bufferDebugData = new byte[expectedBytes];
                buffer.CopyTo(bufferDebugData);
                string bufferHex = NetLogUtil.HexDump(bufferDebugData, 0, Math.Min(expectedBytes, 64));
                NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [TcpPipe Debug] LogSend buffer, len={expectedBytes}\\n{bufferHex}"); 
                    
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[TcpPipe Debug Error] {e.Message}");
            }
#endif

            var socketSender = socketSenderPool?.Get() ?? new SocketSender();

            try
            {
                var sentBytes = await socketSender.SendAsync(_socket, buffer);

#if ENABLE_SUPERSOCKET_LOG
                // Log send result
                try
                {
                    NetLogUtil.LogSendResultSuccess(sentBytes, expectedBytes);
                }
                catch (Exception e)
                {
                    NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Net Send Result Log Error] {e.Message}");
                }
#endif

                if (socketSenderPool != null)
                {
                    socketSenderPool.Return(socketSender);
                    socketSender = null;
                }

                return sentBytes;
            }
            catch (Exception ex)
            {
#if ENABLE_SUPERSOCKET_LOG
                // Log send failure
                try
                {
                    NetLogUtil.LogSendResultFail(ex);
                }
                catch (Exception e)
                {
                    NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Net Send Error Log Error] {e.Message}");
                }
#endif
                throw; // 重新抛出异常
            }
            finally
            {
                socketSender?.Dispose();
            }
        }

        /// <summary>
        /// Closes the connection by shutting down and closing the socket.
        /// </summary>
        protected override void Close()
        {
            var socket = _socket;

            if (socket == null)
                return;

            if (Interlocked.CompareExchange(ref _socket, null, socket) == socket)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                finally
                {
                    socket.Close();
                }
            }
        }

        /// <summary>
        /// Determines whether the specified exception is ignorable.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        /// <returns><c>true</c> if the exception is ignorable; otherwise, <c>false</c>.</returns>
        protected override bool IsIgnorableException(Exception e)
        {
            if (base.IsIgnorableException(e))
                return true;

            if (e is SocketException se)
            {
                if (se.IsIgnorableSocketException())
                    return true;
            }

            return false;
        }
    }
}

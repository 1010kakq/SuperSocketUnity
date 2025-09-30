using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;
using SuperSocket.Log;

namespace SuperSocket.Connection
{
    /// <summary>
    /// Represents a sender for asynchronous socket operations using <see cref="SocketAsyncEventArgs"/>.
    /// </summary>
    public class SocketSender : SocketAsyncEventArgs, IValueTaskSource<int>, IResettable
    {
        private readonly PipeScheduler _pipeScheduler;

        private Action<object> _continuation;

        private static readonly Action<object> _continuationCompleted = _ => { };

        private List<ArraySegment<byte>> _bufferList;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketSender"/> class.
        /// </summary>
        public SocketSender()
            : this(PipeScheduler.Inline)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketSender"/> class.
        /// </summary>
        public SocketSender(PipeScheduler pipeScheduler)
            : base()
        {
            _pipeScheduler = pipeScheduler;
        }

        /// <summary>
        /// Sends data asynchronously over the specified socket.
        /// </summary>
        /// <param name="socket">The socket to send data over.</param>
        /// <param name="buffer">The data to send.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous send operation.</returns>
        internal ValueTask<int> SendAsync(Socket socket, in ReadOnlySequence<byte> buffer)
        {
#if ENABLE_SUPERSOCKET_LOG
            // Log final socket send binary data
            try
            {
                LogSocketSendData(buffer);
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Socket Send Log Error] {e.Message}");
            }
#endif
            
            SetBuffer(buffer);

            if (socket.SendAsync(this))
            {
                return new ValueTask<int>(this, 0);
            }

            return SocketError != SocketError.Success
                ? new ValueTask<int>(Task.FromException<int>(new SocketException((int)SocketError)))
                : new ValueTask<int>(BytesTransferred);
        }

        private void SetBuffer(in ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                var segment = GetArrayByMemory(buffer.First);
                SetBuffer(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                var bufferList = _bufferList;

                if (bufferList == null)
                {
                    _bufferList = bufferList = new List<ArraySegment<byte>>();
                }

                foreach (var piece in buffer)
                {
                    bufferList.Add(GetArrayByMemory(piece));
                }

                BufferList = bufferList;
            }
        }

        /// <summary>
        /// Handles the completion of the asynchronous socket operation.
        /// </summary>
        /// <param name="e">The <see cref="SocketAsyncEventArgs"/> instance containing event data.</param>
        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
#if ENABLE_SUPERSOCKET_LOG
            // 记录Socket发送结�?
            try
            {
                if (SocketError == SocketError.Success)
                {
                    NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Socket Send Complete] bytesSent={BytesTransferred} success=true");
                }
                else
                {
                    NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Socket Send Complete] error={SocketError} success=false");
                    NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Socket Send Complete] error={SocketError} success=false");
                }
            }

            catch (Exception ex)
            {
               NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Socket Send Complete Log Error] {ex.Message}");
            }
#endif
            
            var continuation = Interlocked.CompareExchange(ref _continuation, _continuationCompleted, null);

            if (continuation == null)
            {
                // If the continuation is null, it means no continuation action to invoke
                // and the user token should be cleared if it was set.
                UserToken = null;
                return;
            }

            var state = UserToken;

            // Clear the UserToken to avoid being used twice
            UserToken = null;
            // Set the continuation to completed before queueing the work item
            _continuation = _continuationCompleted;

            _pipeScheduler.Schedule(continuation, state);
        }

        /// <summary>
        /// Gets the result of the asynchronous operation.
        /// </summary>
        /// <param name="token">The token associated with the operation.</param>
        /// <returns>The number of bytes transferred.</returns>
        public int GetResult(short token)
        {
            // Clear both continuation and state
            _continuation = null;
            UserToken = null;
            return BytesTransferred;
        }

        /// <summary>
        /// Gets the status of the asynchronous operation.
        /// </summary>
        /// <param name="token">The token associated with the operation.</param>
        /// <returns>The status of the operation.</returns>
        public ValueTaskSourceStatus GetStatus(short token)
        {
            if (!ReferenceEquals(_continuation, _continuationCompleted))
                return ValueTaskSourceStatus.Pending;

            return SocketError == SocketError.Success
                ? ValueTaskSourceStatus.Succeeded
                : ValueTaskSourceStatus.Faulted;
        }

        /// <summary>
        /// Schedules the continuation action for the asynchronous operation.
        /// </summary>
        /// <param name="continuation">The continuation action to invoke.</param>
        /// <param name="state">The state to pass to the continuation action.</param>
        /// <param name="token">The token associated with the operation.</param>
        /// <param name="flags">Flags that control the behavior of the continuation.</param>
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            // Store the state first
            UserToken = state;
            
            // Try to set the continuation
            var prevContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);

            // If the operation has already completed, the continuation would be _continuationCompleted
            if (ReferenceEquals(prevContinuation, _continuationCompleted))
            {
                // Clear the state since we'll invoke the continuation directly
                UserToken = null;
                
                // Queue the continuation for better performance
                ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(continuation), state);
            }
        }

        /// <summary>
        /// Attempts to reset the state of the sender.
        /// </summary>
        /// <returns><c>true</c> if the state was successfully reset; otherwise, <c>false</c>.</returns>
        public bool TryReset()
        {
            if (BufferList != null)
            {
                BufferList = null;
                _bufferList?.Clear();
            }
            else
            {
                SetBuffer(null, 0, 0);
            }

            return true;
        }

        private ArraySegment<byte> GetArrayByMemory(ReadOnlyMemory<byte> memory)
        {
            if (!MemoryMarshal.TryGetArray<byte>(memory, out var result))
            {
                throw new InvalidOperationException("Buffer backed by array was expected");
            }

            return result;
        }

#if ENABLE_SUPERSOCKET_LOG
        /// <summary>
        /// 记录Socket最终发送的二进制数�?
        /// </summary>
        /// <param name="buffer">要发送的数据缓冲�?/param>
        private void LogSocketSendData(in ReadOnlySequence<byte> buffer)
        {
            try
            {
                var totalLength = (int)buffer.Length;
                byte[] data;
                
                NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Socket Debug] Buffer info: Length={totalLength}, IsSingleSegment={buffer.IsSingleSegment}");

                if (buffer.IsSingleSegment)
                {
                    // 单段数据，直接转换
                    var segment = GetArrayByMemory(buffer.First);
                    NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Socket Debug] Single segment: Array.Length={segment.Array?.Length ?? 0}, Offset={segment.Offset}, Count={segment.Count}");
                    
                    data = new byte[totalLength];
                    System.Buffer.BlockCopy(segment.Array, segment.Offset, data, 0, segment.Count);
                }
                else
                {
                    // 多段数据，需要合并
                    data = new byte[totalLength];
                    int offset = 0;
                    int segmentCount = 0;
                    foreach (var piece in buffer)
                    {
                        var segment = GetArrayByMemory(piece);
                        NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Socket Debug] Segment {segmentCount++}: Array.Length={segment.Array?.Length ?? 0}, Offset={segment.Offset}, Count={segment.Count}, TargetOffset={offset}");
                        System.Buffer.BlockCopy(segment.Array, segment.Offset, data, offset, segment.Count);
                        offset += segment.Count;
                    }
                }

                // 使用NetLogUtil记录最终socket发送的数据
                string header = string.Format("[{0}] [Socket Send Raw] len={1} remote={2}",
                    NetLogUtil.Timestamp(), totalLength, "unknown");
                
                string hex = NetLogUtil.HexDump(data, 0, totalLength);
                NetLogUtil.LogSend(header + "\n" + hex);
            }
            catch (Exception ex)
            {
                NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Socket Send Log Error] {ex.Message}");
            }
        }
#endif
    }
}


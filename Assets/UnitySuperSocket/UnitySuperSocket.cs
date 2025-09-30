using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Collections.Concurrent;
using SuperSocket.Client;
using SuperSocket.ProtoBase;
using SuperSocket.Connection;
using UnityEngine;
using SuperSocket.Log;
using System.Net.Sockets;

namespace UnitySuperSocket
{
    public delegate void MsgPBCallbackDelegateCommon(int msgId, int client_sequeue_id, ushort serverId, object data);
	public delegate void MsgCommonCallbackDelegate(int msgId,int client_sequeue_id, int serverSequeue_id, ushort serverId, object data);
    /// <summary>
    /// 网络连接事件参数
    /// </summary>
    public class NetworkConnectEventArgs : EventArgs
    {
        public bool Success { get; }
        public string Host { get; }
        public int Port { get; }
        public string ErrorMessage { get; }
        
        public NetworkConnectEventArgs(bool success, string host, int port, string errorMessage = null)
        {
            Success = success;
            Host = host;
            Port = port;
            ErrorMessage = errorMessage;
        }
    }
    
    /// <summary>
    /// 网络事件类型
    /// </summary>
    public enum NetworkEventType
    {
        Connect,
        Disconnect
    }
    
    /// <summary>
    /// 网络事件消息
    /// </summary>
    public class NetworkEventMessage
    {
        public NetworkEventType EventType { get; }
        public EventArgs EventArgs { get; }
        public DateTime Timestamp { get; }
        
        public NetworkEventMessage(NetworkEventType eventType, EventArgs eventArgs)
        {
            EventType = eventType;
            EventArgs = eventArgs;
            Timestamp = DateTime.Now;
        }
    }
    
    
    /// <summary>
    /// 基于 SuperSocket 重构的网络连接管理类
    /// </summary>
    public partial class UnitySuperSocketClient : IDisposable
    {
        public byte version = 1;
        
        // SuperSocket 客户端
        private IEasyClient<GameReceivingPackage, GameSendingPackage> _client;
        private readonly ConcurrentQueue<GameReceivingPackage> _receivedMessages;
        
        // 解码器实例，用于释放对象到对象池
        private GameProtocolPackageDecoder _decoder;
        
        // 连接状态
        private string _currentHost;
        private int _currentPort;
        private bool _disposed = false;
        
        // 消息回调管理
        private Dictionary<int, MsgPBCallbackDelegateCommon> _msgCallbacks = new Dictionary<int, MsgPBCallbackDelegateCommon>();
        private MsgCommonCallbackDelegate _commonCallback = null;
        
        // 网络事件队列
        private readonly ConcurrentQueue<NetworkEventMessage> _networkEvents;
        
        // 网络事件处理
        public event EventHandler<NetworkConnectEventArgs> NetworkConnected;
        public event EventHandler<CloseEventArgs> NetworkDisconnected;
        
        /// <summary>
        /// 静态序列号计数- 使用原子操作保证线程安全
        /// </summary>
        private static int s_queue = 0;
        
        /// <summary>
        /// 获取下一个序列号
        /// </summary>
        /// <returns>线程安全的序列号</returns>
        private static int GetNextSequenceId()
        {
            return System.Threading.Interlocked.Increment(ref s_queue);
        }
        
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected 
        {
            get
            {
                if (_client == null)
                {
                    return false;
                }
                if (_client.Connection == null)
                {
                    return false;
                }
                return !_client.Connection.IsClosed;
            }
        }
        
        /// <summary>
        /// 初始化
        /// </summary>
        public UnitySuperSocketClient()
        {
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 构造函数调");
            #endif
            _receivedMessages = new ConcurrentQueue<GameReceivingPackage>();
            _networkEvents = new ConcurrentQueue<NetworkEventMessage>();
            InitializeClient();
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 初始化完");
            #endif
        }
        
        /// <summary>
        /// 初始化客户端 - 遵循 SuperSocket 标准模式
        /// </summary>
        private void InitializeClient()
        {
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始初始化客户端");
            #endif
            try
            {
                // 创建解码器实例
                _decoder = new GameProtocolPackageDecoder();
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 创建解码器完成");
                #endif
                // 创建管道过滤器和编码器
                var pipelineFilter = new GameProtocolPipelineFilter();
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 创建管道过滤器完成");
                #endif
                var packageEncoder = new GameProtocolPackageEncoder();
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 创建编码器完成");
                #endif
                
                // 创建 Unity Logger
                var unityLogger = new UnityLogger("ELEXNet_SuperSocket");
                
                // 创建 EasyClient，使用正确的构造函数（pipelineFilter + packageEncoder + logger）
                _client = new EasyClient<GameReceivingPackage, GameSendingPackage>(pipelineFilter, packageEncoder, unityLogger).AsClient();
                
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 创建客户端完成");
                #endif
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 初始化客户端失败: {ex}");
                #endif
                throw;
            }
        }
        
       
        
        /// <summary>
        /// 连接到服务器 - 异步版本（推荐使用）
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口</param>
        /// <returns>连接结果的UniTask</returns>
        public virtual async UniTask<bool> ConnectAsync(string host, int port)
        {
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始连接服务器: {host}:{port}");
            #endif
            
            // 异步断开之前的连接
            await DisconnectInternalAsync();
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 已断开之前的连接");
            #endif
            // 启动连接任务
            return await ConnectInternalAsync(host, port);
        }

        /// <summary>
        /// 连接到服务器 - 同步版本（不推荐，可能导致主线程阻塞）
        /// 建议使用 ConnectAsync 方法
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口</param>
        [System.Obsolete("Use ConnectAsync for non-blocking operation in Unity. This method may block the main thread.")]
        public virtual void Connect(string host, int port)
        {
            ConnectAsync(host, port).Forget(ex =>
            {
                if (ex != null)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogError($"[ELEXNet_SuperSocket] Connect异步操作失败: {ex}");
                    #endif
                }
            });
        }
        
        /// <summary>
        /// 连接到服务器 - 遵循 SuperSocket 标准模式
        /// </summary>
        /// <param name="host">服务器地址</param>
        /// <param name="port">服务器端口</param>
        /// <returns>连接任务</returns>
        private async UniTask<bool> ConnectInternalAsync(string host, int port)
        {
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始连接服务器: {host}:{port}");
            #endif
            
            try
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始新的连接");
                #endif
                
                _currentHost = host;
                _currentPort = port;
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 保存连接信息: Host={host}, Port={port}");
                #endif
                
                string address = parseIp(host);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(address), port);
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 创建远程端点: {endPoint}");
                #endif
                // 按照示例代码的模式连接
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始连接到服务");
                #endif
                
                var result = await _client.ConnectAsync(endPoint);
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 连接结果: {result}");
                #endif
                
                if (!result)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogError($"[ELEXNet_SuperSocket] 连接失败: Failed to connect the target server.");
                    #endif
                    
                    // 添加连接失败事件到队列
                    var connectArgs = new NetworkConnectEventArgs(false, host, port, "Failed to connect the target server");
                    _networkEvents.Enqueue(new NetworkEventMessage(NetworkEventType.Connect, connectArgs));
                    
                    return false;
                }
                 #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 连接成功");
                #endif
                // 添加连接成功事件到队列
                var successArgs = new NetworkConnectEventArgs(true, host, port);
                _networkEvents.Enqueue(new NetworkEventMessage(NetworkEventType.Connect, successArgs));
                
                // 设置包处理器
                _client.PackageHandler += OnPackageReceived;
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 已设置包处理器");
                #endif
                
                // 设置关闭事件处理器
                _client.Closed += OnClientClosed;
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 已设置关闭事件处理器");
                #endif
                
                // 按照 SuperSocket 标准方式启动接收
                _client.StartReceive();
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 已启动SuperSocket 标准接收模式");
                #endif
                
                return true;
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 连接异常: {ex.Message}");
                #endif
                
                // 添加连接错误事件到队列
                var errorArgs = new NetworkConnectEventArgs(false, host, port, ex.Message);
                _networkEvents.Enqueue(new NetworkEventMessage(NetworkEventType.Connect, errorArgs));
                
                return false;
            }
        }
        /// <summary>
        /// IPV6 支持;
        /// </summary>
        /// <param name="hostOrIp"></param>
        /// <returns></returns>
        private string parseIp(string hostOrIp)
        {
            string address = string.Empty;
            if (string.IsNullOrEmpty(hostOrIp))
                return address;
            IPAddress ipAddress = null;
            if (IPAddress.TryParse(hostOrIp, out ipAddress))
            {
                address = ipAddress.ToString();
                return address;
            }
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(hostOrIp);
                if (hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogError("can't find adress by hostname:" + hostOrIp);
                    #endif
                }
                else
                {
                    foreach (IPAddress ip in hostEntry.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            address = ip.ToString();
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(address))
                    {
                        foreach (IPAddress ip in hostEntry.AddressList)
                        {
                            if (ip.AddressFamily == AddressFamily.InterNetwork)
                            {
                                address = ip.ToString();
                                break;
                            }
                        }
                    }
                }
                if(string.IsNullOrEmpty(address))
                {
                    address = hostOrIp;
                }

            }
            catch (Exception e)
            {
                // 域名解析异常不往上报
                address = hostOrIp;
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError("parse host failed:" + hostOrIp + " " + e.Message);
                #endif
            }
			
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogInfo($"====>> parse game server ip:{address} {hostOrIp}");
            #endif
            return address;
        }
        /// <summary>
        /// 更新网络，处理接收到的消息和网络事件
        /// </summary>
        /// <param name="deltaTime">时间间隔</param>
        /// <returns>更新是否成功</returns>
        public bool UpdateNetwork(float deltaTime)
        {
            try
            {
                // 先处理接收到的数据包
                int messageCount = 0;
                while (TryGetReceivedMessage(out GameReceivingPackage package))
                {
                    messageCount++;
#if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogReceive($"[ELEXNet_SuperSocket] 处理接收消息 #{messageCount}: package={package.ToString()}");
#endif
                    ProcessReceivedMessage(package);
                }
                
                if (messageCount > 0)
                {
#if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogReceive($"[ELEXNet_SuperSocket] 本帧处理{messageCount} 条消息");
#endif
                }
                
                // 再处理网络事件（连接/断开状态变化）
                ProcessNetworkEvents();
                
                return true;
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 更新网络时出错: {ex}");
                #endif
                return false;
            }
        }
        
        /// <summary>
        /// 获取一条接收到的消息
        /// </summary>
        /// <param name="message">输出的消息</param>
        /// <returns>是否有消息</returns>
        private bool TryGetReceivedMessage(out GameReceivingPackage message)
        {
            return _receivedMessages.TryDequeue(out message);
        }
        
        /// <summary>
        /// 处理网络事件队列
        /// </summary>
        private void ProcessNetworkEvents()
        {
            while (_networkEvents.TryDequeue(out NetworkEventMessage eventMessage))
            {
                try
                {
                    switch (eventMessage.EventType)
                    {
                        case NetworkEventType.Connect:
                            NetworkConnected?.Invoke(this, eventMessage.EventArgs as NetworkConnectEventArgs);
                            break;
                        case NetworkEventType.Disconnect:
                            NetworkDisconnected?.Invoke(this, eventMessage.EventArgs as CloseEventArgs);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogError($"[ELEXNet_SuperSocket] 处理网络事件异常: {ex}");
                    #endif
                }
            }
        }
        
        /// <summary>
        /// SuperSocket 包处理器 - 处理接收到的消息
        /// </summary>
        /// <param name="sender">发送方客户端实�?/param>
        /// <param name="package">接收到的消息</param>
        /// <returns>处理任务</returns>
        private UniTask OnPackageReceived(EasyClient<GameReceivingPackage> sender, GameReceivingPackage package)
        {
            try
            {
                if (package != null)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogReceive($"[ELEXNet_SuperSocket] 包处理器接收到消息: MsgId={package.MessageId}, ClientSeqId={package.ClientSequenceId}, ServerId={package.ServerId}, DataLength={package.MessageDataLength}");
                    #endif
                    _receivedMessages.Enqueue(package);
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogReceive($"[ELEXNet_SuperSocket] 消息已加入队列，当前队列大小: {_receivedMessages.Count}");
                    #endif
                }
                else
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogError($"[ELEXNet_SuperSocket] 包处理器接收到空消息");
                    #endif
                }
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 包处理器异常: {ex}");
                #endif
            }
            
            return UniTask.CompletedTask;
        }
        
        /// <summary>
        /// SuperSocket 客户端关闭事件处理器 - �?socket 断开时自动调�?
        /// </summary>
        /// <param name="sender">发送方客户端实�?/param>
        /// <param name="e">关闭事件参数</param>
        private void OnClientClosed(object sender, EventArgs e)
        {
            try
            {
                
                // 添加断开连接事件到队列
                var closeEventArgs = e as CloseEventArgs ?? new CloseEventArgs(CloseReason.Unknown);
                _networkEvents.Enqueue(new NetworkEventMessage(NetworkEventType.Disconnect, closeEventArgs));
                
                // 自动设置连接状态为 false
                
                // 取消订阅事件处理器，避免重复触发
                if (_client != null)
                {
                    _client.Closed -= OnClientClosed;
                    _client.PackageHandler -= OnPackageReceived;
                }
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 客户端关闭事件处理异常: {ex}");
                #endif
            }
        }
        
        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        /// <param name="package">接收到的消息</param>
        private void ProcessReceivedMessage(GameReceivingPackage package)
        {
            try
            {
                // 使用传统BitMemStream 方式处理消息
                DisposeMessage(package.MessageId, package.ClientSequenceId, package.ServerSequenceId, 
                              package.PlayerUID, package.ServerId, package.MessageDataLength,package.MessageObject);
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 处理接收消息时出错: {ex}");
                #endif
            }
            finally
            {
                // 释放对象到对象池
                _decoder?.ReleasePackage(package);
            }
        }
        
        /// <summary>
        /// 处理消息分发
        /// </summary>
        /// <param name="msgId">消息ID</param>
        /// <param name="clientSeqId">客户端序列号</param>
        /// <param name="serverSeqId">服务器序列号</param>
        /// <param name="playerUID">玩家UID</param>
        /// <param name="serverId">服务器ID</param>
        /// <param name="messageData">消息数据</param>
        protected void DisposeMessage(int msgId, int clientSeqId, int serverSeqId, long playerUID, ushort serverId, int messageDataLength,object data)
        {
          
            try
            {
                if(_commonCallback != null)
			    {
				    _commonCallback(msgId, clientSeqId, serverSeqId, serverId, data);
			    }

                // C#回调处理
                if (_msgCallbacks.TryGetValue(msgId, out MsgPBCallbackDelegateCommon cSharpCallback) && cSharpCallback != null)
                {
                    cSharpCallback(msgId, clientSeqId, serverId, data);
                    return;
                }
            }
            finally
            {
                // BitMemStream 不需要手动释放，�?GC 自动回收
                // bitStream 会在作用域结束时自动释放
            }
        }
        
        /// <summary>
        /// 异步断开连接（推荐使用）
        /// </summary>
        /// <returns>断开连接的UniTask</returns>
        public async UniTask DisConnectAsync()
        {
           await DisconnectInternalAsync();
        }
        
        /// <summary>
        /// 断开连接（同步版本，不推荐）
        /// </summary>
        [System.Obsolete("Use DisConnectAsync for non-blocking operation in Unity.")]
        public void DisConnect()
        {
           DisconnectInternalAsync().Forget();
        }
        
        /// <summary>
        /// 异步断开超时（推荐使用）
        /// </summary>
        /// <returns>断开连接的UniTask</returns>
        public async UniTask DisConnectTimeoutAsync()
        {
            await DisconnectInternalAsync();
        }
        
        /// <summary>
        /// 断开超时（同步版本，不推荐）
        /// </summary>
        [System.Obsolete("Use DisConnectTimeoutAsync for non-blocking operation in Unity.")]
        public void DisConnectTimeout()
        {
            DisconnectInternalAsync().Forget();
        }
        
        /// <summary>
        /// 异步GM断开连接（推荐使用）
        /// </summary>
        /// <returns>断开连接的UniTask</returns>
        public async UniTask DisConnectGMAsync()
        {
            await DisconnectInternalAsync();
        }
        
        /// <summary>
        /// GM断开连接（同步版本，不推荐）
        /// </summary>
        [System.Obsolete("Use DisConnectGMAsync for non-blocking operation in Unity.")]
        public void DisConnectGM()
        {
            DisconnectInternalAsync().Forget();
        }
        
        /// <summary>
        /// 内部断开连接方法
        /// </summary>
        private async UniTask DisconnectInternalAsync()
        {
            try
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始断开连接");
                #endif
                if (_client != null)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始关闭客户端连接...");
                    #endif
                    await _client.CloseAsync();
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 客户端连接已关闭");
                    #endif
                }
                else
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogWarning($"[ELEXNet_SuperSocket] 客户端为空，无法关闭");
                    #endif
                }
                
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 断开连接异常: {ex}");
                #endif
            }
        }
        
        /// <summary>
        /// 发送ProtoBuf消息
        /// </summary>
        /// <typeparam name="T">消息类型</typeparam>
        /// <param name="msgId">消息ID</param>
        /// <param name="serverId">服务器ID</param>
        /// <param name="data">消息数据</param>
        /// <param name="sequenceId">返回的序列号</param>
        public void Send<T>(ushort msgId, ushort serverId, T data, out int sequenceId) where T : class
        {
            sequenceId = GetNextSequenceId(); // 线程安全地分配序列号
            int currentSeqId = sequenceId; // 复制到局部变�?
            
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogSend($"[ELEXNet_SuperSocket] 开始发送消息 MsgId={msgId}, ServerId={serverId}, SeqId={currentSeqId}, DataType={typeof(T).Name}");
            #endif
            
            // Fire-and-Forget 模式：直接调用非阻塞�?Send 方法
            try
            {
                var result = SendMessage(msgId, serverId, data, currentSeqId);
                if (!result)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogError($"[ELEXNet_SuperSocket] 发送消息失败 MsgId={msgId}, SeqId={currentSeqId}");
                    #endif
                }
                else
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogSend($"[ELEXNet_SuperSocket] 消息已提交发送队列 MsgId={msgId}, SeqId={currentSeqId}");
                    #endif
                }
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 启动发送消息异常 {ex}");
                #endif
            }
        }
        
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <typeparam name="T">消息类型</typeparam>
        /// <param name="messageId">消息ID</param>
        /// <param name="serverId">服务器ID</param>
        /// <param name="data">消息数据</param>
        /// <param name="sequenceId">预设的序列号</param>
        /// <returns>发送是否成功</returns>
        private bool SendMessage<T>(ushort messageId, ushort serverId, T data, int sequenceId) where T : class
        {
            if (_disposed)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogWarning($"[ELEXNet_SuperSocket] 对象已释放，无法发送消息");
                #endif
                return false;
            }
            
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogSend($"[ELEXNet_SuperSocket] 开始发送消息 MsgId={messageId}, ServerId={serverId}, SeqId={sequenceId}, DataType={typeof(T).Name}");
            #endif
            
            try
            {
                if (!IsConnected)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogWarning($"[ELEXNet_SuperSocket] 未连接到服务器，无法发送消息");
                    #endif
                    return false;
                }
                
                // 创建发送包（直接传protobuf 对象，在编码器中序列化）
                var packageInfo = GameSendingPackage.Create(messageId, sequenceId, serverId, data);
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogSend($"[ELEXNet_SuperSocket] 创建发送包完成: MsgId={messageId}, ServerId={serverId}, SeqId={sequenceId}, DataType={typeof(T).Name}");
                #endif
                
                // 发送消息（使用内置编码器）
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogSend($"[ELEXNet_SuperSocket] 开始异步发送消息..: MsgId={messageId}, ServerId={serverId}, SeqId={sequenceId}, DataType={typeof(T).Name}");
                #endif
                _client.SendAsync(packageInfo);
                
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogSend($"[ELEXNet_SuperSocket] 消息已提交发送队列: MsgId={messageId}, ServerId={serverId}, SeqId={sequenceId}, DataType={typeof(T).Name}");
                #endif
                return true;
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 发送消息失败: {ex}");
                #endif
                
                // 发送错误，触发断开连接
                return false;
            }
        }
        
        /// <summary>
        /// 发送缓冲区数据
        /// </summary>
        /// <param name="msgId">消息ID</param>
        /// <param name="serverId">服务器ID</param>
        /// <param name="buf">数据缓冲区</param>
        /// <param name="sequenceId">返回的序列号</param>
        public void SendBuffer(ushort msgId, ushort serverId, byte[] buf, out int sequenceId)
        {
            sequenceId = GetNextSequenceId(); // 线程安全地分配序列号
            int currentSeqId = sequenceId; // 复制到局部变量
            
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 开始发送缓冲区数据: MsgId={msgId}, ServerId={serverId}, SeqId={currentSeqId}, BufferLength={buf?.Length ?? 0}");
            #endif
            // Fire-and-Forget 模式：启动异步发送但不等待结�?
            try
            {
                UniTask.Run(() => 
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 异步发送缓冲区数据开始: MsgId={msgId}, SeqId={currentSeqId}");
                    #endif
                    var result = SendBufferData(msgId, serverId, buf, currentSeqId);
                    if (!result)
                    {
                        #if ENABLE_SUPERSOCKET_LOG
                        NetLogUtil.LogError($"[ELEXNet_SuperSocket] 发送缓冲区数据失败: MsgId={msgId}, SeqId={currentSeqId}");
                        #endif
                    }
                    else
                    {
                        #if ENABLE_SUPERSOCKET_LOG
                        NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 缓冲区数据发送成功: MsgId={msgId}, SeqId={currentSeqId}");
                        #endif
                    }
                }).Forget();
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogConnection($"[ELEXNet_SuperSocket] 已启动异步发送缓冲区数据任务");
                #endif
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[ELEXNet_SuperSocket] 启动发送缓冲区数据异常: {ex}");
                #endif
            }
        }
        
        /// <summary>
        /// 发送缓冲区数据
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="serverId">服务器ID</param>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="sequenceId">预设的序列号</param>
        /// <returns>发送是否成功</returns>
        private bool SendBufferData(ushort messageId, ushort serverId, byte[] buffer, int sequenceId)
        {
            try
            {
                if (!IsConnected)
                {
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogWarning($"[ELEXNet_SuperSocket] 未连接到服务器，无法发送消息");
                    #endif
                    return false;
                }
                
                var packageInfo = GameSendingPackage.CreateWithData(messageId, sequenceId, serverId, buffer);
                
                // 发送消息（使用内置编码器）
                _client.SendAsync(packageInfo);
                
                return true;
            }
            catch (Exception ex)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"发送缓冲区消息失败: {ex}");
                #endif
                return false;
            }
        }
        
        
        
        
        /// <summary>
        /// 注册C#层Stream协议
        /// </summary>
        /// <param name="msgId">消息ID</param>
        /// <param name="callback">回调函数</param>
        public void Register(int msgId, MsgPBCallbackDelegateCommon callback)
        {
            if (callback == null) { return; }
            _msgCallbacks[msgId] = callback;
        }
        
        public void RegisterCommonCallback(MsgCommonCallbackDelegate callback)
		{
			_commonCallback -= callback;
			_commonCallback += callback;
		}
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 受保护的 Dispose 方法
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    
                    if (_client != null)
                    {
                        try
                        {
                            // 取消订阅事件处理�?
                            _client.PackageHandler -= OnPackageReceived;
                            _client.Closed -= OnClientClosed;
                            
                            // 非阻塞方式关闭连接，避免阻塞主线�?
                            _client.CloseAsync().Forget();
                        }
                        catch (Exception ex)
                        {
                            #if ENABLE_SUPERSOCKET_LOG
                            NetLogUtil.LogError($"关闭客户端异常 {ex}");
                            #endif
                        }
                        finally
                        {
                            _client = null;
                        }
                    }
                    
                    // 释放解码�?
                   // _decoder?.Dispose();
                   // _decoder = null;
                    
                    // 清空消息队列
                    while (_receivedMessages.TryDequeue(out _)) { }
                    
                    // 清空网络事件队列
                    while (_networkEvents.TryDequeue(out _)) { }
                    
                    _msgCallbacks?.Clear();
                }
                
                _disposed = true;
            }
        }
    }
}

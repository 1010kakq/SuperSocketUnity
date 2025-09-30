using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnitySuperSocket;
using ELEXNetwork;

namespace Samples.ProtoBufChat
{
    /// <summary>
    /// ProtoBuf聊天客户端示例
    /// 演示如何使用ProtoBuf协议进行聊天通信
    /// </summary>
    public class ProtoBufChatClient : MonoBehaviour
    {
        [Header("连接配置")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 8080;
        
        [Header("用户信息")]
        [SerializeField] private string username = "TestPlayer";
        [SerializeField] private string password = "123456";
        
        [Header("聊天配置")]
        [SerializeField] private ChatChannelType currentChannel = ChatChannelType.World;
        [SerializeField] private bool enableAutoHeartbeat = true;
        [SerializeField] private float heartbeatInterval = 30f;
        
        // 消息ID定义
        private const ushort MSG_LOGIN_REQUEST = 1001;
        private const ushort MSG_LOGIN_RESPONSE = 1002;
        private const ushort MSG_CHAT_MESSAGE = 2001;
        private const ushort MSG_HEARTBEAT = 9001;
        private const ushort MSG_ERROR_RESPONSE = 9999;
        
        private UnitySuperSocketClient client;
        private bool isLoggedIn = false;
        private PlayerInfo currentPlayer;
        private float lastHeartbeatTime;
        
        // UI状态
        private string chatInput = "";
        private string chatHistory = "";
        private Vector2 scrollPosition;
        
        async void Start()
        {
            // 配置日志
            NetLogUtil.Configure(
                enableConnection: true,
                enableDebug: false,
                enableSend: false,
                enableReceive: false,
                enableInfo: true,
                enableWarning: true
            );
            
            InitializeNetworking();
            await ConnectAndLogin();
        }
        
        void InitializeNetworking()
        {
            // 创建客户端
            client = new UnitySuperSocketClient();
            
            // 注册ProtoBuf消息类型
            RegisterMessageTypes();
            
            // 注册消息处理器
            client.Register(MSG_LOGIN_RESPONSE, OnLoginResponse);
            client.Register(MSG_CHAT_MESSAGE, OnChatMessage);
            client.Register(MSG_ERROR_RESPONSE, OnErrorResponse);
            
            // 注册网络事件
            NetManager.Instance.NetworkConnected += OnNetworkConnected;
            NetManager.Instance.NetworkDisconnected += OnNetworkDisconnected;
            
            Debug.Log("[ProtoBufChatClient] 网络初始化完成");
        }
        
        void RegisterMessageTypes()
        {
            // 注册所有ProtoBuf消息类型到解码器
            GameProtocolPackageDecoder.RegisterMessageType(MSG_LOGIN_REQUEST, typeof(LoginRequest));
            GameProtocolPackageDecoder.RegisterMessageType(MSG_LOGIN_RESPONSE, typeof(LoginResponse));
            GameProtocolPackageDecoder.RegisterMessageType(MSG_CHAT_MESSAGE, typeof(ChatMessage));
            GameProtocolPackageDecoder.RegisterMessageType(MSG_HEARTBEAT, typeof(HeartbeatMessage));
            GameProtocolPackageDecoder.RegisterMessageType(MSG_ERROR_RESPONSE, typeof(ErrorResponse));
            
            Debug.Log("[ProtoBufChatClient] ProtoBuf消息类型注册完成");
        }
        
        async UniTask ConnectAndLogin()
        {
            try
            {
                // 连接服务器
                AddChatMessage("系统", "正在连接服务器...", ChatChannelType.System);
                var connected = await client.ConnectAsync(serverHost, serverPort);
                
                if (!connected)
                {
                    AddChatMessage("系统", "连接服务器失败", ChatChannelType.System);
                    return;
                }
            }
            catch (Exception ex)
            {
                AddChatMessage("系统", $"连接异常: {ex.Message}", ChatChannelType.System);
            }
        }
        
        private void OnNetworkConnected(object sender, NetworkConnectEventArgs e)
        {
            if (e.Success)
            {
                AddChatMessage("系统", $"已连接到服务器: {e.Host}:{e.Port}", ChatChannelType.System);
                
                // 自动发送登录请求
                SendLoginRequest();
            }
            else
            {
                AddChatMessage("系统", $"连接失败: {e.ErrorMessage}", ChatChannelType.System);
            }
        }
        
        private void OnNetworkDisconnected(object sender, CloseEventArgs e)
        {
            isLoggedIn = false;
            AddChatMessage("系统", $"与服务器断开连接: {e.Reason}", ChatChannelType.System);
        }
        
        void SendLoginRequest()
        {
            var loginRequest = new LoginRequest
            {
                Username = username,
                Password = password,
                ClientVersion = Application.version
            };
            
            client.Send<LoginRequest>(MSG_LOGIN_REQUEST, 1, loginRequest, out int seqId);
            AddChatMessage("系统", $"正在登录用户: {username}", ChatChannelType.System);
        }
        
        private void OnLoginResponse(int msgId, int clientSeqId, ushort serverId, object data)
        {
            var response = data as LoginResponse;
            if (response == null)
            {
                AddChatMessage("系统", "登录响应数据无效", ChatChannelType.System);
                return;
            }
            
            if (response.Success)
            {
                isLoggedIn = true;
                currentPlayer = new PlayerInfo
                {
                    PlayerId = response.PlayerId,
                    PlayerName = response.PlayerName,
                    IsOnline = true
                };
                
                AddChatMessage("系统", $"登录成功! 欢迎 {response.PlayerName}", ChatChannelType.System);
                
                // 启动心跳
                if (enableAutoHeartbeat)
                {
                    lastHeartbeatTime = Time.time;
                }
            }
            else
            {
                AddChatMessage("系统", $"登录失败: {response.Message}", ChatChannelType.System);
            }
        }
        
        private void OnChatMessage(int msgId, int clientSeqId, ushort serverId, object data)
        {
            var chatMessage = data as ChatMessage;
            if (chatMessage == null) return;
            
            AddChatMessage(chatMessage.PlayerName, chatMessage.Content, chatMessage.Channel);
        }
        
        private void OnErrorResponse(int msgId, int clientSeqId, ushort serverId, object data)
        {
            var errorResponse = data as ErrorResponse;
            if (errorResponse == null) return;
            
            AddChatMessage("系统", $"错误 [{errorResponse.ErrorCode}]: {errorResponse.ErrorMessage}", ChatChannelType.System);
        }
        
        void AddChatMessage(string playerName, string content, ChatChannelType channel)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var channelText = GetChannelDisplayName(channel);
            var message = $"[{timestamp}] [{channelText}] {playerName}: {content}\n";
            
            chatHistory += message;
            
            // 限制聊天记录长度
            if (chatHistory.Length > 2000)
            {
                var lines = chatHistory.Split('\n');
                if (lines.Length > 50)
                {
                    chatHistory = string.Join("\n", lines, lines.Length - 40, 40);
                }
            }
            
            Debug.Log($"[Chat] {message.TrimEnd()}");
            
            // 自动滚动到底部
            scrollPosition.y = float.MaxValue;
        }
        
        string GetChannelDisplayName(ChatChannelType channel)
        {
            switch (channel)
            {
                case ChatChannelType.World: return "世界";
                case ChatChannelType.Team: return "队伍";
                case ChatChannelType.Private: return "私聊";
                case ChatChannelType.System: return "系统";
                default: return "未知";
            }
        }
        
        void SendChatMessage()
        {
            if (!isLoggedIn || string.IsNullOrWhiteSpace(chatInput))
                return;
            
            var chatMessage = new ChatMessage
            {
                PlayerName = currentPlayer.PlayerName,
                Content = chatInput.Trim(),
                Channel = currentChannel,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            
            client.Send<ChatMessage>(MSG_CHAT_MESSAGE, 1, chatMessage, out int seqId);
            chatInput = "";
        }
        
        void SendHeartbeat()
        {
            if (!isLoggedIn) return;
            
            var heartbeat = new HeartbeatMessage
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ClientInfo = $"Unity_{SystemInfo.deviceModel}"
            };
            
            client.Send<HeartbeatMessage>(MSG_HEARTBEAT, 1, heartbeat, out int seqId);
        }
        
        void Update()
        {
            if (client != null)
            {
                client.UpdateNetwork(Time.deltaTime);
                
                // 自动心跳
                if (enableAutoHeartbeat && isLoggedIn && Time.time - lastHeartbeatTime > heartbeatInterval)
                {
                    SendHeartbeat();
                    lastHeartbeatTime = Time.time;
                }
            }
            
            // 回车发送消息
            if (Input.GetKeyDown(KeyCode.Return) && GUI.GetNameOfFocusedControl() == "ChatInput")
            {
                SendChatMessage();
            }
        }
        
        void OnGUI()
        {
            // 主聊天窗口
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
            GUILayout.BeginVertical();
            
            // 标题
            GUILayout.Label("ProtoBuf聊天客户端示例", GUI.skin.box);
            
            // 连接状态
            var statusColor = isLoggedIn ? Color.green : Color.red;
            var oldColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label($"状态: {(isLoggedIn ? "✅ 已登录" : "❌ 未登录")}", GUI.skin.box);
            GUI.color = oldColor;
            
            GUILayout.Space(10);
            
            // 聊天记录区域
            GUILayout.Label("聊天记录:");
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            GUILayout.TextArea(chatHistory, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            // 频道选择
            GUILayout.BeginHorizontal();
            GUILayout.Label("频道:", GUILayout.Width(50));
            currentChannel = (ChatChannelType)GUILayout.SelectionGrid(
                (int)currentChannel, 
                new string[] { "世界", "队伍", "私聊" }, 
                3,
                GUILayout.Height(30)
            );
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // 消息输入
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("ChatInput");
            chatInput = GUILayout.TextField(chatInput, GUILayout.ExpandWidth(true));
            
            GUI.enabled = isLoggedIn && !string.IsNullOrWhiteSpace(chatInput);
            if (GUILayout.Button("发送", GUILayout.Width(60)))
            {
                SendChatMessage();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // 控制按钮
            GUILayout.BeginHorizontal();
            if (!isLoggedIn)
            {
                if (GUILayout.Button("重新连接"))
                {
                    ConnectAndLogin().Forget();
                }
            }
            else
            {
                if (GUILayout.Button("断开连接"))
                {
                    client?.DisConnect();
                }
                
                if (GUILayout.Button("发送心跳"))
                {
                    SendHeartbeat();
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        void OnDestroy()
        {
            // 取消事件订阅
            if (NetManager.Instance != null)
            {
                NetManager.Instance.NetworkConnected -= OnNetworkConnected;
                NetManager.Instance.NetworkDisconnected -= OnNetworkDisconnected;
            }
            
            // 断开连接
            client?.DisConnect();
            
            Debug.Log("[ProtoBufChatClient] 资源已清理");
        }
    }
}
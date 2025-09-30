using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnitySuperSocket;
using ELEXNetwork;

namespace Samples.BasicClient
{
    /// <summary>
    /// 基础网络客户端示例
    /// 演示如何连接服务器、发送接收消息的基本操作
    /// </summary>
    public class BasicNetworkClient : MonoBehaviour
    {
        [Header("连接配置")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 8080;
        [SerializeField] private bool enableDetailedLogs = true;
        
        [Header("消息测试")]
        [SerializeField] private string testMessage = "Hello Server!";
        [SerializeField] private bool autoSendTestMessage = true;
        [SerializeField] private float sendInterval = 5f;
        
        private UnitySuperSocketClient client;
        private bool isConnected = false;
        private float lastSendTime;
        
        async void Start()
        {
            // 配置日志
            if (enableDetailedLogs)
            {
                NetLogUtil.Configure(
                    enableConnection: true,
                    enableDebug: true,
                    enableSend: true,
                    enableReceive: true,
                    enableInfo: true,
                    enableWarning: true
                );
                Debug.Log("[BasicNetworkClient] 详细日志已启用");
            }
            
            // 创建客户端
            client = new UnitySuperSocketClient();
            
            // 注册网络事件
            NetManager.Instance.NetworkConnected += OnNetworkConnected;
            NetManager.Instance.NetworkDisconnected += OnNetworkDisconnected;
            
            // 连接服务器
            Debug.Log($"[BasicNetworkClient] 开始连接服务器: {serverHost}:{serverPort}");
            await ConnectToServer();
        }
        
        async UniTask ConnectToServer()
        {
            try
            {
                var connected = await client.ConnectAsync(serverHost, serverPort);
                if (!connected)
                {
                    Debug.LogError("[BasicNetworkClient] 连接失败，请检查服务器是否正常运行");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BasicNetworkClient] 连接异常: {ex.Message}");
            }
        }
        
        private void OnNetworkConnected(object sender, NetworkConnectEventArgs e)
        {
            if (e.Success)
            {
                isConnected = true;
                Debug.Log($"[BasicNetworkClient] ✅ 连接成功: {e.Host}:{e.Port}");
                
                if (autoSendTestMessage)
                {
                    SendTestMessage();
                }
            }
            else
            {
                isConnected = false;
                Debug.LogError($"[BasicNetworkClient] ❌ 连接失败: {e.ErrorMessage}");
            }
        }
        
        private void OnNetworkDisconnected(object sender, CloseEventArgs e)
        {
            isConnected = false;
            Debug.LogWarning($"[BasicNetworkClient] 🔌 连接断开: {e.Reason}");
        }
        
        private void SendTestMessage()
        {
            if (!isConnected)
            {
                Debug.LogWarning("[BasicNetworkClient] 未连接到服务器，无法发送消息");
                return;
            }
            
            try
            {
                // 将字符串转换为字节数组
                var messageBytes = System.Text.Encoding.UTF8.GetBytes(testMessage);
                
                // 发送消息
                client.SendBuffer(1001, 1, messageBytes, out int sequenceId);
                
                Debug.Log($"[BasicNetworkClient] 📤 发送消息: \"{testMessage}\" (序列号: {sequenceId})");
                lastSendTime = Time.time;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BasicNetworkClient] 发送消息失败: {ex.Message}");
            }
        }
        
        void Update()
        {
            if (client != null)
            {
                // 更新网络状态
                client.UpdateNetwork(Time.deltaTime);
                
                // 自动发送测试消息
                if (autoSendTestMessage && isConnected && Time.time - lastSendTime > sendInterval)
                {
                    SendTestMessage();
                }
            }
        }
        
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical();
            
            GUILayout.Label($"连接状态: {(isConnected ? "✅ 已连接" : "❌ 未连接")}", 
                isConnected ? GUI.skin.box : GUI.skin.label);
            
            GUILayout.Space(10);
            
            if (!isConnected)
            {
                if (GUILayout.Button("重新连接"))
                {
                    ConnectToServer().Forget();
                }
            }
            else
            {
                if (GUILayout.Button("断开连接"))
                {
                    client?.DisConnect();
                }
                
                GUILayout.Space(10);
                testMessage = GUILayout.TextField(testMessage);
                
                if (GUILayout.Button("发送测试消息"))
                {
                    SendTestMessage();
                }
                
                autoSendTestMessage = GUILayout.Toggle(autoSendTestMessage, "自动发送消息");
            }
            
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
            
            Debug.Log("[BasicNetworkClient] 资源已清理");
        }
        
        void OnApplicationQuit()
        {
            client?.DisConnect();
        }
    }
}
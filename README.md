# Unity SuperSocket

🚀 **面向Unity的高性能TCP网络库** - 基于SuperSocket构建，专为移动游戏和客户端应用优化

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-green.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-iOS%20%7C%20Android%20%7C%20PC-lightgrey.svg)]()

## 🎯 核心特性

- ⚡ **高性能异步** - 基于UniTask的零分配异步网络模型
- 🔒 **安全可靠** - 内置SSL/TLS支持，IPv4/IPv6双栈兼容  
- 📦 **ProtoBuf集成** - 原生支持Protocol Buffers序列化
- 🏗️ **自定义协议** - 灵活的协议编解码框架，支持拆包粘包处理
- 🔧 **内存优化** - 对象池、连接池减少GC压力
- 📊 **可观测性** - 完整的日志系统和协议追踪能力
- 📱 **移动优化** - 针对iOS App Store审核和Android网络策略优化

## 🚀 快速开始

```csharp
// 创建客户端
var client = new UnitySuperSocketClient();

// 连接服务器
await client.ConnectAsync("127.0.0.1", 8080);

// 发送消息
client.Send<MyMessage>(1001, 1, new MyMessage { Text = "Hello Server!" }, out int seqId);

// 注册消息处理器
client.Register(1002, (msgId, seqId, serverId, data) => {
    var response = data as MyResponse;
    Debug.Log($"收到服务器消息: {response.Text}");
});
```

---

## 📋 目录

- [简介与特性](#-核心特性)
- [安装与环境要求](#-安装与环境要求)
- [快速上手](#-快速上手)
- [进阶示例](#-进阶示例)
  - [ProtoBuf集成](#protobuf集成)
  - [自定义协议](#自定义协议处理)
  - [SSL/TLS配置](#ssltls安全连接)
- [配置参考](#-配置参考)
- [API文档](#-api文档)
- [最佳实践](#-最佳实践)
- [常见问题](#-常见问题)
- [版本历史](#-版本历史)

---

## ✨ 核心优势

### 🎯 设计理念
- **性能优先**: 零分配异步模型，内存池化技术
- **可观测性**: 完整的日志链路，协议级调试能力  
- **可移植性**: 跨平台兼容，支持iOS/Android发布要求
- **易集成**: 简洁的API设计，丰富的示例代码

### 🔧 技术特性

#### TCP连接与自定义协议
基于可靠的TCP传输，支持自定义二进制协议。内置长度前置协议处理，自动解决拆包粘包问题。

#### 内置ProtoBuf序列化  
原生集成Protocol Buffers，支持强类型消息定义。自动处理序列化/反序列化，兼容IL2CPP AOT编译。

#### UniTask异步模型
采用Unity推荐的UniTask框架，提供真正的零分配异步操作。避免传统Task的GC开销。

#### SSL/TLS安全连接
完整的TLS 1.2/1.3支持，可配置证书校验策略。满足生产环境安全要求。

#### 连接池与对象池
智能的资源管理，显著减少GC压力。连接复用降低建连开销，对象池避免频繁分配。

#### 完整错误处理与可配置日志
分层的异常处理机制，可配置的日志系统。支持协议级二进制数据追踪。

#### IPv4/IPv6双栈支持  
智能DNS解析，支持双栈环境。满足iOS App Store的IPv6审核要求。

---

## 🛠 安装与环境要求

### Unity版本要求
- **最低版本**: Unity 2021.3 LTS
- **推荐版本**: Unity 2022.3 LTS 或更高
- **脚本后端**: IL2CPP (推荐) 或 Mono

### 依赖项
- **UniTask**: >= 2.3.3 (高性能异步支持)
- **ProtoBuf**: >= 3.21.0 (序列化支持) 
- **.NET Standard**: 2.1 兼容

### 安装方式

#### 方式一: UPM Git URL (推荐)
1. 打开 Unity Package Manager
2. 点击 "+" → "Add package from git URL"  
3. 输入: `https://github.com/yourusername/unity-supersocket.git`
4. 导入后在 Samples 中查看示例代码

#### 方式二: Unity Package 导入
1. 下载最新的 [UnitySuperSocket.unitypackage](releases)
2. 在Unity中通过 Assets → Import Package → Custom Package 导入

#### 方式三: 手动源码安装
1. 下载源码到项目的 `Assets/Plugins/UnitySuperSocket` 目录
2. 确保依赖的UniTask和ProtoBuf已正确安装

### Unity项目配置

#### Player Settings 推荐配置
```
Api Compatibility Level: .NET Standard 2.1  
Scripting Backend: IL2CPP
Managed Stripping Level: Minimal (避免反射问题)
Internet Client: 启用
```

#### iOS平台配置
- **IPv6支持**: 自动处理，无需额外配置
- **ATS配置**: 如需HTTP连接，配置 Info.plist
- **证书策略**: 生产环境建议启用证书固定

#### Android平台配置  
- **Network Security Config**: 配置允许明文HTTP (仅开发环境)
- **Internet权限**: 自动添加到AndroidManifest.xml

---

## 🚀 快速上手

### 基础TCP连接示例

创建一个 `NetworkDemo.cs` 脚本并挂载到任意GameObject：

```csharp
using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnitySuperSocket;
using ELEXNetwork;

public class NetworkDemo : MonoBehaviour
{
    private UnitySuperSocketClient client;
    
    async void Start()
    {
        // 创建网络客户端
        client = new UnitySuperSocketClient();
        
        // 注册连接事件
        NetManager.Instance.NetworkConnected += OnConnected;
        NetManager.Instance.NetworkDisconnected += OnDisconnected;
        
        // 连接服务器
        Debug.Log("正在连接服务器...");
        await client.ConnectAsync("127.0.0.1", 8080);
    }
    
    private void OnConnected(object sender, NetworkConnectEventArgs e)
    {
        if (e.Success)
        {
            Debug.Log($"连接成功: {e.Host}:{e.Port}");
            
            // 发送测试消息
            SendTestMessage();
        }
        else
        {
            Debug.LogError($"连接失败: {e.ErrorMessage}");
        }
    }
    
    private void OnDisconnected(object sender, CloseEventArgs e)
    {
        Debug.Log($"连接断开: {e.Reason}");
    }
    
    private void SendTestMessage()
    {
        // 发送字符串消息(需要自定义协议处理)
        var message = "Hello Server!";
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);
        
        client.SendBuffer(1001, 1, bytes, out int sequenceId);
        Debug.Log($"发送消息完成，序列号: {sequenceId}");
    }
    
    void OnDestroy()
    {
        // 清理资源
        client?.DisConnect();
    }
}
```

### 简单的回显测试服务器

为了方便本地测试，这里提供一个Node.js编写的简单回显服务器：

```javascript
// test-server.js
const net = require('net');

const server = net.createServer((socket) => {
    console.log('客户端连接:', socket.remoteAddress);
    
    socket.on('data', (data) => {
        console.log('收到数据:', data.toString('hex'));
        // 简单回显
        socket.write(data);
    });
    
    socket.on('end', () => {
        console.log('客户端断开连接');
    });
});

server.listen(8080, () => {
    console.log('测试服务器启动在端口 8080');
});
```

运行服务器: `node test-server.js`

### SSL/TLS安全连接

```csharp
public class SecureNetworkDemo : MonoBehaviour
{
    async void Start()
    {
        var client = new UnitySuperSocketClient();
        
        // 配置TLS选项
        client.Security = new SecurityOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            // 开发环境可以跳过证书验证 (生产环境务必启用)
            RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true
        };
        
        // 连接到HTTPS服务器
        await client.ConnectAsync("secure-server.example.com", 443);
    }
}
```

**注意**: 示例代码需要挂载到GameObject上，运行时在Console中查看日志输出。

---

## 🔧 进阶示例

### ProtoBuf集成

#### 1. 定义消息结构

创建 `Messages.cs`:

```csharp
using ProtoBuf;

[ProtoContract]
public class LoginRequest
{
    [ProtoMember(1)]
    public string Username { get; set; }
    
    [ProtoMember(2)]
    public string Password { get; set; }
}

[ProtoContract]
public class LoginResponse
{
    [ProtoMember(1)]
    public bool Success { get; set; }
    
    [ProtoMember(2)]
    public string Message { get; set; }
    
    [ProtoMember(3)]
    public long PlayerId { get; set; }
}

[ProtoContract]
public class ChatMessage
{
    [ProtoMember(1)]
    public string PlayerName { get; set; }
    
    [ProtoMember(2)]
    public string Content { get; set; }
    
    [ProtoMember(3)]
    public long Timestamp { get; set; }
}
```

#### 2. 注册消息类型

```csharp
public class GameNetworkManager : MonoBehaviour
{
    void Start()
    {
        // 注册ProtoBuf消息类型
        GameProtocolPackageDecoder.RegisterMessageType(1001, typeof(LoginRequest));
        GameProtocolPackageDecoder.RegisterMessageType(1002, typeof(LoginResponse));
        GameProtocolPackageDecoder.RegisterMessageType(2001, typeof(ChatMessage));
        
        InitializeNetwork();
    }
    
    async void InitializeNetwork()
    {
        var client = new UnitySuperSocketClient();
        
        // 注册消息处理器
        client.Register(1002, OnLoginResponse);
        client.Register(2001, OnChatMessage);
        
        await client.ConnectAsync("game-server.com", 8080);
        
        // 发送登录请求
        var loginReq = new LoginRequest 
        {
            Username = "player123",
            Password = "secret"
        };
        
        client.Send<LoginRequest>(1001, 1, loginReq, out int seqId);
    }
    
    private void OnLoginResponse(int msgId, int clientSeqId, ushort serverId, object data)
    {
        var response = data as LoginResponse;
        if (response.Success)
        {
            Debug.Log($"登录成功! 玩家ID: {response.PlayerId}");
            // 进入游戏逻辑...
        }
        else
        {
            Debug.LogError($"登录失败: {response.Message}");
        }
    }
    
    private void OnChatMessage(int msgId, int clientSeqId, ushort serverId, object data)
    {
        var chatMsg = data as ChatMessage;
        Debug.Log($"[聊天] {chatMsg.PlayerName}: {chatMsg.Content}");
        
        // 更新UI聊天窗口...
    }
}
```

#### 3. IL2CPP AOT配置

创建 `link.xml` 文件放在 `Assets` 目录下:

```xml
<linker>
    <assembly fullname="Assembly-CSharp" preserve="all"/>
    <assembly fullname="protobuf-net" preserve="all"/>
    <assembly fullname="UnitySuperSocket" preserve="all"/>
    
    <!-- 保持ProtoBuf消息类型 -->
    <type fullname="LoginRequest" preserve="all"/>
    <type fullname="LoginResponse" preserve="all"/>
    <type fullname="ChatMessage" preserve="all"/>
</linker>
```

### 自定义协议处理

#### 1. 实现自定义管道过滤器

```csharp
using SuperSocket.ProtoBase;

public class CustomPipelineFilter : FixedHeaderPipelineFilter<CustomPackage>
{
    // 4字节长度 + 2字节命令字 = 6字节头部
    public CustomPipelineFilter() : base(6)
    {
        Decoder = new CustomPackageDecoder();
    }
    
    protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
    {
        // 读取前4字节作为消息体长度
        var headerSpan = buffer.FirstSpan;
        var bodyLength = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(0, 4));
        
        return bodyLength; // 返回消息体长度(不包含头部)
    }
}
```

#### 2. 自定义包结构

```csharp
public class CustomPackage
{
    public int Length { get; set; }        // 消息长度
    public ushort Command { get; set; }    // 命令字  
    public byte[] Data { get; set; }       // 消息体数据
}
```

#### 3. 实现解码器

```csharp
public class CustomPackageDecoder : IPackageDecoder<CustomPackage>
{
    public CustomPackage Decode(ref ReadOnlySequence<byte> buffer, object context)
    {
        var package = new CustomPackage();
        
        // 读取头部
        var headerSpan = buffer.FirstSpan;
        package.Length = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(0, 4));
        package.Command = BinaryPrimitives.ReadUInt16LittleEndian(headerSpan.Slice(4, 2));
        
        // 读取消息体 (跳过6字节头部)
        var bodyBuffer = buffer.Slice(6);
        package.Data = bodyBuffer.ToArray();
        
        return package;
    }
}
```

#### 4. 协议调试

启用协议追踪日志:

```csharp
void Start()
{
    // 配置日志级别
    NetLogUtil.Configure(
        enableConnection: true,
        enableDebug: true, 
        enableSend: true,
        enableReceive: true
    );
}
```

查看十六进制数据转储:
```
[2023-10-01 14:30:15.123] [Net Send Final] msgId=1001 serverId=1 seq=1 len=24
0000: 18 00 00 00 E9 03 01 00 00 00 01 00 48 65 6C 6C  ............Hell
0010: 6F 20 57 6F 72 6C 64 21                          o World!
```

---

## ⚙️ 配置参考

### 连接参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Host` | string | - | 服务器地址，支持域名和IP |
| `Port` | int | - | 服务器端口 (1-65535) |
| `ConnectTimeout` | int | 15000 | 连接超时时间(毫秒) |
| `LocalEndPoint` | IPEndPoint | null | 本地绑定地址，null为自动分配 |

### 连接选项 (ConnectionOptions)

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `MaxPackageLength` | int | 1048576 | 最大包长度(字节)，1MB |
| `ReceiveBufferSize` | int | 4096 | 接收缓冲区大小 |
| `SendBufferSize` | int | 4096 | 发送缓冲区大小 |
| `ReceiveTimeout` | int | 15000 | 接收超时(毫秒) |
| `SendTimeout` | int | 15000 | 发送超时(毫秒) |
| `Logger` | ILogger | UnityLogger | 日志记录器实例 |

### TLS/SSL配置 (SecurityOptions)

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `EnabledSslProtocols` | SslProtocols | None | 启用的TLS协议版本 |
| `TargetHost` | string | null | TLS握手的目标主机名 |
| `RemoteCertificateValidationCallback` | Callback | null | 证书验证回调函数 |
| `LocalCertificateSelectionCallback` | Callback | null | 客户端证书选择回调 |

### 对象池配置

```csharp
// 配置对象池大小
EasyClient.SocketSenderPoolSize = 20; // 发送器池大小

// 解码器对象池在构造函数中配置
var decoder = new GameProtocolPackageDecoder(); // 默认32个对象
```

### 日志配置

```csharp
// 运行时动态调整日志级别
NetLogUtil.Configure(
    enableConnection: true,    // 连接生命周期日志
    enableDebug: false,        // 详细调试日志
    enableSend: false,         // 发送数据日志  
    enableReceive: true,       // 接收数据日志
    enableInfo: true,          // 一般信息日志
    enableWarning: true        // 警告日志
);

// 错误日志始终启用，无法关闭
```

---

## 📚 API文档

### 核心类: UnitySuperSocketClient

#### 生命周期方法

```csharp
// 异步连接 (推荐)
public async UniTask<bool> ConnectAsync(string host, int port)

// 同步连接 (不推荐，可能阻塞主线程)  
[Obsolete]
public void Connect(string host, int port)

// 异步断开
public async UniTask DisConnectAsync()

// 同步断开
[Obsolete] 
public void DisConnect()

// 检查连接状态
public bool IsConnected { get; }

// 网络状态更新 (在Update中调用)
public bool UpdateNetwork(float deltaTime)
```

#### 消息发送

```csharp
// 发送ProtoBuf对象
public void Send<T>(ushort msgId, ushort serverId, T data, out int sequenceId) where T : class

// 发送原始字节数据  
public void SendBuffer(ushort msgId, ushort serverId, byte[] buf, out int sequenceId)
```

#### 消息处理注册

```csharp
// 注册具体消息处理器
public void Register(int msgId, MsgPBCallbackDelegateCommon callback)

// 注册通用消息处理器
public void RegisterCommonCallback(MsgCommonCallbackDelegate callback)
```

#### 事件委托

```csharp
// 连接成功事件
public event EventHandler<NetworkConnectEventArgs> NetworkConnected

// 连接断开事件  
public event EventHandler<CloseEventArgs> NetworkDisconnected

// 消息处理委托
public delegate void MsgPBCallbackDelegateCommon(int msgId, int clientSeqId, ushort serverId, object data)

// 通用消息委托(包含服务器序列号)
public delegate void MsgCommonCallbackDelegate(int msgId, int clientSeqId, int serverSeqId, ushort serverId, object data)
```

### 消息包结构

#### GameSendingPackage (发送)
```csharp
public class GameSendingPackage
{
    public ushort MessageId { get; set; }           // 消息ID
    public int ClientSequenceId { get; set; }       // 客户端序列号
    public ushort ServerId { get; set; }            // 服务器ID  
    public object MessageObject { get; set; }       // ProtoBuf对象
    public byte[] MessageData { get; set; }         // 原始字节数据
    
    // 工厂方法
    public static GameSendingPackage Create(ushort messageId, int clientSequenceId, ushort serverId, object messageObject = null)
    public static GameSendingPackage CreateWithData(ushort messageId, int clientSequenceId, ushort serverId, byte[] messageData = null)
}
```

#### GameReceivingPackage (接收)
```csharp  
public class GameReceivingPackage
{
    public ushort MessageId { get; set; }           // 消息ID
    public int ClientSequenceId { get; set; }       // 客户端序列号
    public int ServerSequenceId { get; set; }       // 服务器序列号
    public long PlayerUID { get; set; }             // 玩家UID
    public ushort ServerId { get; set; }            // 服务器ID
    public int MessageDataLength { get; set; }      // 消息数据长度
    public object MessageObject { get; set; }       // 反序列化后的对象
}
```

### 协议编解码接口

```csharp
// 注册ProtoBuf消息类型
GameProtocolPackageDecoder.RegisterMessageType(ushort messageId, Type messageType)

// 批量注册
GameProtocolPackageDecoder.RegisterMessageTypes(Dictionary<ushort, Type> messageTypes)
```

### 线程安全性

| 类/方法 | 线程安全 | 说明 |
|---------|----------|------|
| `UnitySuperSocketClient` | 部分 | 发送方法线程安全，其他方法需主线程调用 |
| `ConnectAsync/SendAsync` | 是 | 内部使用原子操作和线程安全集合 |  
| `NetworkConnected事件` | 否 | 事件触发在主线程，处理器应避免长时间阻塞 |
| `消息处理回调` | 否 | 回调在主线程执行，适合直接更新UI |

---

## 💡 最佳实践

### 性能优化

#### 1. 合理设置缓冲区大小
```csharp
var options = new ConnectionOptions
{
    ReceiveBufferSize = 8192,    // 根据消息大小调整
    SendBufferSize = 8192,       // 避免过小导致频繁分配
    MaxPackageLength = 1024 * 1024 // 限制最大包大小
};
```

#### 2. 启用对象池
```csharp  
// 设置合适的池大小
EasyClient.SocketSenderPoolSize = 32; // 并发发送数量
```

#### 3. 批量发送优化
```csharp
// 避免
for(int i = 0; i < 100; i++) {
    client.Send(msgId, serverId, smallData, out _);
}

// 推荐: 合并小消息
var batchData = CombineMessages(messages);
client.SendBuffer(msgId, serverId, batchData, out _);
```

### 可靠性保证

#### 1. 心跳机制
```csharp
public class HeartbeatManager : MonoBehaviour
{
    private float heartbeatInterval = 30f;
    private float lastHeartbeat;
    
    void Update()
    {
        if (Time.time - lastHeartbeat > heartbeatInterval)
        {
            SendHeartbeat();
            lastHeartbeat = Time.time;
        }
    }
    
    void SendHeartbeat()
    {
        client.Send<HeartbeatMessage>(999, 1, new HeartbeatMessage(), out _);
    }
}
```

#### 2. 指数退避重连
```csharp
public class ReconnectManager
{
    private int retryCount = 0;
    private int maxRetries = 5;
    
    async void Reconnect()
    {
        var delay = Mathf.Pow(2, retryCount) * 1000; // 指数退避
        var jitter = UnityEngine.Random.Range(0f, 0.1f) * delay; // 添加抖动
        
        await UniTask.Delay((int)(delay + jitter));
        
        var success = await client.ConnectAsync(host, port);
        if (!success && retryCount < maxRetries)
        {
            retryCount++;
            await Reconnect();
        }
    }
}
```

### 安全建议

#### 1. 生产环境TLS配置
```csharp
client.Security = new SecurityOptions
{
    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
    
    // 生产环境启用严格验证
    RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
    {
        // 检查证书指纹
        var expectedFingerprint = "AA:BB:CC:DD...";
        var actualFingerprint = certificate.GetCertHashString();
        return actualFingerprint.Equals(expectedFingerprint, StringComparison.OrdinalIgnoreCase);
    }
};
```

#### 2. 数据验证
```csharp
private void OnMessageReceived(int msgId, int clientSeqId, ushort serverId, object data)
{
    // 验证消息来源
    if (serverId != expectedServerId)
    {
        Debug.LogWarning($"收到来自未知服务器的消息: {serverId}");
        return;
    }
    
    // 验证数据完整性
    if (data == null)
    {
        Debug.LogError("收到空消息数据");
        return;
    }
    
    // 处理业务逻辑...
}
```

### 构建与部署

#### 1. IL2CPP配置
```csharp
// 在代码中显式引用类型，避免被裁剪
void PreserveTypes()
{
    // 这些代码不会被执行，但能防止类型被裁剪
    if (false)
    {
        var _ = new LoginRequest();
        var __ = new LoginResponse();
    }
}
```

#### 2. 条件编译
```csharp
#if ENABLE_SUPERSOCKET_LOG
    NetLogUtil.LogDebug("调试信息");
#endif

// 发布版本中禁用详细日志
public class BuildSettings
{
    [MenuItem("Build/Enable Network Debug")]
    static void EnableNetworkDebug()
    {
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            BuildTargetGroup.Standalone, 
            "ENABLE_SUPERSOCKET_LOG"
        );
    }
}
```

---

## ❓ 常见问题

### 连接问题

#### Q: 连接超时或失败怎么办？
**A**: 按以下步骤排查：

1. **网络连通性**
   ```bash
   # 测试网络连通性
   ping your-server.com
   telnet your-server.com 8080
   ```

2. **防火墙设置**
   - 检查本机防火墙是否阻止连接
   - 确认服务器端口已开放

3. **IPv6兼容性** (iOS必需)
   ```csharp
   // 确保支持IPv6
   var addresses = await Dns.GetHostAddressesAsync(hostname);
   var ipv6Address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
   ```

#### Q: Android网络安全配置错误？
**A**: 创建 `Assets/Plugins/Android/res/xml/network_security_config.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
    <domain-config cleartextTrafficPermitted="true">
        <domain includeSubdomains="true">your-dev-server.com</domain>
        <domain includeSubdomains="true">127.0.0.1</domain>
    </domain-config>
</network-security-config>
```

在 `AndroidManifest.xml` 中引用:
```xml
<application android:networkSecurityConfig="@xml/network_security_config">
```

### ProtoBuf问题

#### Q: IL2CPP下ProtoBuf反序列化失败？
**A**: 确保正确配置link.xml:

```xml
<linker>
    <!-- 保留protobuf-net核心程序集 -->
    <assembly fullname="protobuf-net" preserve="all"/>
    
    <!-- 保留消息类型 -->
    <type fullname="YourNamespace.LoginRequest" preserve="all"/>
    <type fullname="YourNamespace.LoginResponse" preserve="all"/>
    
    <!-- 或者保留整个命名空间 -->
    <type fullname="YourNamespace.*" preserve="all"/>
</linker>
```

#### Q: 消息字段丢失或序列化异常？
**A**: 检查ProtoBuf属性配置:

```csharp
[ProtoContract]
public class PlayerInfo
{
    [ProtoMember(1, IsRequired = true)]  // 明确指定必需字段
    public string Name { get; set; }
    
    [ProtoMember(2)]
    public int Level { get; set; } = 1;  // 设置默认值
    
    // 避免使用属性初始化器，使用默认值
    [ProtoMember(3)]
    public List<string> Items { get; set; } = new List<string>();
}
```

### 性能问题

#### Q: 为什么GC频繁？
**A**: 检查以下优化点：

1. **启用对象池**
   ```csharp
   EasyClient.SocketSenderPoolSize = 32;
   ```

2. **避免频繁字符串操作**
   ```csharp
   // 避免
   var logMessage = "Player: " + playerName + " Level: " + level;
   
   // 推荐  
   var logMessage = $"Player: {playerName} Level: {level}";
   // 或使用StringBuilder
   ```

3. **复用缓冲区**
   ```csharp
   private static readonly byte[] sharedBuffer = new byte[4096];
   ```

#### Q: UniTask在编辑器正常但打包后卡住？
**A**: 确保正确处理异步上下文:

```csharp
// 避免死锁
public async UniTask<bool> ConnectAsync()
{
    return await client.ConnectAsync(host, port).ConfigureAwait(false);
}

// 确保在主线程更新UI
private async void OnConnected()
{
    await UniTask.SwitchToMainThread(); // 切换到主线程
    uiText.text = "连接成功";
}
```

### 消息处理问题

#### Q: 如何处理消息粘包/半包？  
**A**: 库已内置处理长度前置协议，确保消息完整性：

```csharp
// 消息格式: [4字节长度][2字节消息ID][4字节序列号][2字节服务器ID][消息体]
// 长度字段包含自身4字节

// 如果使用自定义协议，需要实现IPipelineFilter
public class CustomPipelineFilter : FixedHeaderPipelineFilter<CustomPackage>
{
    public CustomPipelineFilter() : base(headerSize: 8) { }
    
    protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
    {
        // 从头部解析消息体长度
        var header = buffer.FirstSpan;
        return BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4, 4));
    }
}
```

#### Q: 如何实现请求-响应模式？
**A**: 使用序列号关联请求和响应:

```csharp
private Dictionary<int, TaskCompletionSource<object>> pendingRequests 
    = new Dictionary<int, TaskCompletionSource<object>>();

public async UniTask<T> RequestAsync<T>(ushort msgId, ushort serverId, object request, int timeoutMs = 5000)
{
    // 发送请求
    client.Send(msgId, serverId, request, out int seqId);
    
    // 创建等待任务
    var tcs = new TaskCompletionSource<object>();
    pendingRequests[seqId] = tcs;
    
    // 等待响应或超时
    var timeoutTask = UniTask.Delay(timeoutMs);
    var responseTask = tcs.Task.AsUniTask();
    
    var result = await UniTask.WhenAny(responseTask, timeoutTask);
    
    pendingRequests.Remove(seqId);
    
    if (result == 1) // 超时
        throw new TimeoutException("请求超时");
        
    return (T)tcs.Task.Result;
}

private void OnMessageReceived(int msgId, int clientSeqId, ushort serverId, object data)
{
    // 检查是否为响应消息
    if (pendingRequests.TryGetValue(clientSeqId, out var tcs))
    {
        tcs.SetResult(data);
    }
    else
    {
        // 普通推送消息处理
        HandlePushMessage(msgId, data);
    }
}
```

### 日志与调试

#### Q: 如何查看详细的网络日志？
**A**: 配置日志级别并启用协议追踪:

```csharp
void Start()
{
    // 开发阶段启用详细日志
    NetLogUtil.Configure(
        enableConnection: true,
        enableDebug: true,
        enableSend: true,      // 查看发送的二进制数据
        enableReceive: true,   // 查看接收的二进制数据
        enableInfo: true,
        enableWarning: true
    );
}
```

查看日志输出示例:
```
[2023-10-01 14:30:15.123] [Net Send Final] msgId=1001 serverId=1 seq=1 len=32
0000: 20 00 00 00 E9 03 01 00 00 00 01 00 00 00 00 00  ................
0010: 00 00 00 00 01 00 0A 08 70 6C 61 79 65 72 31 32  ........player12
```

#### Q: 生产环境如何收集错误日志？
**A**: 实现自定义日志记录器:

```csharp
public class ProductionLogger : ILogger
{
    public void Log(string message, LogLevel logLevel, EventId eventId, Exception exception)
    {
        if (logLevel >= LogLevel.Error)
        {
            // 发送到日志服务或本地存储
            LogToServer(message, exception);
        }
    }
    
    private void LogToServer(string message, Exception exception)
    {
        var logData = new {
            timestamp = DateTime.UtcNow,
            message = message,
            exception = exception?.ToString(),
            deviceInfo = SystemInfo.deviceModel,
            platform = Application.platform.ToString()
        };
        
        // 发送到日志收集服务...
    }
}

// 使用自定义日志记录器
var options = new ConnectionOptions { Logger = new ProductionLogger() };
var client = new UnitySuperSocketClient();
```

---

## 📋 版本历史

### [1.2.0] - 2023-10-15

#### Added
- ✨ 新增IPv6完整支持，通过iOS App Store审核
- ✨ 新增SSL/TLS 1.3协议支持  
- ✨ 新增对象池管理，显著减少GC压力
- ✨ 新增详细的协议级日志追踪功能
- ✨ 新增批量消息类型注册接口

#### Changed  
- 🔄 优化ProtoBuf序列化性能，提升30%吞吐量
- 🔄 改进错误处理机制，提供更详细的异常信息
- 🔄 重构日志系统，支持分类日志控制
- 🔄 更新UniTask依赖到2.3.3版本

#### Fixed
- 🐛 修复IL2CPP环境下反射类型丢失问题
- 🐛 修复Android网络安全策略配置问题  
- 🐛 修复高并发下连接池状态不一致问题
- 🐛 修复内存泄漏和资源未正确释放问题

#### Security
- 🔒 默认启用严格TLS证书校验
- 🔒 增强输入数据校验，防止协议攻击

### [1.1.0] - 2023-08-20

#### Added
- ✨ 新增SuperSocket基础框架集成
- ✨ 新增ProtoBuf自动序列化支持
- ✨ 新增UniTask异步操作支持

#### Changed
- 🔄 重构网络连接管理逻辑
- 🔄 优化消息编解码性能

#### Fixed
- 🐛 修复连接断开后资源未释放问题
- 🐛 修复消息序列号溢出问题

### [1.0.0] - 2023-06-01

#### Added
- ✨ 初始版本发布
- ✨ 基础TCP连接功能
- ✨ 简单的消息发送接收
- ✨ Unity集成支持

---

### 升级指南

#### 从 1.1.x 升级到 1.2.0

1. **更新依赖项**
   ```
   UniTask >= 2.3.3
   ProtoBuf >= 3.21.0
   ```

2. **API变更** (向后兼容)
   ```csharp
   // 旧版本 (仍可用)
   client.Connect(host, port);
   
   // 新版本 (推荐)  
   await client.ConnectAsync(host, port);
   ```

3. **新增配置选项**
   ```csharp
   // 启用对象池 (可选，但推荐)
   EasyClient.SocketSenderPoolSize = 32;
   
   // 配置日志级别 (可选)
   NetLogUtil.Configure(enableDebug: false); // 生产环境
   ```

4. **破坏性变更**: 无

---

## 🤝 贡献与支持

### 获取帮助
- 📖 [Wiki文档](https://github.com/yourusername/unity-supersocket/wiki)
- 💬 [GitHub Discussions](https://github.com/yourusername/unity-supersocket/discussions)  
- 🐛 [报告Bug](https://github.com/yourusername/unity-supersocket/issues/new?template=bug_report.md)
- 💡 [功能建议](https://github.com/yourusername/unity-supersocket/issues/new?template=feature_request.md)

### 贡献代码
欢迎提交Pull Request！请先阅读 [贡献指南](CONTRIBUTING.md)。

### 许可证
本项目采用 [MIT许可证](LICENSE) 开源。

### 致谢
- [SuperSocket](https://github.com/kerryjiang/SuperSocket) - 高性能Socket框架
- [UniTask](https://github.com/Cysharp/UniTask) - Unity异步解决方案  
- [protobuf-net](https://github.com/protobuf-net/protobuf-net) - .NET ProtoBuf实现

---

<div align="center">

**⭐ 如果这个项目对你有帮助，请给个Star支持一下！**

Made with ❤️ for Unity Developers

</div>
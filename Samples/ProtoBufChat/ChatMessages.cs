using ProtoBuf;

namespace Samples.ProtoBufChat
{
    /// <summary>
    /// 登录请求消息
    /// </summary>
    [ProtoContract]
    public class LoginRequest
    {
        [ProtoMember(1)]
        public string Username { get; set; }
        
        [ProtoMember(2)]  
        public string Password { get; set; }
        
        [ProtoMember(3)]
        public string ClientVersion { get; set; } = "1.0.0";
    }
    
    /// <summary>
    /// 登录响应消息
    /// </summary>
    [ProtoContract]
    public class LoginResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
        
        [ProtoMember(2)]
        public string Message { get; set; }
        
        [ProtoMember(3)]
        public long PlayerId { get; set; }
        
        [ProtoMember(4)]
        public string PlayerName { get; set; }
    }
    
    /// <summary>
    /// 聊天消息
    /// </summary>
    [ProtoContract]
    public class ChatMessage
    {
        [ProtoMember(1)]
        public string PlayerName { get; set; }
        
        [ProtoMember(2)]
        public string Content { get; set; }
        
        [ProtoMember(3)]
        public long Timestamp { get; set; }
        
        [ProtoMember(4)]
        public ChatChannelType Channel { get; set; } = ChatChannelType.World;
    }
    
    /// <summary>
    /// 聊天频道类型
    /// </summary>
    [ProtoContract]
    public enum ChatChannelType
    {
        [ProtoEnum]
        World = 0,      // 世界频道
        
        [ProtoEnum]
        Team = 1,       // 队伍频道
        
        [ProtoEnum] 
        Private = 2,    // 私聊
        
        [ProtoEnum]
        System = 3      // 系统消息
    }
    
    /// <summary>
    /// 玩家信息
    /// </summary>
    [ProtoContract]
    public class PlayerInfo
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
        
        [ProtoMember(2)]
        public string PlayerName { get; set; }
        
        [ProtoMember(3)]
        public int Level { get; set; } = 1;
        
        [ProtoMember(4)]
        public bool IsOnline { get; set; }
        
        [ProtoMember(5)]
        public long LastLoginTime { get; set; }
    }
    
    /// <summary>
    /// 心跳消息
    /// </summary>
    [ProtoContract]
    public class HeartbeatMessage
    {
        [ProtoMember(1)]
        public long Timestamp { get; set; }
        
        [ProtoMember(2)]
        public string ClientInfo { get; set; }
    }
    
    /// <summary>
    /// 错误响应消息
    /// </summary>
    [ProtoContract]
    public class ErrorResponse
    {
        [ProtoMember(1)]
        public int ErrorCode { get; set; }
        
        [ProtoMember(2)]
        public string ErrorMessage { get; set; }
        
        [ProtoMember(3)]
        public string Details { get; set; }
    }
}
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
//using Newtonsoft.Json;
using SuperSocket.Log;

namespace UnitySuperSocket
{
    /// <summary>
    /// 发送数据包结构 - 只包含发送时需要的字段
    /// 结构：长�?4) + 消息ID(2) + 客户端序列号(4) + 服务器ID(2) + 消息数据
    /// </summary>
    public class GameSendingPackage
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public ushort MessageId { get; set; }
        
        /// <summary>
        /// 客户端序列号
        /// </summary>
        public int ClientSequenceId { get; set; }
        
        /// <summary>
        /// 服务器ID
        /// </summary>
        public ushort ServerId { get; set; }
        
        /// <summary>
        /// ProtoBuf消息对象（直接存储对象，在编码器中序列化�?
        /// </summary>
        public object MessageObject { get; set; }
        
        /// <summary>
        /// 原始字节数据（用�?SendBufferAsync�?
        /// </summary>
        public byte[] MessageData { get; set; }
        
        /// <summary>
        /// 获取序列化后的估计大�?
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEstimatedSize()
        {
            var baseSize = sizeof(ushort) + sizeof(int) + sizeof(ushort);
            var dataSize = MessageData?.Length ?? 0;
            return baseSize + dataSize + 64; // 额外缓冲用于 protobuf 序列�?
        }
        
        /// <summary>
        /// 快速创建发送消息包（使�?protobuf 对象�?
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameSendingPackage Create(ushort messageId, int clientSequenceId, 
            ushort serverId, object messageObject = null)
        {
            var package = new GameSendingPackage
            {
                MessageId = messageId,
                ClientSequenceId = clientSequenceId,
                ServerId = serverId,
                MessageObject = messageObject
            };
            
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogSend($"[GameSendingPackage] 创建发送包: MsgId={messageId}, SeqId={clientSequenceId}, ServerId={serverId}, ObjectType={messageObject?.GetType().Name ?? "null"}");
            #endif
            return package;
        }
        
        /// <summary>
        /// 快速创建发送消息包（使用字节数据）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameSendingPackage CreateWithData(ushort messageId, int clientSequenceId, 
            ushort serverId, byte[] messageData = null)
        {
            var package = new GameSendingPackage
            {
                MessageId = messageId,
                ClientSequenceId = clientSequenceId,
                ServerId = serverId,
                MessageData = messageData
            };
            
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogInfo($"[GameSendingPackage] 创建数据�? MsgId={messageId}, SeqId={clientSequenceId}, ServerId={serverId}, DataLength={messageData?.Length ?? 0}");
            #endif
            return package;
        }
    }
    
    /// <summary>
    /// 接收数据包结�?- 包含接收时需要的所有字�?
    /// 结构：长�?4) + 消息ID(2) + 客户端序列号(4) + 服务器序列号(4) + 玩家UID(8) + 服务器ID(2) + 消息数据
    /// </summary>
    public class GameReceivingPackage
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public ushort MessageId { get; set; }
        
        /// <summary>
        /// 客户端序列号
        /// </summary>
        public int ClientSequenceId { get; set; }
        
        /// <summary>
        /// 服务器序列号
        /// </summary>
        public int ServerSequenceId { get; set; }
        
        /// <summary>
        /// 玩家UID
        /// </summary>
        public long PlayerUID { get; set; }
        
        /// <summary>
        /// 服务器ID
        /// </summary>
        public ushort ServerId { get; set; }
        
        /// <summary>
        /// ProtoBuf消息数据长度
        /// </summary>
        public int MessageDataLength { get; set; }
        
        /// <summary>
        /// 反序列化后的ProtoBuf消息对象
        /// </summary>
        public object MessageObject { get; set; }
        

        public override string ToString()
        {
           // string messageObject = MessageObject == null ? "null" : JsonConvert.SerializeObject(MessageObject); 
            string messageObject = MessageObject == null ? "null" : MessageObject.ToString();
            return $"MessageId={MessageId}, ClientSequenceId={ClientSequenceId}, ServerSequenceId={ServerSequenceId}, PlayerUID={PlayerUID}, ServerId={ServerId}, MessageDataLength={MessageDataLength},MessageObject={messageObject}";
        }

        /// <summary>
        /// 获取序列化后的估计大�?
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEstimatedSize()
        {
            var baseSize = sizeof(ushort) + sizeof(int) * 2 + sizeof(long) + sizeof(ushort);
            return baseSize + MessageDataLength;
        }
        
        /// <summary>
        /// 快速创建接收消息包
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameReceivingPackage Create()
        {
            return new GameReceivingPackage();
        }
    }
}

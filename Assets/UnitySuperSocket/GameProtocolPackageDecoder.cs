using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using ProtoBuf;
using ProtoBuf.Meta;
using SuperSocket.ProtoBase;
using SuperSocket.Log;

namespace UnitySuperSocket
{
    /// <summary>
    /// 基于二进制序列化的高性能游戏协议包解码器
    /// 使用零拷贝技术和内存池优化，提供极致解码性能
    /// 专门用于解码接收到的数据包
    /// </summary>
    public class GameProtocolPackageDecoder : IPackageDecoder<GameReceivingPackage>
    {
        // 复用GameReceivingPackage实例以减少GC压力
        private readonly ObjectPool<GameReceivingPackage> _packagePool;
        
        // 消息类型注册表，用于根据MessageId反序列化对应的protobuf类型
        private static readonly Dictionary<ushort, Type> _messageTypeRegistry = new Dictionary<ushort, Type>();
        
        // 静态共享的MemoryStream，用于protobuf反序列化
        private static readonly ThreadLocal<MemoryStream> _sharedDeserializeStream = 
            new ThreadLocal<MemoryStream>(() => new MemoryStream(2048), true);
        
        /// <summary>
        /// 构造函数，初始化对象池
        /// </summary>
        public GameProtocolPackageDecoder()
        {
            _packagePool = new ObjectPool<GameReceivingPackage>(() => GameReceivingPackage.Create(), 32);
        }
        
        /// <summary>
        /// 注册消息类型，用于反序列化
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="messageType">对应的protobuf类型</param>
        public static void RegisterMessageType(ushort messageId, Type messageType)
        {
            if (messageType != null && typeof(IExtensible).IsAssignableFrom(messageType))
            {
                _messageTypeRegistry[messageId] = messageType;
                #if ENABLE_SUPERSOCKET_LOG	
                NetLogUtil.LogInfo($"[GameProtocolPackageDecoder] 注册消息类型: MessageId={messageId}, Type={messageType.Name}");
                #endif
            }
            else
            {
                #if ENABLE_SUPERSOCKET_LOG	
                NetLogUtil.LogError($"[GameProtocolPackageDecoder] 无效的消息类型: MessageId={messageId}, Type={messageType?.Name ?? "null"}");
                #endif
            }
        }
        
        /// <summary>
        /// 批量注册消息类型
        /// </summary>
        /// <param name="messageTypes">消息ID到类型的映射</param>
        public static void RegisterMessageTypes(Dictionary<ushort, Type> messageTypes)
        {
            foreach (var kvp in messageTypes)
            {
                RegisterMessageType(kvp.Key, kvp.Value);
            }
        }
        
        /// <summary>
        /// 使用二进制序列化高性能解码二进制数据为GameReceivingPackage
        /// </summary>
        /// <param name="buffer">包含完整消息的缓冲区</param>
        /// <param name="context">上下文对象（未使用）</param>
        /// <returns>解码后的GameReceivingPackage</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameReceivingPackage Decode(ref ReadOnlySequence<byte> buffer, object context)
        {
            #if ENABLE_SUPERSOCKET_LOG	
            NetLogUtil.LogReceive($"[GameProtocolPackageDecoder.Decode] 开始解码数据包，缓冲区长度: {buffer.Length}");
            #endif
            
#if ENABLE_SUPERSOCKET_LOG
            // 记录Decoder接收到的原始buffer数据
            try
            {
                byte[] completeBufferData = buffer.ToArray();
                string completeHex = NetLogUtil.HexDump(completeBufferData, 0, Math.Min(completeBufferData.Length, 64));
                NetLogUtil.LogReceive($"[GameProtocolPackageDecoder.Decode] 接收到的完整buffer数据 ({completeBufferData.Length} bytes):\n{completeHex}");
                
                // 检查长度字段
                if (completeBufferData.Length >= 4)
                {
                    int bufferLength = System.BitConverter.ToInt32(completeBufferData, 0);
                    NetLogUtil.LogReceive($"[GameProtocolPackageDecoder.Decode] Buffer长度字段: {bufferLength} (前4字节)");
                }
                
                // 检查MsgId字段
                if (completeBufferData.Length >= 6)
                {
                    ushort msgId = System.BitConverter.ToUInt16(completeBufferData, 4);
                    NetLogUtil.LogReceive($"[GameProtocolPackageDecoder.Decode] Buffer MsgId字段: {msgId} (第5-6字节)");
                    
                    if (msgId == 499)
                    {
                        NetLogUtil.LogReceive($"[GameProtocolPackageDecoder.Decode] *** 检测到MsgId=499，开始详细分析 ***");
                        NetLogUtil.LogReceive($"[GameProtocolPackageDecoder.Decode] MsgId=499 完整buffer ({completeBufferData.Length} bytes):\n{NetLogUtil.HexDump(completeBufferData, 0, completeBufferData.Length)}");
                    }
                }
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[GameProtocolPackageDecoder.Decode] Buffer分析日志错误: {e.Message}");
            }
#endif
            
            // 从对象池获取复用实例
            var packageInfo = _packagePool.Get();
            
            if (buffer.IsSingleSegment)
            {
                #if ENABLE_SUPERSOCKET_LOG
                // 单段数据，使用高性能Span处理
                NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] 使用单段解码");
                #endif
                DecodeSingleSegment(buffer.FirstSpan.Slice(4), packageInfo); // 跳过长度字段
            }
            else
            {
                #if ENABLE_SUPERSOCKET_LOG
                // 多段数据，使用SequenceReader处理
                NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] 使用多段解码");
                #endif
                DecodeMultiSegment(ref buffer, packageInfo);
            }
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] 解码完成: MsgId={packageInfo.MessageId}, ClientSeqId={packageInfo.ClientSequenceId}, ServerId={packageInfo.ServerId}, DataLength={packageInfo.MessageDataLength}");
            #endif

#if ENABLE_SUPERSOCKET_LOG
            // Log complete message information with all protocol fields and binary data
            try
            {
                // 获取完整的二进制数据（包含长度头）
                byte[] completeBinaryData = buffer.ToArray();
                int completeBinaryLength = completeBinaryData.Length;
                
                // 计算总长度（第一个字段就是总长度）
                int totalLength = completeBinaryLength;
                if (completeBinaryLength >= 4)
                {
                    totalLength = System.BitConverter.ToInt32(completeBinaryData, 0);
                }
                
                // 计算protobuf数据长度
                int protobufLength = packageInfo.MessageDataLength;
                
                NetLogUtil.LogRecvMessageComplete(
                    totalLength,
                    packageInfo.MessageId,
                    packageInfo.ClientSequenceId,
                    packageInfo.ServerSequenceId,
                    packageInfo.PlayerUID,
                    packageInfo.ServerId,
                    completeBinaryData,
                    completeBinaryLength,
                    protobufLength);
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[{NetLogUtil.Timestamp()}] [Net Recv Complete Log Error] {e.Message}");
            }
#endif
            
            return packageInfo;
        }
        
        /// <summary>
        /// 高性能单段数据解码（零拷贝）
        /// 格式：消息ID(2) + 客户端序列号(4) + 服务器序列号(4) + 玩家UID(8) + 服务器ID(2) + protobuf数据
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecodeSingleSegment(ReadOnlySpan<byte> span, GameReceivingPackage packageInfo)
        {
            // 检查数据长度是否足够
            const int headerSize = 2 + 4 + 4 + 8 + 2; // ushort + int + int + long + ushort = 20字节
            
#if ENABLE_SUPERSOCKET_LOG
            // 添加详细的调试日志
            try
            {
                NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] DecodeSingleSegment开始: span.Length={span.Length}, headerSize={headerSize}");
                if (span.Length > 0)
                {
                    string spanHex = NetLogUtil.HexDump(span.ToArray(), 0, Math.Min(span.Length, 32));
                    NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] Span前32字节数据:\n{spanHex}");
                }
            }
            catch (Exception debugEx)
            {
                NetLogUtil.LogError($"[GameProtocolPackageDecoder] 调试日志错误: {debugEx.Message}");
            }
#endif
            
            if (span.Length < headerSize)
            {
                // 数据不完整，设置默认值
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[GameProtocolPackageDecoder] 数据长度不足: {span.Length} < {headerSize}");
                #endif
                packageInfo.MessageId = 0;
                packageInfo.ClientSequenceId = 0;
                packageInfo.ServerSequenceId = 0;
                packageInfo.PlayerUID = 0;
                packageInfo.ServerId = 0;
                packageInfo.MessageDataLength = 0;
                return;
            }
            
            int offset = 0;
            
            // 使用 BinaryPrimitives 读取小端序数据
            ushort messageId = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
#if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] 字段解析 - MsgId: 偏移{offset}-{offset+1}, 原始字节:[{span[offset]:X2} {span[offset+1]:X2}], 解析值:{messageId}");
#endif
            offset += 2;
            
            int clientSequenceId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
#if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] 字段解析 - 客户端序列: 偏移{offset}-{offset+3}, 原始字节:[{span[offset]:X2} {span[offset+1]:X2} {span[offset+2]:X2} {span[offset+3]:X2}], 解析值:{clientSequenceId}");
#endif
            offset += 4;
            
            int serverSequenceId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
#if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] 字段解析 - 服务器序列: 偏移{offset}-{offset+3}, 原始字节:[{span[offset]:X2} {span[offset+1]:X2} {span[offset+2]:X2} {span[offset+3]:X2}], 解析值:{serverSequenceId}");
#endif
            offset += 4;
            
            long playerUID = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8));
#if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] 字段解析 - PlayerUID: 偏移{offset}-{offset+7}, 解析值:{playerUID}");
            NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] PlayerUID原始字节: [{span[offset]:X2} {span[offset+1]:X2} {span[offset+2]:X2} {span[offset+3]:X2} {span[offset+4]:X2} {span[offset+5]:X2} {span[offset+6]:X2} {span[offset+7]:X2}]");
#endif
            offset += 8;
            
            ushort serverId = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
#if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] 字段解析 - 服务器ID: 偏移{offset}-{offset+1}, 原始字节:[{span[offset]:X2} {span[offset+1]:X2}], 解析值:{serverId}");
#endif
            offset += 2;
            
            packageInfo.MessageId = messageId;
            packageInfo.ClientSequenceId = clientSequenceId;
            packageInfo.ServerSequenceId = serverSequenceId;
            packageInfo.PlayerUID = playerUID;
            packageInfo.ServerId = serverId;
            
#if ENABLE_SUPERSOCKET_LOG
            // 特别关注MsgId=499的情况
            if (messageId == 499)
            {
                NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] MsgId=499 详细信息: span.Length={span.Length}, offset={offset}, headerSize={headerSize}");
                NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] MsgId=499 解析结果: MessageId={messageId}, ClientSeqId={clientSequenceId}, ServerSeqId={serverSequenceId}, PlayerUID={playerUID}, ServerId={serverId}");
            }
#endif
            
            // 读取剩余的ProtoBuf消息数据
            var remaining = span.Length - offset;
            
#if ENABLE_SUPERSOCKET_LOG
            if (messageId == 499)
            {
                NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] MsgId=499 剩余数据计算: remaining = {span.Length} - {offset} = {remaining}");
            }
#endif
            
            // 只记录protobuf数据长度，不复制数据
            packageInfo.MessageDataLength = remaining;
            
            if (remaining >= 0)
            {
                // 尝试反序列化protobuf对象，传递原始span数据
                TryDeserializeMessageObject(packageInfo, span.Slice(offset));
            }
        }
        
        /// <summary>
        /// 多段数据解码（处理网络分段情况）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecodeMultiSegment(ref ReadOnlySequence<byte> buffer, GameReceivingPackage packageInfo)
        {
            // 跳过长度字段
            var slicedBuffer = buffer.Slice(4);
            
            // 将多段数据合并为单段进行处理
            var dataArray = slicedBuffer.ToArray();
            DecodeSingleSegment(dataArray, packageInfo);
        }
        
        /// <summary>
        /// 尝试反序列化protobuf消息对象
        /// </summary>
        /// <param name="packageInfo">包含消息信息的包</param>
        /// <param name="protobufData">Protobuf数据的内存切片</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryDeserializeMessageObject(GameReceivingPackage packageInfo, ReadOnlySpan<byte> protobufData)
        {
            try
            {
                // 检查是否已注册该消息类型
                if (_messageTypeRegistry.TryGetValue(packageInfo.MessageId, out Type messageType))
                {
#if ENABLE_SUPERSOCKET_LOG
                    // 详细调试Protobuf数据
                    try
                    {
                        var debugArray = protobufData.ToArray();
                        string protobufHex = NetLogUtil.HexDump(debugArray, 0, Math.Min(protobufData.Length, 64));
                        NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] 尝试反序列化 MessageId={packageInfo.MessageId}, Type={messageType.Name}\n原Protobuf数据 ({protobufData.Length} bytes):\n{protobufHex}");
                        
                        // 检查前几个字节的内容，分析Protobuf的wire-type
                        if (protobufData.Length > 0)
                        {
                            byte firstByte = protobufData[0];
                            int fieldNumber = firstByte >> 3;
                            int wireType = firstByte & 0x07;
                            NetLogUtil.LogDebug($"[GameProtocolPackageDecoder] 首字节分析: 0x{firstByte:X2}, FieldNumber={fieldNumber}, WireType={wireType}");
                        }
                    }
                    catch (Exception debugEx)
                    {
                        NetLogUtil.LogError($"[GameProtocolPackageDecoder] Protobuf调试日志错误: {debugEx.Message}");
                    }
#endif
                    
                    // 使用共享的MemoryStream进行反序列化
                    var stream = _sharedDeserializeStream.Value;
                    stream.Position = 0;
                    stream.SetLength(0);
                    
                    // 将字节数据写入流
                    stream.Write(protobufData);
                    stream.Position = 0;
                    
                    // 使用ProtoBuf反序列化
                    var messageObject = RuntimeTypeModel.Default.Deserialize(stream, null, messageType);
                    packageInfo.MessageObject = messageObject;
                    
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] 成功反序列化消息: MessageId={packageInfo.MessageId}, Type={messageType.Name}");
                    #endif
                }
                else
                {
                    // 未注册的消息类型，只保留字节数据
                    packageInfo.MessageObject = null;
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogWarning($"[GameProtocolPackageDecoder] 未注册的消息类型: MessageId={packageInfo.MessageId}");
                    #endif
#if ENABLE_SUPERSOCKET_LOG
                    // 在调试模式下显示Protobuf数据
                    try
                    {
                        if (protobufData.Length > 0)
                        {
                            var debugArray = protobufData.ToArray();
                            string protobufHex = NetLogUtil.HexDump(debugArray, 0, Math.Min(protobufData.Length, 32));
                            NetLogUtil.LogReceive($"[GameProtocolPackageDecoder] 未注册消息的Protobuf数据:\n{protobufHex}");
                        }
                    }
                    catch (Exception debugEx)
                    {
                        NetLogUtil.LogError($"[GameProtocolPackageDecoder] 未注册消息调试日志错误: {debugEx.Message}");
                    }
#endif
                }
            }
            catch (Exception ex)
            {
                // 反序列化失败，记录错误但不影响解码流程
                packageInfo.MessageObject = null;
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[GameProtocolPackageDecoder] 反序列化失败: MessageId={packageInfo.MessageId}, Error={ex.Message}");
                #endif
#if ENABLE_SUPERSOCKET_LOG
                // 在反序列化失败时输出Protobuf数据供调试
                try
                {
                    if (protobufData.Length > 0)
                    {
                        var debugArray = protobufData.ToArray();
                        string protobufHex = NetLogUtil.HexDump(debugArray, 0, Math.Min(protobufData.Length, 64));
                        NetLogUtil.LogError($"[GameProtocolPackageDecoder] 反序列化失败的Protobuf数据 ({protobufData.Length} bytes):\n{protobufHex}");
                    }
                }
                catch (Exception debugEx)
                {
                    NetLogUtil.LogError($"[GameProtocolPackageDecoder] 失败数据调试日志错误: {debugEx.Message}");
                }
#endif
            }
        }
        
        /// <summary>
        /// 释放对象回池中（在消息处理完成后调用）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleasePackage(GameReceivingPackage packageInfo)
        {
            if (packageInfo != null)
            {
                // 重置所有字段以避免内存泄漏
                packageInfo.MessageId = 0;
                packageInfo.ClientSequenceId = 0;
                packageInfo.ServerSequenceId = 0;
                packageInfo.PlayerUID = 0;
                packageInfo.ServerId = 0;
                packageInfo.MessageDataLength = 0;
                packageInfo.MessageObject = null; // 清理反序列化的对象
                _packagePool.Return(packageInfo);
            }
        }
    }
    
    /// <summary>
    /// 简单的对象池实现，减少GameReceivingPackage的GC压力
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        private readonly Func<T> _objectGenerator;
        private readonly System.Collections.Concurrent.ConcurrentQueue<T> _objects = new();
        private readonly int _maxPoolSize;
        private volatile int _currentCount = 0;
        
        public ObjectPool(Func<T> objectGenerator, int maxPoolSize = 100)
        {
            _objectGenerator = objectGenerator;
            _maxPoolSize = maxPoolSize;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get()
        {
            if (_objects.TryDequeue(out T item))
            {
                System.Threading.Interlocked.Decrement(ref _currentCount);
                return item;
            }
            
            return _objectGenerator();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item)
        {
            if (item != null && _currentCount < _maxPoolSize)
            {
                _objects.Enqueue(item);
                System.Threading.Interlocked.Increment(ref _currentCount);
            }
        }
    }
}

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using SuperSocket.ProtoBase;
using ProtoBuf.Meta;
using SuperSocket;
using SuperSocket.Log;

namespace UnitySuperSocket
{
    /// <summary>
    /// 基于二进制序列化的高性能游戏协议包编码器
    /// </summary>
    public class GameProtocolPackageEncoder : IPackageEncoder<GameSendingPackage>
    {
        /// <summary>
        /// 构造函�?
        /// </summary>
        public GameProtocolPackageEncoder()
        {
        }
        
        /// <summary>
        /// 编码发送包为二进制数据（IPackageEncoder接口实现�?
        /// 结构：长�?4,包含自身) + 消息ID(2) + 客户端序列号(4) + 服务器ID(2) + 消息数据
        /// </summary>
        /// <param name="writer">目标缓冲区写入器</param>
        /// <param name="package">要编码的发送包</param>
        /// <returns>编码的字节数</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Encode(IBufferWriter<byte> writer, GameSendingPackage package)
        {
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogSend($"[GameProtocolPackageEncoder] 开始编码发送包: MsgId={package.MessageId}, SeqId={package.ClientSequenceId}, ServerId={package.ServerId}");
            #endif          
            var result = EncodeSendingPackage(writer, package);
            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogSend($"[GameProtocolPackageEncoder] 编码完成，字节数: {result}");
            #endif
            return result;
        }
        
        /// <summary>
        /// 编码发送包为二进制数据
        /// 结构：长�?4,包含自身) + 消息ID(2) + 客户端序列号(4) + 服务器ID(2) + 消息数据
        /// </summary>
        /// <param name="writer">目标缓冲区写入器</param>
        /// <param name="package">要编码的发送包</param>
        /// <returns>编码的字节数</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EncodeSendingPackage(IBufferWriter<byte> writer, GameSendingPackage package)
        {
            if (package == null) return 0;
            
            // 使用共享MemoryStream 构建完整数据
            var ms = _sharedMemoryStream.Value;
            ms.Position = 0;
            ms.SetLength(0);
            
            // 预留4字节长度占位
            ms.Write(new byte[4], 0, 4);
            
            // 写头部（小端）
            Span<byte> header = stackalloc byte[2 + 4 + 2];
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(0, 2), package.MessageId);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(2, 4), package.ClientSequenceId);
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(6, 2), package.ServerId);
            ms.Write(header);
            
            // 写消息体并记录原始protobuf数据
            byte[] protobufData = null;
            int protobufLength = 0;
            
            if (package.MessageObject != null)
            {
                var tmp = _sharedProtobufStream.Value;
                tmp.Position = 0;
                tmp.SetLength(0);
                var model = RuntimeTypeModel.Default;
                model.Serialize(tmp, package.MessageObject);
                protobufLength = (int)tmp.Position;
                protobufData = new byte[protobufLength];
                Buffer.BlockCopy(tmp.GetBuffer(), 0, protobufData, 0, protobufLength);
                ms.Write(protobufData, 0, protobufLength);
                
#if ENABLE_SUPERSOCKET_LOG
                // Log protobuf binary data (pre-send)
                try
                {
                    NetLogUtil.LogSendPre(package.MessageId, package.ServerId, package.ClientSequenceId, protobufData, protobufLength);
                }
                catch (Exception e)
                {
                    NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Net Send Pre Log Error] {e.Message}");
                }
#endif
            }
            else if (package.MessageData != null && package.MessageData.Length > 0)
            {
                protobufData = package.MessageData;
                protobufLength = package.MessageData.Length;
                ms.Write(package.MessageData, 0, package.MessageData.Length);
                
#if ENABLE_SUPERSOCKET_LOG
                // Log buffer binary data (pre-send)
                try
                {
                    NetLogUtil.LogSendPre(package.MessageId, package.ServerId, package.ClientSequenceId, protobufData, protobufLength);
                }
                catch (Exception e)
                {
                    NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Net Send Pre Log Error] {e.Message}" );
                }
#endif
            }
            
            // 回填长度（包含自身4字节）
            var totalLen = (int)ms.Length;
            ms.Position = 0;
            Span<byte> lenSpan = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lenSpan, totalLen);
            ms.Write(lenSpan);
            
#if ENABLE_SUPERSOCKET_LOG
            // Log final binary data before socket send
            try
            {
                byte[] finalData = new byte[totalLen];
                Buffer.BlockCopy(ms.GetBuffer(), 0, finalData, 0, totalLen);
                NetLogUtil.LogSendFinal(package.MessageId, package.ServerId, package.ClientSequenceId, finalData, 0, totalLen);
            }
            catch (Exception e)
            {
                NetLogUtil.LogSend($"[{ NetLogUtil.Timestamp()}] [Net Send Final Log Error] {e.Message}" );
            }
#endif
            
            // 一次性写writer
            var finalSpan = writer.GetSpan(totalLen);
            
#if ENABLE_SUPERSOCKET_LOG
            // Debug: 记录写入writer之前的数据
            try
            {
                byte[] debugData = new byte[totalLen];
                Buffer.BlockCopy(ms.GetBuffer(), 0, debugData, 0, totalLen);
                string debugHex = NetLogUtil.HexDump(debugData, 0, Math.Min(totalLen, 64));
                NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Encoder Debug] Before writer.Advance, totalLen={totalLen}\n{debugHex}");
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[Encoder Debug Error] {e.Message}");
            }
#endif
            
            // 直接将数据复制到Span
            var sourceData = ms.GetBuffer().AsSpan(0, totalLen);
            sourceData.CopyTo(finalSpan);
            writer.Advance(totalLen);
            
#if ENABLE_SUPERSOCKET_LOG
            // Debug: 验证写入writer后的数据
            try
            {
                var verifySpan = finalSpan.Slice(0, totalLen);
                byte[] verifyData = verifySpan.ToArray();
                string verifyHex = NetLogUtil.HexDump(verifyData, 0, Math.Min(totalLen, 64));
                NetLogUtil.LogSend($"[{NetLogUtil.Timestamp()}] [Encoder Debug] After writer.Advance, verifyLen={verifyData.Length}\n{verifyHex}");
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[Encoder Verify Error] {e.Message}");
            }
#endif
            
            return totalLen;
        }
        
        // 静态共享的 MemoryStream，避免频繁创建和销毁
        // 使用较小的初始容量以减少内存占用
        private static readonly ThreadLocal<MemoryStream> _sharedMemoryStream = 
            new ThreadLocal<MemoryStream>(() => new MemoryStream(2048), true);
            
        // 专门用于 protobuf 序列化的共享 MemoryStream
        private static readonly ThreadLocal<MemoryStream> _sharedProtobufStream = 
            new ThreadLocal<MemoryStream>(() => new MemoryStream(2048), true);

        /// <summary>
        /// 直接Span 中序列化 protobuf 对象
        /// </summary>
        /// <param name="messageObject">要序列化�?protobuf 对象</param>
        /// <param name="targetSpan">目标 span</param>
        /// <returns>序列化的字节数</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SerializeProtobufObjectToSpan(object messageObject, Span<byte> targetSpan)
        {
            if (messageObject == null) return 0;
            
            // 使用专门用于 protobuf 序列化的共享 MemoryStream
            var memoryStream = _sharedProtobufStream.Value;
            memoryStream.Position = 0;
            memoryStream.SetLength(0);
            
            // 使用 RuntimeTypeModel 序列化（高效方式�?
            var model = RuntimeTypeModel.Default;
            model.Serialize(memoryStream, messageObject);
            
            var length = (int)memoryStream.Position;
            if (length > targetSpan.Length)
            {
                throw new InvalidOperationException($"序列化数据太�? {length} > {targetSpan.Length}");
            }
            
            // 直接复制到目标span
            memoryStream.GetBuffer().AsSpan(0, length).CopyTo(targetSpan);
            
            return length;
        }
        
    }
}

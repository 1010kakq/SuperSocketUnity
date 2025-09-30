using System;
using System.Buffers;
using SuperSocket.ProtoBase;
using SuperSocket.Log;

namespace UnitySuperSocket
{
    /// <summary>
    /// 游戏协议的管道过滤器，用于解析游戏特有的消息格式
    /// </summary>
    public class GameProtocolPipelineFilter : FixedHeaderPipelineFilter<GameReceivingPackage>
    {
        /// <summary>
        /// 初始化游戏协议管道过滤器
        /// </summary>
        public GameProtocolPipelineFilter()
            : base(4) // 4字节长度
        {
            Decoder = new GameProtocolPackageDecoder();
        }

        /// <summary>
        /// 从消息头中获取消息体长度
        /// </summary>
        /// <param name="buffer">包含消息头的缓冲区</param>
        /// <returns>消息体长度</returns>
        protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
        {
#if ENABLE_SUPERSOCKET_LOG
            // 详细调试原始缓冲区内
            try
            {
                byte[] bufferDebug = buffer.ToArray();
                string bufferHex = NetLogUtil.HexDump(bufferDebug, 0, Math.Min(bufferDebug.Length, 32));
                NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] Header缓冲区内?(len={bufferDebug.Length}):\n{bufferHex}");
                
                // 检查是否可能包含MsgId=499
                if (bufferDebug.Length >= 6) // 4字节长度 + 2字节MsgId
                {
                    ushort msgId = System.BitConverter.ToUInt16(bufferDebug, 4);
                    if (msgId == 499)
                    {
                        NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] 检测到MsgId=499的消息");
                        // 显示更多数据用于调试
                        string extendedHex = NetLogUtil.HexDump(bufferDebug, 0, Math.Min(bufferDebug.Length, 64));
                        NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] MsgId=499 扩展数据:\n{extendedHex}");
                    }
                }
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[GameProtocolPipelineFilter] 调试日志错误: {e.Message}");
            }
#endif
            
            // 使用更简单直接的方式读取长度字段
            if (buffer.Length < 4)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[GameProtocolPipelineFilter] Header长度不足: {buffer.Length} < 4");
                #endif
                return -1;
            }
            
            // 直接读取buffer 中读取前4个字节
            var lengthBytes = new byte[4];
            buffer.Slice(0, 4).CopyTo(lengthBytes);
            
#if ENABLE_SUPERSOCKET_LOG
            // 详细调试长度字段的字节内容
            try
            {
                string lengthHex = NetLogUtil.HexDump(lengthBytes, 0, 4);
                NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] 长度字段字节: {lengthHex}");
                
                // 逐字节显示
                NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] 字节详情: [0]={lengthBytes[0]:X2} [1]={lengthBytes[1]:X2} [2]={lengthBytes[2]:X2} [3]={lengthBytes[3]:X2}");
                
                // 尝试不同的字节序解释
                int littleEndian = BitConverter.ToInt32(lengthBytes, 0);
                int bigEndian = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBytes, 0));
                
                // 手动计算小端序和大端序
                int manualLittleEndian = lengthBytes[0] | (lengthBytes[1] << 8) | (lengthBytes[2] << 16) | (lengthBytes[3] << 24);
                int manualBigEndian = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) | (lengthBytes[2] << 8) | lengthBytes[3];
                
                NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] 长度解析: 小端序{littleEndian}, 大端序{bigEndian}");
                NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] 手动计算: 小端序{manualLittleEndian}, 大端序{manualBigEndian}");
                NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] Buffer总长度? {buffer.Length}");
                NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] 系统是否小端序? {BitConverter.IsLittleEndian}");
            }
            catch (Exception e)
            {
                NetLogUtil.LogError($"[GameProtocolPipelineFilter] 调试日志错误: {e.Message}");
            }
#endif
            
            // 使用小端序解析长度（与编码器保持一致）
            int totalLength = BitConverter.ToInt32(lengthBytes, 0);
            
            // 检查长度是否合理
            if (totalLength < 4) // 长度必须至少4
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[GameProtocolPipelineFilter] 无效的消息长度: {totalLength}");
                #endif
                // 注意：不能返回负数，否则SuperSocket会抛出ProtocolException
                // 返回0表示只有头部，没有消息体
                return 0;
            }
            
            // 按编码器实现，长度字段表示总长度（包含长度字段本身4字节）
            // 所以消息体长度 = 总长度- 4字节长度
            int bodyLength = totalLength - 4;
            
            // 再次检查消息体长度的合理性
            if (bodyLength < 0)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogError($"[GameProtocolPipelineFilter] 计算出的消息体长度为负数: {bodyLength}, 总长度{totalLength}");
                #endif
                return 0; // 返回0而不是负数
            }

            #if ENABLE_SUPERSOCKET_LOG
            NetLogUtil.LogReceive($"[GameProtocolPipelineFilter] 解析消息长度: 总长度{totalLength}, 消息体长度{bodyLength}");
            #endif
            return bodyLength;
        }
    }
}

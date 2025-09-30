#if ENABLE_SUPERSOCKET_LOG

using System;
using System.Text;
using UnityEngine;

namespace SuperSocket.Log 
{
/// <summary>
/// Network logging utility with configurable log categories and binary data dumps.
/// Only compiled in Unity Editor or when USE_LOG is defined.
/// </summary>
public static class NetLogUtil
{
    private const int DumpLimit = 64000000;      // Maximum bytes to dump per log
    private const int LineBytes = 16;      // Bytes per hex dump line
    
    #region Logging Control Toggles
    
    /// <summary>
    /// Controls whether connection lifecycle logs are enabled (connect, disconnect, etc.)
    /// </summary>
    public static volatile bool EnableConnectionLogs = true;
    
    /// <summary>
    /// Controls whether debug/verbose logs are enabled
    /// </summary>
    public static volatile bool EnableDebugLogs = true;
    
    /// <summary>
    /// Controls whether send-related logs are enabled (requires EnableDebugLogs = true)
    /// </summary>
    public static volatile bool EnableSendLogs = false;
    
    /// <summary>
    /// Controls whether receive-related logs are enabled (requires EnableDebugLogs = true)
    /// </summary>
    public static volatile bool EnableReceiveLogs = true;
    
    /// <summary>
    /// Controls whether info logs are enabled
    /// </summary>
    public static volatile bool EnableInfoLogs = true;
    
    /// <summary>
    /// Controls whether warning logs are enabled
    /// </summary>
    public static volatile bool EnableWarningLogs = true;
    
    // Note: Error logs are always enabled and cannot be disabled
    
    #endregion
    
    #region Configuration
    
    /// <summary>
    /// Configure logging toggles at startup
    /// </summary>
    /// <param name="enableConnection">Enable connection logs</param>
    /// <param name="enableDebug">Enable debug logs</param>
    /// <param name="enableSend">Enable send logs</param>
    /// <param name="enableReceive">Enable receive logs</param>
    /// <param name="enableInfo">Enable info logs</param>
    /// <param name="enableWarning">Enable warning logs</param>
    public static void Configure(bool enableConnection = true, bool enableDebug = false, 
        bool enableSend = false, bool enableReceive = false, bool enableInfo = true, bool enableWarning = true)
    {
        EnableConnectionLogs = enableConnection;
        EnableDebugLogs = enableDebug;
        EnableSendLogs = enableSend;
        EnableReceiveLogs = enableReceive;
        EnableInfoLogs = enableInfo;
        EnableWarningLogs = enableWarning;
    }
    
    #endregion
    
    #region General Logging Methods
    
    /// <summary>
    /// Logs connection lifecycle events (connect, disconnect, reconnect, etc.)
    /// </summary>
    /// <param name="message">Connection message</param>
    public static void LogConnection(string message)
    {
        if (EnableConnectionLogs)
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// Logs debug/verbose information
    /// </summary>
    /// <param name="message">Debug message</param>
    public static void LogDebug(string message)
    {
        if (EnableDebugLogs)
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// Logs send-related information (requires EnableDebugLogs = true)
    /// </summary>
    /// <param name="message">Send message</param>
    public static void LogSend(string message)
    {
        if (EnableDebugLogs && EnableSendLogs)
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// Logs receive-related information (requires EnableDebugLogs = true)
    /// </summary>
    /// <param name="message">Receive message</param>
    public static void LogReceive(string message)
    {
        if (EnableDebugLogs && EnableReceiveLogs)
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// Logs informational messages
    /// </summary>
    /// <param name="message">Info message</param>
    public static void LogInfo(string message)
    {
        if (EnableInfoLogs)
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// Logs warning messages
    /// </summary>
    /// <param name="message">Warning message</param>
    public static void LogWarning(string message)
    {
        if (EnableWarningLogs)
        {
            Debug.LogWarning(message);
        }
    }
    
    /// <summary>
    /// Logs error messages (always enabled, cannot be disabled)
    /// </summary>
    /// <param name="message">Error message</param>
    public static void LogError(string message)
    {
        Debug.LogError(message);
    }
    
    #endregion
    

        /// <summary>
        /// Returns current timestamp with millisecond precision.
        /// </summary>
        public static string Timestamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// Generates a hex dump string from binary data.
        /// </summary>
        /// <param name="data">Source byte array</param>
        /// <param name="offset">Starting offset in the array</param>
        /// <param name="count">Number of bytes to dump</param>
        /// <returns>Formatted hex dump string</returns>
        public static string HexDump(byte[] data, int offset, int count)
        {
            if (data == null || count <= 0)
                return "(empty)";
            
            int toDump = Math.Min(count, Math.Min(DumpLimit, data.Length - offset));
            if (toDump <= 0)
                return "(empty)";

            // Pre-calculate capacity to minimize allocations
            var sb = new StringBuilder(toDump * 3 + (toDump / LineBytes + 1) * 8 + 32);
            
            for (int i = 0; i < toDump; i++)
            {
                if (i % LineBytes == 0)
                {
                    if (i > 0) sb.AppendLine();
                    sb.AppendFormat("{0:X4}: ", i);
                }
                
                byte b = data[offset + i];
                sb.AppendFormat("{0:X2}", b);
                
                if ((i % LineBytes) != LineBytes - 1 && i < toDump - 1)
                    sb.Append(" ");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Logs binary data before network processing (pre-send).
        /// </summary>
        /// <param name="msgId">Message ID</param>
        /// <param name="serverId">Server ID</param>
        /// <param name="sequenceId">Sequence ID</param>
        /// <param name="data">Binary data buffer</param>
        /// <param name="length">Length of valid data</param>
        public static void LogSendPre(int msgId, int serverId, long sequenceId, byte[] data, int length)
        {
            if (!EnableDebugLogs || !EnableSendLogs) return;
            
            string header = string.Format("[{0}] [Net Send Pre] msgId={1} serverId={2} seq={3} len={4}", 
                Timestamp(), msgId, serverId, sequenceId, length);
            
            if (data == null || length == 0)
            {
                Debug.Log(header + "\n(empty)");
                return;
            }
            
            int toDump = Math.Min(length, DumpLimit);
            string hex = HexDump(data, 0, toDump);
            string suffix = length > DumpLimit ? 
                string.Format("\n... total {0} bytes, showing first {1}", length, DumpLimit) : "";
            
            Debug.Log(header + "\n" + hex + suffix);
        }

        /// <summary>
        /// Logs final binary data before socket send.
        /// </summary>
        /// <param name="msgId">Message ID (may be -1 if not available)</param>
        /// <param name="serverId">Server ID (may be -1 if not available)</param>
        /// <param name="sequenceId">Sequence ID (may be -1 if not available)</param>
        /// <param name="data">Final send buffer</param>
        /// <param name="offset">Offset in send buffer</param>
        /// <param name="count">Number of bytes to send</param>
        public static void LogSendFinal(int msgId, int serverId, long sequenceId, byte[] data, int offset, int count)
        {
            if (!EnableDebugLogs || !EnableSendLogs) return;
            
            string header = string.Format("[{0}] [Net Send Final] msgId={1} serverId={2} seq={3} len={4}", 
                Timestamp(), msgId, serverId, sequenceId, count);
            
            if (data == null || count == 0)
            {
                Debug.Log(header + "\n(empty)");
                return;
            }
            
            int toDump = Math.Min(count, DumpLimit);
            string hex = HexDump(data, offset, toDump);
            string suffix = count > DumpLimit ? 
                string.Format("\n... total {0} bytes, showing first {1}", count, DumpLimit) : "";
            
            Debug.Log(header + "\n" + hex + suffix);
        }

        /// <summary>
        /// Logs successful send operation result.
        /// </summary>
        /// <param name="bytesSent">Actual bytes sent</param>
        /// <param name="expected">Expected bytes to send</param>
        public static void LogSendResultSuccess(int bytesSent, int expected)
        {
            if (!EnableDebugLogs || !EnableSendLogs) return;
            Debug.LogFormat("[{0}] [Net Send Done] bytesSent={1} expected={2}", 
                Timestamp(), bytesSent, expected);
        }

        /// <summary>
        /// Logs failed send operation with exception.
        /// </summary>
        /// <param name="ex">Exception that occurred</param>
        public static void LogSendResultFail(Exception ex)
        {
            Debug.LogErrorFormat("[{0}] [Net Send Error] {1}", 
                Timestamp(), ex != null ? ex.Message : "(unknown error)");
        }

        /// <summary>
        /// Logs raw binary data received from socket.
        /// </summary>
        /// <param name="data">Received binary data buffer</param>
        /// <param name="offset">Offset in receive buffer</param>
        /// <param name="count">Number of bytes received</param>
        public static void LogRecvRaw(byte[] data, int offset, int count)
        {
            if (!EnableDebugLogs || !EnableReceiveLogs) return;
            
            string header = string.Format("[{0}] [Net Recv Raw] len={1}", 
                Timestamp(), count);
            
            if (data == null || count == 0)
            {
                Debug.Log(header + "\n(empty)");
                return;
            }
            
            int toDump = Math.Min(count, DumpLimit);
            string hex = HexDump(data, offset, toDump);
            string suffix = count > DumpLimit ? 
                string.Format("\n... total {0} bytes, showing first {1}", count, DumpLimit) : "";
            
            Debug.Log(header + "\n" + hex + suffix);
        }

        /// <summary>
        /// Logs complete message after protocol parsing with all protocol fields.
        /// </summary>
        /// <param name="totalLength">Total message length including headers</param>
        /// <param name="msgId">Message ID</param>
        /// <param name="clientSequenceId">Client sequence ID</param>
        /// <param name="serverSequenceId">Server sequence ID</param>
        /// <param name="playerUID">Player UID</param>
        /// <param name="serverId">Server ID</param>
        /// <param name="completeBinaryData">Complete binary data including all headers</param>
        /// <param name="completeBinaryLength">Length of complete binary data</param>
        /// <param name="protobufLength">Length of protobuf data only</param>
        public static void LogRecvMessageComplete(int totalLength, int msgId, int clientSequenceId, int serverSequenceId, 
            long playerUID, int serverId, byte[] completeBinaryData, int completeBinaryLength, int protobufLength)
        {
            if (!EnableDebugLogs || !EnableReceiveLogs) return;
            
            string header = string.Format(
                "[{0}] [Net Recv Complete]\n" +
                "  TotalLen={1} MsgId={2} ClientSeq={3} ServerSeq={4}\n" +
                "  PlayerUID={5} ServerId={6} ProtobufLen={7}",
                Timestamp(), totalLength, msgId, clientSequenceId, serverSequenceId, 
                playerUID, serverId, protobufLength);
            
            if (completeBinaryData == null || completeBinaryLength == 0)
            {
                Debug.Log(header + "\n  Binary Data: (empty)");
                return;
            }
            
            int toDump = Math.Min(completeBinaryLength, DumpLimit);
            string hex = HexDump(completeBinaryData, 0, toDump);
            string suffix = completeBinaryLength > DumpLimit ? 
                string.Format("\n  ... total {0} bytes, showing first {1}", completeBinaryLength, DumpLimit) : "";
            
            // Add field breakdown for binary data
            string fieldBreakdown = GenerateFieldBreakdown(completeBinaryData, completeBinaryLength, 
                totalLength, msgId, clientSequenceId, serverSequenceId, playerUID, serverId, protobufLength);
            
            Debug.Log(header + "\n  Complete Binary Data:\n" + hex + suffix + "\n" + fieldBreakdown);
        }
        
        /// <summary>
        /// Generates field breakdown explanation for binary data.
        /// </summary>
        private static string GenerateFieldBreakdown(byte[] data, int dataLength, int totalLength, 
            int msgId, int clientSeq, int serverSeq, long playerUID, int serverId, int protobufLength)
        {
            var sb = new StringBuilder();
            sb.AppendLine("  Field Breakdown:");
            
            if (dataLength >= 4)
            {
                uint lengthValue = (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
                sb.AppendFormat("    0000-0003: Length Header ({0:X2} {1:X2} {2:X2} {3:X2} = {4} bytes)\n", 
                    data[0], data[1], data[2], data[3], lengthValue);
            }
            
            if (dataLength >= 6)
            {
                ushort msgIdValue = (ushort)(data[4] | (data[5] << 8));
                sb.AppendFormat("    0004-0005: Message ID ({0:X2} {1:X2} = {2})\n", 
                    data[4], data[5], msgIdValue);
            }
            
            if (dataLength >= 10)
            {
                uint clientSeqValue = (uint)(data[6] | (data[7] << 8) | (data[8] << 16) | (data[9] << 24));
                sb.AppendFormat("    0006-0009: Client Sequence ({0:X2} {1:X2} {2:X2} {3:X2} = {4})\n", 
                    data[6], data[7], data[8], data[9], clientSeqValue);
            }
            
            if (dataLength >= 14)
            {
                uint serverSeqValue = (uint)(data[10] | (data[11] << 8) | (data[12] << 16) | (data[13] << 24));
                sb.AppendFormat("    000A-000D: Server Sequence ({0:X2} {1:X2} {2:X2} {3:X2} = {4})\n", 
                    data[10], data[11], data[12], data[13], serverSeqValue);
            }
            
            if (dataLength >= 22)
            {
                // Player UID (8 bytes, little-endian)
                ulong playerUIDValue = 0;
                for (int i = 0; i < 8; i++)
                {
                    playerUIDValue |= ((ulong)data[14 + i] << (i * 8));
                }
                sb.AppendFormat("    000E-0015: Player UID ({0:X2} {1:X2} {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} = {8})\n",
                    data[14], data[15], data[16], data[17], data[18], data[19], data[20], data[21], playerUIDValue);
            }
            
            if (dataLength >= 24)
            {
                ushort serverIdValue = (ushort)(data[22] | (data[23] << 8));
                sb.AppendFormat("    0016-0017: Server ID ({0:X2} {1:X2} = {2})\n", 
                    data[22], data[23], serverIdValue);
            }
            
            if (dataLength > 24 && protobufLength > 0)
            {
                int protobufEndOffset = Math.Min(24 + protobufLength - 1, dataLength - 1);
                sb.AppendFormat("    0018-{0:X4}: Protobuf Data ({1} bytes)", 
                    protobufEndOffset, protobufLength);
            }
            
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Logs complete message after protocol parsing (simplified version for backward compatibility).
        /// </summary>
        /// <param name="msgId">Message ID</param>
        /// <param name="serverId">Server ID</param>
        /// <param name="sequenceId">Sequence ID</param>
        /// <param name="data">Message data buffer</param>
        /// <param name="offset">Offset in data buffer</param>
        /// <param name="count">Number of bytes in message</param>
        public static void LogRecvMessage(int msgId, int serverId, long sequenceId, byte[] data, int offset, int count)
        {
            if (!EnableDebugLogs || !EnableReceiveLogs) return;
            
            string header = string.Format("[{0}] [Net Recv Msg] msgId={1} serverId={2} seq={3} len={4}", 
                Timestamp(), msgId, serverId, sequenceId, count);
            
            if (data == null || count == 0)
            {
                Debug.Log(header + "\n(empty)");
                return;
            }
            
            int toDump = Math.Min(count, DumpLimit);
            string hex = HexDump(data, offset, toDump);
            string suffix = count > DumpLimit ? 
                string.Format("\n... total {0} bytes, showing first {1}", count, DumpLimit) : "";
            
            Debug.Log(header + "\n" + hex + suffix);
        }

        /// <summary>
        /// Logs successful receive operation result.
        /// </summary>
        /// <param name="bytesReceived">Actual bytes received</param>
        public static void LogRecvResultSuccess(int bytesReceived)
        {
            Debug.LogFormat("[{0}] [Net Recv Done] bytesReceived={1}", 
                Timestamp(), bytesReceived);
        }

        /// <summary>
        /// Logs failed receive operation with exception.
        /// </summary>
        /// <param name="ex">Exception that occurred</param>
        public static void LogRecvResultFail(Exception ex)
        {
            Debug.LogErrorFormat("[{0}] [Net Recv Error] {1}", 
                Timestamp(), ex != null ? ex.Message : "(unknown error)");
        }
    }
}
//}

#endif

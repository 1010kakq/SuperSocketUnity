#!/usr/bin/env node

/**
 * Unity SuperSocket 测试服务器
 * 
 * 这是一个简单的TCP回显服务器，用于测试Unity SuperSocket客户端
 * 支持基本的消息回显和协议解析
 * 
 * 使用方法：
 * 1. 安装Node.js (https://nodejs.org/)
 * 2. 在此目录运行: node test-server.js
 * 3. 服务器将在端口8080启动
 */

const net = require('net');
const fs = require('fs');
const path = require('path');

// 配置
const CONFIG = {
    port: 8080,
    host: '0.0.0.0',
    logFile: 'server.log',
    enableBinaryLog: true,
    enableProtocolParsing: true
};

// 日志函数
function log(message) {
    const timestamp = new Date().toISOString();
    const logMessage = `[${timestamp}] ${message}`;
    console.log(logMessage);
    
    // 写入日志文件
    fs.appendFile(CONFIG.logFile, logMessage + '\n', (err) => {
        if (err) console.error('写入日志文件失败:', err);
    });
}

// 十六进制转储
function hexDump(buffer, offset = 0, length = buffer.length) {
    let result = '';
    const bytesPerLine = 16;
    
    for (let i = offset; i < offset + length; i += bytesPerLine) {
        // 地址
        result += i.toString(16).padStart(4, '0').toUpperCase() + ': ';
        
        // 十六进制字节
        let hexPart = '';
        let asciiPart = '';
        
        for (let j = 0; j < bytesPerLine; j++) {
            if (i + j < offset + length) {
                const byte = buffer[i + j];
                hexPart += byte.toString(16).padStart(2, '0').toUpperCase() + ' ';
                asciiPart += (byte >= 32 && byte <= 126) ? String.fromCharCode(byte) : '.';
            } else {
                hexPart += '   ';
                asciiPart += ' ';
            }
        }
        
        result += hexPart + ' ' + asciiPart + '\n';
    }
    
    return result.trimEnd();
}

// 解析Unity SuperSocket协议
function parseGameProtocol(buffer) {
    if (buffer.length < 4) {
        return { error: '数据长度不足，需要至少4字节' };
    }
    
    try {
        // 读取总长度 (小端序)
        const totalLength = buffer.readUInt32LE(0);
        
        if (totalLength !== buffer.length) {
            return { 
                error: `长度不匹配: 头部显示${totalLength}字节, 实际${buffer.length}字节` 
            };
        }
        
        if (buffer.length < 12) { // 4 + 2 + 4 + 2 = 12字节最小头部
            return { error: '头部数据不完整' };
        }
        
        // 解析头部字段
        const messageId = buffer.readUInt16LE(4);      // 消息ID
        const clientSeqId = buffer.readInt32LE(6);     // 客户端序列号  
        const serverId = buffer.readUInt16LE(10);      // 服务器ID
        
        // 消息体数据
        const messageData = buffer.slice(12);
        
        return {
            totalLength,
            messageId,
            clientSeqId,
            serverId,
            messageData,
            messageDataLength: messageData.length
        };
    } catch (error) {
        return { error: `解析失败: ${error.message}` };
    }
}

// 创建响应数据包
function createResponse(originalPacket, responseData = null) {
    // 如果没有响应数据，直接回显原始数据
    if (!responseData) {
        return originalPacket;
    }
    
    // 解析原始包
    const parsed = parseGameProtocol(originalPacket);
    if (parsed.error) {
        return originalPacket; // 解析失败，回显原数据
    }
    
    // 创建响应包
    const responseBuffer = Buffer.from(responseData, 'utf8');
    const totalLength = 12 + responseBuffer.length; // 头部 + 数据
    
    const response = Buffer.alloc(totalLength);
    let offset = 0;
    
    // 写入总长度
    response.writeUInt32LE(totalLength, offset);
    offset += 4;
    
    // 写入消息ID (可以修改为响应消息ID)
    response.writeUInt16LE(parsed.messageId + 1, offset);
    offset += 2;
    
    // 写入客户端序列号 (保持一致)
    response.writeInt32LE(parsed.clientSeqId, offset);
    offset += 4;
    
    // 写入服务器ID
    response.writeUInt16LE(parsed.serverId, offset);
    offset += 2;
    
    // 写入响应数据
    responseBuffer.copy(response, offset);
    
    return response;
}

// 客户端连接计数
let clientCount = 0;

// 创建服务器
const server = net.createServer((socket) => {
    const clientId = ++clientCount;
    const clientAddr = `${socket.remoteAddress}:${socket.remotePort}`;
    
    log(`🔗 客户端#${clientId} 已连接: ${clientAddr}`);
    
    // 设置socket选项
    socket.setKeepAlive(true, 60000); // 60秒心跳
    socket.setTimeout(300000);        // 5分钟超时
    
    // 存储不完整的数据
    let incompleteBuffer = Buffer.alloc(0);
    
    // 处理接收数据
    socket.on('data', (data) => {
        try {
            log(`📨 客户端#${clientId} 发送了 ${data.length} 字节`);
            
            // 如果启用二进制日志，输出十六进制
            if (CONFIG.enableBinaryLog) {
                log(`📨 客户端#${clientId} 数据:\n${hexDump(data)}`);
            }
            
            // 合并不完整的数据
            const fullBuffer = Buffer.concat([incompleteBuffer, data]);
            incompleteBuffer = Buffer.alloc(0);
            
            let offset = 0;
            
            // 处理可能包含多个消息的缓冲区
            while (offset < fullBuffer.length) {
                // 检查是否有足够的字节读取长度字段
                if (offset + 4 > fullBuffer.length) {
                    incompleteBuffer = fullBuffer.slice(offset);
                    break;
                }
                
                // 读取消息长度
                const messageLength = fullBuffer.readUInt32LE(offset);
                
                // 检查是否有完整的消息
                if (offset + messageLength > fullBuffer.length) {
                    incompleteBuffer = fullBuffer.slice(offset);
                    break;
                }
                
                // 提取单个消息
                const messageBuffer = fullBuffer.slice(offset, offset + messageLength);
                
                // 处理单个消息
                processMessage(clientId, socket, messageBuffer);
                
                offset += messageLength;
            }
            
        } catch (error) {
            log(`❌ 客户端#${clientId} 数据处理错误: ${error.message}`);
        }
    });
    
    // 处理连接错误
    socket.on('error', (err) => {
        log(`❌ 客户端#${clientId} 连接错误: ${err.message}`);
    });
    
    // 处理连接超时
    socket.on('timeout', () => {
        log(`⏰ 客户端#${clientId} 连接超时`);
        socket.end();
    });
    
    // 处理连接关闭
    socket.on('close', (hadError) => {
        log(`🔌 客户端#${clientId} 连接关闭${hadError ? ' (异常)' : ' (正常)'}`);
    });
    
    // 发送欢迎消息 (如果需要)
    // socket.write('Welcome to Unity SuperSocket Test Server!\n');
});

// 处理单个消息
function processMessage(clientId, socket, messageBuffer) {
    log(`🔍 处理客户端#${clientId}的消息 (${messageBuffer.length} 字节)`);
    
    if (CONFIG.enableProtocolParsing) {
        const parsed = parseGameProtocol(messageBuffer);
        
        if (parsed.error) {
            log(`❌ 客户端#${clientId} 协议解析失败: ${parsed.error}`);
        } else {
            log(`✅ 客户端#${clientId} 协议解析成功:`);
            log(`   - 总长度: ${parsed.totalLength}`);
            log(`   - 消息ID: ${parsed.messageId}`);
            log(`   - 客户端序列号: ${parsed.clientSeqId}`);
            log(`   - 服务器ID: ${parsed.serverId}`);
            log(`   - 消息数据长度: ${parsed.messageDataLength}`);
            
            if (parsed.messageData.length > 0) {
                // 尝试解析为UTF-8字符串
                try {
                    const messageText = parsed.messageData.toString('utf8');
                    log(`   - 消息内容: "${messageText}"`);
                } catch (e) {
                    log(`   - 消息数据 (十六进制):\n${hexDump(parsed.messageData)}`);
                }
            }
        }
    }
    
    // 创建响应
    let response;
    
    // 根据消息类型创建不同的响应
    if (CONFIG.enableProtocolParsing) {
        const parsed = parseGameProtocol(messageBuffer);
        if (!parsed.error) {
            // 创建回显响应
            const echoMessage = `Server received: ${parsed.messageData.toString('utf8')}`;
            response = createResponse(messageBuffer, echoMessage);
        } else {
            response = messageBuffer; // 直接回显
        }
    } else {
        response = messageBuffer; // 直接回显
    }
    
    // 发送响应
    socket.write(response, (err) => {
        if (err) {
            log(`❌ 客户端#${clientId} 发送响应失败: ${err.message}`);
        } else {
            log(`📤 客户端#${clientId} 响应已发送 (${response.length} 字节)`);
            
            if (CONFIG.enableBinaryLog) {
                log(`📤 客户端#${clientId} 响应数据:\n${hexDump(response)}`);
            }
        }
    });
}

// 处理服务器错误
server.on('error', (err) => {
    log(`💥 服务器错误: ${err.message}`);
});

// 启动服务器
server.listen(CONFIG.port, CONFIG.host, () => {
    log(`🚀 Unity SuperSocket 测试服务器已启动`);
    log(`📍 监听地址: ${CONFIG.host}:${CONFIG.port}`);
    log(`📝 日志文件: ${CONFIG.logFile}`);
    log(`🔧 协议解析: ${CONFIG.enableProtocolParsing ? '启用' : '禁用'}`);
    log(`📊 二进制日志: ${CONFIG.enableBinaryLog ? '启用' : '禁用'}`);
    log(`✨ 准备接受客户端连接...`);
    log('');
});

// 优雅关闭
process.on('SIGINT', () => {
    log('📝 收到关闭信号，正在关闭服务器...');
    server.close(() => {
        log('👋 测试服务器已关闭');
        process.exit(0);
    });
});

process.on('SIGTERM', () => {
    log('📝 收到终止信号，正在关闭服务器...');
    server.close(() => {
        log('👋 测试服务器已关闭');
        process.exit(0);
    });
});

using System;
using Utility;
using UnityEngine;
using System.Collections.Generic;
using SuperSocket.Connection;
using UnitySuperSocket;
using SuperSocket.Log;

namespace ELEXNetwork
{
	
	public partial class NetManager : MonoBehaviourSingle<NetManager>
	{
				//ELEXNet连接方式-使用新的SuperSocket实现;
		private UnitySuperSocketClient m_NetClient = new UnitySuperSocketClient();
		
		// 连接事件
		public event EventHandler<NetworkConnectEventArgs> NetworkConnected;
		public event EventHandler<CloseEventArgs> NetworkDisconnected;
		
		private void Awake()
		{
			// 连接事件
			m_NetClient.NetworkConnected += OnNetworkConnected;
			m_NetClient.NetworkDisconnected += OnNetworkDisconnected;
		}

		public override void Clear()
		{
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogInfo($"[NetManager] Clear");
			#endif
			// 断开连接
			DisConnect();
		}
        
		public void Connect(string host, int port)
		{
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogInfo($"[NetManager] 开始连接: {host}:{port}");
			#endif
			//RegisterDisconnectDelegate(null);
			//m_NetClient.DisConnect();
			m_NetClient.Connect(host, port);
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogInfo($"[NetManager] 连接结束");
			#endif
		}

		public void DisConnect()
		{
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogInfo($"[NetManager] 断开连接");
			#endif
			/*	
			if (silence)
			{
				RegisterDisconnectDelegate(null);
			}
			*/
			m_NetClient.DisConnect();
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogInfo($"[NetManager] 断开连接成功");
			#endif
		}
		
        public void DisConnectTimeout()
        {
            #if ENABLE_SUPERSOCKET_LOG	
            NetLogUtil.LogInfo($"[NetManager] 断开连接超时");
            #endif
            m_NetClient.DisConnectTimeout();
        }

        public void DisConnectGM()
        {
            #if ENABLE_SUPERSOCKET_LOG	
            NetLogUtil.LogInfo($"[NetManager] GM断开连接");
            #endif
	        m_NetClient.DisConnectGM();
        }

        public void Send<T>(ushort msgId, ushort serverId, T data,out int sequenceId) where T : class
		{
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogSend($"[NetManager] 发送消息: MsgId={msgId}, ServerId={serverId}, DataType={typeof(T).Name}");
			#endif
			m_NetClient.Send(msgId, serverId, data,out sequenceId);
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogSend($"[NetManager] 发送消息完成: {sequenceId}");
			#endif
		}
		
		public void SendBuffer(int msgId, int serverId, byte[] buf,out int sequenceId)
		{
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogSend($"[NetManager] 发送消息: MsgId={msgId}, ServerId={serverId}, BufferLength={buf?.Length ?? 0}");
			#endif
			m_NetClient.SendBuffer((ushort)msgId, (ushort)serverId, buf,out sequenceId);
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogSend($"[NetManager] 发送消息完成: {sequenceId}");
			#endif
		}

		/*
		public void RegisterDisconnectDelegate(System.Action<int> func)
		{
			m_NetClient.DisconnectedHandler = func;
		}
		*/

		public void Register(int msgId, MsgPBCallbackDelegateCommon callback)
		{
			m_NetClient.Register(msgId, callback);
		}

		public void RegisterCommonCallback(MsgCommonCallbackDelegate callback)
		{
			m_NetClient.RegisterCommonCallback(callback);
		}

        void OnApplicationQuit()
		{
			DisConnect();
		}

        // void Awake()
        // {
	       //  FlushNetworkStatus();
        // }

		void OnDestroy()
		{
			// 取消连接事件
			m_NetClient.NetworkConnected -= OnNetworkConnected;
			m_NetClient.NetworkDisconnected -= OnNetworkDisconnected;
			
			m_NetClient.DisConnect();
		}
		
		/// <summary>
		/// 连接成功事件		
		/// </summary>
		/// <param name="sender">发送方</param>	
		/// <param name="e">连接成功事件参数</param>
		private void OnNetworkConnected(object sender, NetworkConnectEventArgs e)
		{
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogInfo($"[NetManager] 连接结果: Success={e.Success}, Host={e.Host}, Port={e.Port}");
			#endif
			if (!e.Success && !string.IsNullOrEmpty(e.ErrorMessage))
			{
				#if ENABLE_SUPERSOCKET_LOG	
				NetLogUtil.LogError($"[NetManager] 连接失败: {e.ErrorMessage}");
				#endif
			}
			
				// 触发连接成功事件
			NetworkConnected?.Invoke(this, e);
		}
		
		/// <summary>
		/// 连接断开事件
		/// </summary>
		/// <param name="sender">发送方</param>
		/// <param name="e">连接断开事件参数</param>
		private void OnNetworkDisconnected(object sender, CloseEventArgs e)
		{
			#if ENABLE_SUPERSOCKET_LOG	
			NetLogUtil.LogInfo($"[NetManager] 连接断开: Reason={e.Reason}");
			#endif
			
			// 触发连接断开事件
			NetworkDisconnected?.Invoke(this, e);
		}

		void Update()
		{
			// 每帧更新网络状态
			bool updateResult = m_NetClient.UpdateNetwork(Time.deltaTime);
			if (!updateResult)
			{
				#if ENABLE_SUPERSOCKET_LOG	
				NetLogUtil.LogWarning($"[NetManager] 更新网络状态失败");
				#endif
			}
			// UpdateNetworkStatus();
		}

		public bool IsConnected
		{
			get
			{
				if (m_NetClient == null)
					return false;

				return m_NetClient.IsConnected;
			}
		}
		
		public static string GetDomainFromUrl(string url)
		{
			try
			{
				Uri uri = new Uri(url);
				return uri.Host;
			}
			catch (Exception e)
			{
				#if ENABLE_SUPERSOCKET_LOG	
				NetLogUtil.LogError(e.Message);
				#endif
				//throw;
				return url;
			}
			
		}
	}
}

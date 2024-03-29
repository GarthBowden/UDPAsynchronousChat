﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UDPAsynchronousChat
{
    public class UDPAsyncChatClient
    {
        Socket mBroadcastSender;
        IPEndPoint IPEPLocal;
        IPEndPoint IPEPServer;
        public EventHandler<TextUpdateEventArgs> RaiseTextUpdateEvent;
        private EndPoint mChatServerEP;

        public UDPAsyncChatClient(int _localPort = 0, int _remotePort = 0)
        {
            IPEPLocal = new IPEndPoint(IPAddress.Any, _localPort);
            IPEPServer = new IPEndPoint(IPAddress.Broadcast, _remotePort);

            mBroadcastSender = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            mBroadcastSender.EnableBroadcast = true;
        }
        protected virtual void OnRaiseTextUpdateEvent(TextUpdateEventArgs e)
        {
            EventHandler<TextUpdateEventArgs> handler = RaiseTextUpdateEvent;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public void SendBroadcast(string strDataforBroadcast)
        {
            if(String.IsNullOrEmpty(strDataforBroadcast))
            {
                Console.WriteLine("UDPAsyncChatClient.SendBroadcast was passed null or empty string!");
                return;
            }
            try
            {
                if(!mBroadcastSender.IsBound)
                {
                    mBroadcastSender.Bind(IPEPLocal);
                }

                byte[] databytes = Encoding.ASCII.GetBytes(strDataforBroadcast);

                SocketAsyncEventArgs saeArgs = new SocketAsyncEventArgs();
                saeArgs.SetBuffer(databytes, 0, databytes.Length);
                saeArgs.RemoteEndPoint = IPEPServer;

                saeArgs.Completed += SendCompletedCallback;
                mBroadcastSender.SendToAsync(saeArgs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void SendCompletedCallback(object sender, SocketAsyncEventArgs e)
        {
            OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                $"Data({e.Count} bytes) sent to {e.RemoteEndPoint.ToString()}"));
            if(Encoding.ASCII.GetString(e.Buffer, 0, e.BytesTransferred).Equals("<discover>"))
            {
                ReceiveTextFromServer(expectedValue: "<confirm>", IPEPReceiverLocal: IPEPLocal);
            }
        }

        private void ReceiveTextFromServer(string expectedValue, IPEndPoint IPEPReceiverLocal)
        {
            if(IPEPReceiverLocal == null)
            {
                return;
            }
            SocketAsyncEventArgs saeConfirmArgs = new SocketAsyncEventArgs();
            saeConfirmArgs.SetBuffer(new byte[1024], 0, 1024);
            saeConfirmArgs.RemoteEndPoint = IPEPReceiverLocal;

            saeConfirmArgs.UserToken = expectedValue;
            saeConfirmArgs.Completed += ReceiveConfirmationCompleted;

            mBroadcastSender.ReceiveFromAsync(saeConfirmArgs);
        }

        private void ReceiveConfirmationCompleted(object sender, SocketAsyncEventArgs e)
        {
            if(e.BytesTransferred == 0) return;
            string receivedText = Encoding.ASCII.GetString(e.Buffer, 0, e.BytesTransferred);
            string UserTokenString = Convert.ToString(e.UserToken);
            if (receivedText.Equals(UserTokenString))
            {
                OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                    $"Received confirmation from server"));
                mChatServerEP = e.RemoteEndPoint;

            }
            else if(string.IsNullOrEmpty(UserTokenString) && !string.IsNullOrEmpty(receivedText))
            {
                OnRaiseTextUpdateEvent(new TextUpdateEventArgs(receivedText));
            }
            else
            {
                OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                    $"Received unexpected usertoken: {receivedText} with usertoken {UserTokenString}"));
            }
            ReceiveTextFromServer(string.Empty, mChatServerEP as IPEndPoint);
        }

        public void SendMessageToKnownServer(string message)
        {
            try
            {
                if(string.IsNullOrEmpty(message))
                {
                    return;
                }

                Byte[] bytesToSend = Encoding.ASCII.GetBytes(message);
                SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
                saea.SetBuffer(bytesToSend, 0, bytesToSend.Length);
                saea.RemoteEndPoint = mChatServerEP;

                saea.UserToken = message;

                saea.Completed += SendMessageToKnownServerCompletedCallback;

                mBroadcastSender.SendToAsync(saea);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
               
            }
        }

        private void SendMessageToKnownServerCompletedCallback(object sender, SocketAsyncEventArgs e)
        {
            Console.WriteLine($"Sent: {e.UserToken} Server: {e.RemoteEndPoint}");
        }
    }
}

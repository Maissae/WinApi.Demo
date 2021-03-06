﻿using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using static WinApi.Demo.Signatures.Ws2_32;

namespace WinApi.Demo.Chat.Server.Common
{
    public class MessageHandler
    {
        private static int connectionId = 0;
        private ConcurrentDictionary<int, IntPtr> connectedClients;
        private ConnectionHandler _connectionHandler;

        public MessageHandler(ConnectionHandler connectionHandler)
        {
            connectedClients = new ConcurrentDictionary<int, IntPtr>();
            _connectionHandler = connectionHandler;
            _connectionHandler.ClientConnected += ClientConnected;
        }

        private void ClientConnected(object sender, IntPtr e)
        {
            if (connectedClients.TryAdd(connectionId, e) == false)
                Console.WriteLine("Error! Could not add client!");
            connectionId++;
            Console.WriteLine($"Clients connected: {connectedClients.Count}");
            Task.Factory.StartNew(() => Handle(e), TaskCreationOptions.LongRunning);
            //Task.Run(() => Handle(e));
        }

        private void BroadcastMessage(string message)
        {
            byte[] buff = Encoding.ASCII.GetBytes(message);

            foreach (var client in connectedClients.Keys)
            {
                if (connectedClients.TryGetValue(client, out var connection) == false)
                {
                    Console.WriteLine("Error! Could not get client from connection list!");
                    continue;
                }
                var iSendResult = send(connection, //We pick a connection we want to send data to.
                    buff, //Buffer, our data payload in bytes.
                    buff.Length, //Length of our buffer.
                    0); //Flags, either MSG_DONTROUTE or MSG_OOB.
                if (iSendResult == SOCKET_ERROR)
                {
                    Console.WriteLine($"Error, Sending failed. Code: {WSAGetLastError()}");
                    Cleanup(client);
                }
                Console.WriteLine($"Bytes sent: {iSendResult}");
            }
        }

        private void Cleanup(int client)
        {
            if (connectedClients.TryRemove(client, out var connection) == false)
            {
                Console.WriteLine("Error! Could not remove client from connection list!");
                return;
            }
            shutdown(connection, 2); //We shutdown the socket, both ways send/recv.
            closesocket(connection); //We close the socket and free it.
            Console.WriteLine($"Clients connected: {connectedClients.Count}");
        }

        public async Task Handle(IntPtr client)
        {            
            while (true)
            {
                int iResult = 0;
                do
                {
                    byte[] buffer = new byte[512];
                    iResult = recv(client, //Socket connection we want to recieve data from.
                        buffer, //Buffer we load data into.
                        buffer.Length, //Length of the buffer.
                        0); //Flag, MSG_PEEK, MSG_OOB or MSG_WAITALL.
                    if (iResult > 0)
                    {
                        Console.WriteLine($"Bytes recieved: {iResult}");
                        string msg = Encoding.ASCII.GetString(buffer).TrimEnd('\0');
                        BroadcastMessage(msg);
                    }
                } while (iResult > 0);
            }
        }
    }
}

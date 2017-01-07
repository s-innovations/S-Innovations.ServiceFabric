using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace SInnovations.ServiceFabric.Storage.Configuration
{
    public class FileCache : TokenCache
    {
        private readonly ILogger Logger;
        private readonly IDataProtector Protector;
        public string CacheFilePath;
        private static readonly object FileLock = new object();


        public FileCache(
           ILoggerFactory loggerFactory,
           IDataProtectionProvider dataProtectionProvider
            ) : this(loggerFactory, dataProtectionProvider, @".\TokenCache.dat")
        {

        }
        // Initializes the cache against a local file.
        // If the file is already present, it loads its content in the ADAL cache
        public FileCache(
            ILoggerFactory loggerFactory,
            IDataProtectionProvider dataProtectionProvider,
            string filePath)
        {
            CacheFilePath = filePath;
            Logger = loggerFactory.CreateLogger<FileCache>();
            Protector = dataProtectionProvider.CreateProtector(typeof(FileCache).FullName);

            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            lock (FileLock)
            {
                this.Deserialize(File.Exists(CacheFilePath) ?
                    Protector.Unprotect(File.ReadAllBytes(CacheFilePath))
                    : null);
            }
        }

        // Empties the persistent store.
        public override void Clear()
        {
            base.Clear();
            File.Delete(CacheFilePath);
        }

        // Triggered right before ADAL needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
        void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                this.Deserialize(File.Exists(CacheFilePath) ?
                    Protector.Unprotect(File.ReadAllBytes(CacheFilePath))
                    : null);
            }
        }

        // Triggered right after ADAL accessed the cache.
        void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (this.HasStateChanged)
            {
                lock (FileLock)
                {
                    // reflect changes in the persistent store
                    File.WriteAllBytes(CacheFilePath,
                        Protector.Protect(this.Serialize()));
                    // once the write operation took place, restore the HasStateChanged bit to false
                    this.HasStateChanged = false;
                }
            }
        }
    }
}

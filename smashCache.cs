using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Resources;
using System.Windows.Threading;
using Windows.Storage;

namespace ZomatoApp
{
    /// <summary>
    /// An awesome key value caching library
    /// Created By: Adhityan
    /// </summary>
    class smashCache
    {
        private const string folderName = "Cache";
        private const string persistanceKey = "Persist";

        private const float keepCount   = 1000;
        private const float dayCount    = 7;
        private const ushort garbageCan = 9;

        private static readonly object wlock;

        static smashCache()
        {
            init();

            wlock = new object();
            schedule();
        }

        public static void init()
        {
            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!iS.DirectoryExists(folderName))
                {
                    iS.CreateDirectory(folderName);
                }
            }
        }

        public static string hash(string o)
        {
            SHA1Managed s = new SHA1Managed();
            UTF8Encoding enc = new UTF8Encoding();
            s.ComputeHash(enc.GetBytes(o.ToCharArray()));
            return BitConverter.ToString(s.Hash).Replace("-", "").ToUpperInvariant();
        }

        public static bool contains(string key)
        {
            string fileName = hash(key);
            string filePath = System.IO.Path.Combine(folderName, fileName);

            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (iS.FileExists(filePath)) return true;
                else return false;
            }
        }

        public static string get(string key)
        {
            #region cacheGetFromStorage
            /*string result = null;
            StorageFolder local = Windows.Storage.ApplicationData.Current.LocalFolder;

            // Get the DataFolder folder.
            var dataFolder = await local.GetFolderAsync("Cache");

            // Get the file.
            var file = await dataFolder.OpenStreamForReadAsync(hash(key));

            // Read the data.
            using (StreamReader streamReader = new StreamReader(file))
            {
                result = streamReader.ReadToEnd();
            }

            return result;*/
            #endregion

            string fileName = hash(key);
            string filePath = System.IO.Path.Combine(folderName, fileName);

            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!iS.FileExists(filePath)) return null;
                else
                {
                    using (IsolatedStorageFileStream fileStream = iS.OpenFile(filePath, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader reader = new StreamReader(fileStream))
                        {
                            reader.ReadLine();
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }

        public static void set(string key, string value, bool persist = false)
        {
            #region cacheSetFromStorage
            /*byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(value);

            // Get the local folder.
            StorageFolder local = Windows.Storage.ApplicationData.Current.LocalFolder;

            // Create a new folder name DataFolder.
            var dataFolder = await local.CreateFolderAsync("Cache", CreationCollisionOption.OpenIfExists);

            // Create a new file named DataFile.txt.
            var file = await dataFolder.CreateFileAsync(hash(key), CreationCollisionOption.ReplaceExisting);

            // Write the data from the textbox.
            var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var writer = new StreamWriter(stream.AsStreamForWrite());
            writer.Write(value);
            writer.Close();
             
             
            StreamResourceInfo streamResourceInfo = Application.GetResourceStream(new Uri(filePath, UriKind.Relative));*/
            #endregion

            string fileName = hash(key);
            string filePath = System.IO.Path.Combine(folderName, fileName);

            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                lock (wlock)
                {
                    using (IsolatedStorageFileStream fileStream = iS.OpenFile(filePath, FileMode.Create, FileAccess.Write))
                    {
                        using (StreamWriter writer = new StreamWriter(fileStream))
                        {
                            string parameters = "";
                            if (persist) parameters = persistanceKey;
                            writer.WriteLine(parameters);

                            writer.Write(value);
                        }
                    }
                }
            }

            //CrittercismSDK.Crittercism.LeaveBreadcrumb("smashCache Key was set [Key: " + key + "]");
        }

        public static void delete(string key)
        {
            string fileName = hash(key);
            string filePath = System.IO.Path.Combine(folderName, fileName);

            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                lock (wlock)
                {
                    if (iS.FileExists(filePath))
                    {
                        iS.DeleteFile(filePath);
                    }
                }
            }
        }

        public static void DeleteDirectoryRecursive(string dir)
        {
            if (String.IsNullOrEmpty(dir)) return;

            using (var isoFiles = IsolatedStorageFile.GetUserStoreForApplication())
            {
                lock (wlock)
                {
                    foreach (var file in isoFiles.GetFileNames(dir + "\\*"))
                    {
                        var filename = dir + "/" + file;
                        if (isoFiles.FileExists(filename))
                            isoFiles.DeleteFile(filename);
                    }

                    foreach (var subdir in isoFiles.GetDirectoryNames(dir))
                    {
                        var dirname = dir + subdir + "\\";
                        if (isoFiles.DirectoryExists(dirname))
                            DeleteDirectoryRecursive(dirname);
                    }

                    var currentDirname = dir.TrimEnd('\\');
                    if (isoFiles.DirectoryExists(currentDirname))
                        isoFiles.DeleteDirectory(currentDirname);
                }
            }
        }

        public static void clear()
        {
            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                lock (wlock)
                {
                    if (iS.DirectoryExists(folderName))
                    {
                        DeleteDirectoryRecursive(folderName);
                        iS.CreateDirectory(folderName);
                    }
                }
            }

            //CrittercismSDK.Crittercism.LeaveBreadcrumb("smashCache was cleared");
        }

        private static void garbageCollect(float keep = keepCount, float day = dayCount)
        {
            new Thread(() =>
            {
                ushort count = 0;
                List<string> orderedFiles = getSortedFileNames();

                using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    lock (wlock)
                    {
                        while (orderedFiles.Count > keep)
                        {
                            string curiosity = orderedFiles.First();

                            try
                            {
                                if (!doesKeyPersist(curiosity))
                                {
                                    iS.DeleteFile(curiosity);
                                }
                                else count++;
                            }
                            catch { }

                            orderedFiles.Remove(curiosity);
                        }

                        for (int i = 0; i < orderedFiles.Count; i++)
                        {
                            string curiosity = orderedFiles[i];
                            DateTimeOffset x = iS.GetLastAccessTime(curiosity);

                            if (x.AddDays(day) < DateTime.Now)
                            {
                                try
                                {
                                    if (!doesKeyPersist(curiosity))
                                    {
                                        iS.DeleteFile(curiosity);
                                    }
                                    else count++;
                                }
                                catch { }

                                orderedFiles.Remove(curiosity);
                                i--;
                            }
                            else break;
                        }
                    }
                }

                if (Debugger.IsAttached)
                {
                    Debug.WriteLine("Cache GC completed (" + count + " keys removed)");
                }

                //CrittercismSDK.Crittercism.LeaveBreadcrumb("smashCache Garbage Collector ran and destroyed some Keys [Count: " + count + "]");
            }) { IsBackground = true }.Start();
        }

        private static bool doesKeyPersist(string record)
        {
            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!iS.FileExists(record)) return false;
                else
                {
                    using (IsolatedStorageFileStream fileStream = iS.OpenFile(record, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader reader = new StreamReader(fileStream))
                        {
                            var parameters = reader.ReadLine();
                            return (parameters == persistanceKey);
                        }
                    }
                }
            }
        }

        private static List<string> getSortedFileNames()
        {
            using (IsolatedStorageFile iS = IsolatedStorageFile.GetUserStoreForApplication())
            {
                string pattern = string.Format("{0}\\*.*", folderName);
                string[] files = iS.GetFileNames(pattern);

                SortedDictionary<long, string> order = new SortedDictionary<long, string>();
                
                foreach (var filePath in files)
                {
                    DateTimeOffset x = iS.GetLastAccessTime(filePath);
                    
                    long key = x.Ticks;
                    while (order.ContainsKey(key)) key--;

                    order.Add(key, filePath);
                }

                return order.Values.ToList();
            }
        }

        private static void schedule()
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                DispatcherTimer t = new DispatcherTimer();
                t.Interval = TimeSpan.FromMinutes(garbageCan);
                t.Tick += ((x, e) =>
                {
                    garbageCollect();
                    t.Stop();
                });
                t.Start();
            });
        }
    }
}

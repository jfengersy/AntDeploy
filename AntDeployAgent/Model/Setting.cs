﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntDeployAgentWindows.Model
{
    public class Setting
    {
        //private static readonly System.Threading.Timer mDetectionTimer;
        private static readonly int _clearOldPublishFolderOverDays = 10;
        private static readonly int _oldPulishLimit = 10;
        private static readonly List<string> MacWhiteList = new List<string>();
        /// <summary>
        /// 是否开启备份
        /// </summary>
        /// <returns></returns>
        public static bool NeedBackUp = true;
        static Setting()
        {
            //#if DEBUG
            //             mDetectionTimer = new System.Threading.Timer(OnVerify, null, 1000 * 5, 1000 * 5);
            //#else
            //             mDetectionTimer = new System.Threading.Timer(OnVerify, null, 1000 * 60 * 30, 1000 * 60 * 30);
            //#endif

            var oldPulishLimit = System.Configuration.ConfigurationManager.AppSettings["OldPulishLimit"];
            if (int.TryParse(oldPulishLimit, out int value1) && value1 > 0)
            {
                _oldPulishLimit = value1;
            }

            var clearOldPublishFolderOverDaysStr = System.Configuration.ConfigurationManager.AppSettings["ClearOldPublishFolderOverDays"];
            if (int.TryParse(clearOldPublishFolderOverDaysStr, out int value) && value > 0)
            {
                _clearOldPublishFolderOverDays = value;
            }

            var _whiteMacList = System.Configuration.ConfigurationManager.AppSettings["MacWhiteList"];
            if (!string.IsNullOrEmpty(_whiteMacList))
            {
                MacWhiteList = _whiteMacList.Split(',').Distinct().ToList();
            }

            var needBackUp = System.Configuration.ConfigurationManager.AppSettings["NeedBackUp"];
            if (!string.IsNullOrEmpty(needBackUp) && needBackUp.ToLower().Equals("false"))
            {
                NeedBackUp = false;
            }
        }


        public static string WebRootPath = "";

        public static string PublishPathFolder = "";

        public static string PublishIIsPathFolder = "";
        public static string BackUpIIsPathFolder = "";


        public static string PublishWindowServicePathFolder = "";
        public static string BackUpWindowServicePathFolder = "";

        public static void InitWebRoot(string rootPath, bool useCustomer = false)
        {

            if (string.IsNullOrEmpty(rootPath))
            {
                return;
            }

            WebRootPath = rootPath;

            PublishPathFolder = (useCustomer) ? rootPath : Path.Combine(WebRootPath, "antdeploy");

            if (!Directory.Exists(PublishPathFolder))
            {
                Directory.CreateDirectory(PublishPathFolder);
            }


            PublishIIsPathFolder = Path.Combine(PublishPathFolder, "iis");

            if (!Directory.Exists(PublishIIsPathFolder))
            {
                Directory.CreateDirectory(PublishIIsPathFolder);
            }

            BackUpIIsPathFolder = Path.Combine(PublishPathFolder, "iis_backup");

            if (!Directory.Exists(BackUpIIsPathFolder))
            {
                Directory.CreateDirectory(BackUpIIsPathFolder);
            }

            PublishWindowServicePathFolder = Path.Combine(PublishPathFolder, "window_service");

            if (!Directory.Exists(PublishWindowServicePathFolder))
            {
                Directory.CreateDirectory(PublishWindowServicePathFolder);
            }

            BackUpWindowServicePathFolder = Path.Combine(PublishPathFolder, "window_service_backup");

            if (!Directory.Exists(BackUpWindowServicePathFolder))
            {
                Directory.CreateDirectory(BackUpWindowServicePathFolder);
            }
        }

        public static void ClearOldFolders(bool isIis,string projectFolderName)
        {

            new Task(() =>
            {
                if (isIis)
                {
                    CheckOldFolder(PublishIIsPathFolder, projectFolderName);
                    CheckOldFolder(BackUpIIsPathFolder, projectFolderName);
                }
                else
                {

                    CheckOldFolder(PublishWindowServicePathFolder, projectFolderName);
                    CheckOldFolder(BackUpWindowServicePathFolder, projectFolderName);
                }
            
            }).Start();
        }

        //        private static void OnVerify(object state)
        //        {
        //            mDetectionTimer.Change(-1, -1);
        //            try
        //            {
        //                ClearOldFolders();
        //            }
        //            catch
        //            {
        //                // ignored
        //            }
        //            finally
        //            {
        //#if DEBUG
        //                mDetectionTimer.Change(1000 * 5, 1000 * 5);
        //#else
        //                mDetectionTimer.Change(1000 * 60 * 30, 1000 * 60 * 30);
        //#endif

        //            }
        //        }


        /// <summary>
        /// 清除老的发布版本记录 (不会删除正在使用的版本 即使已经到期)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="projectFolder"></param>
        private static void CheckOldFolder(string path, string projectFolder)
        {
            try
            {
                var now = DateTime.Now.Date;
                if (string.IsNullOrEmpty(path)) return;

                if (!Directory.Exists(path)) return;

                var applicationFolders = !string.IsNullOrEmpty(projectFolder) ? new List<string> { Path.Combine(path,projectFolder) }.ToArray() : Directory.GetDirectories(path);
                if (applicationFolders.Length < 1) return;

                foreach (var applicationFolder in applicationFolders)
                {
                    var subFolders = Directory.GetDirectories(applicationFolder);
                    if (subFolders.Length < _oldPulishLimit) continue;//还没超过最低的保留记录数
                    //找到current.txt文件 记录着当前正在使用的版本
                    var currentText = Path.Combine(applicationFolder, "current.txt");
                    var currentVersion = "";
                    if (File.Exists(currentText))
                    {
                        currentVersion = File.ReadAllText(currentText);
                    }
                    //超过了就要比对文件夹的日期 把超过了x天数的就要删掉
                    var oldFolderList = new List<OldFolder>();
                    foreach (var subFolder in subFolders)
                    {
                        var folder = new DirectoryInfo(subFolder);
                        var folderName = folder.Name;
                        folderName = folderName.Replace("Backup", "").Replace("Err", "").Replace("_", "");
                        if (!DateTime.TryParseExact(folderName, "yyyyMMddHHmmss", null, DateTimeStyles.None, out DateTime createDate))
                        {
                            continue;
                        }

                        var days = (now - createDate.Date).TotalDays;

                        oldFolderList.Add(new OldFolder
                        {
                            Name = folder.Name,
                            FullName = subFolder,
                            DateTime = createDate,
                            DiffDays = days//这个是当前日期和文件夹日期(也就是发布日期)进行相比的天数
                        });
                    }


                    var targetList = oldFolderList.OrderByDescending(r => r.DateTime)
                        .Where(r => r.DiffDays >= _clearOldPublishFolderOverDays)
                        .ToList();

                    var diff = subFolders.Length - targetList.Count;

                    if (diff >= 0 && diff < _oldPulishLimit)
                    {
                        targetList = targetList.Skip(_oldPulishLimit - diff).ToList();
                    }

                    foreach (var target in targetList)
                    {
                        //如果是当前正在使用的版本的话 不能删除
                        if (!string.IsNullOrEmpty(currentVersion) && target.Name.Equals(currentVersion))
                        {
                            continue;
                        }
                        try
                        {
                            Directory.Delete(target.FullName, true);
                        }
                        catch
                        {
                            //ignore
                        }
                    }


                }

            }
            catch
            {
                // ignored
            }
        }

        public static void StopWatchFolderTask()
        {
            //mDetectionTimer.Change(-1, -1);
            //mDetectionTimer.Dispose();
        }

        /// <summary>
        /// 检查是否mac地址白名单
        /// </summary>
        /// <param name="macAddress"></param>
        /// <returns></returns>
        public static bool CheckIsInWhiteMacList(string macAddress)
        {
            if (!MacWhiteList.Any()) return true;

            return MacWhiteList.Contains(macAddress);
        }

    }

    class OldFolder
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public DateTime DateTime { get; set; }
        public double DiffDays { get; set; }
    }
}

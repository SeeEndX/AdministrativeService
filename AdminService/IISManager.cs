﻿using Microsoft.Web.Administration;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace AdminService
{
    public class IISManager
    {
        private string cs = "server=localhost;user=root;database=admintooldb;password=1234;"; //строка подключения к БД
        FunctionExecutor executor;

        //класс информации о сайте
        public class SiteInfo
        {
            public string Name { get; set; }
            public string State { get; set; }
            public string Bindings { get; set; }
        }

        //класс информации о пулах приложений
        public class AppPoolInfo
        {
            public string Name { get; set; }
            public string State { get; set; }
            public string NETCLRVersion { get; set; }
            public string ManagedPipelineMode { get; set; }
        }

        //класс о функциях для пользователей (веб-разработчиков)
        public class ActionItem
        {
            public string Name { get; }
            public Action Action { get; }

            public ActionItem(string name, Action action)
            {
                Name = name;
                Action = action;
            }
        }

        //метод создания отчетов о действиях пользователя
        public void AddReport(string currentUser, string description)
        {
            using (MySqlConnection con = new MySqlConnection(cs)) //инициализация подключения
            {
                con.Open();

                string insertReportQuery = "INSERT INTO Reports (user, description, time) VALUES (@UserId, @Description, @Time);";
                using (MySqlCommand insertReportCmd = new MySqlCommand(insertReportQuery, con))
                {
                    insertReportCmd.Parameters.AddWithValue("@UserId", GetUserIdByUsername(currentUser));
                    insertReportCmd.Parameters.AddWithValue("@Description", description);
                    insertReportCmd.Parameters.AddWithValue("@Time", DateTime.Now.ToString());

                    insertReportCmd.ExecuteNonQuery();
                }
                con.Close();
            }
        }

        //получение ид пользователя по логину
        public int GetUserIdByUsername(string username)
        {
            int userId = -1;

            using (MySqlConnection con = new MySqlConnection(cs))
            {
                con.Open();

                string query = "SELECT id FROM Users WHERE login = @Username;";

                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Username", username);

                    object result = cmd.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out userId))
                    {
                        return userId;
                    }
                }
            }

            return userId;
        }

        //получение списка функций по логину пользователя
        public List<ActionItem> GetFunctionsForUser(string user)
        {
            List<ActionItem> functions = new List<ActionItem>();

            using (MySqlConnection con = new MySqlConnection(cs))
            {
                con.Open();

                string query = @"
            SELECT f.name
            FROM `Function` f
            JOIN Function_users fu ON f.id = fu.`function`
            JOIN Users u ON u.id = fu.user
            WHERE u.login = @User;";

                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@User", user);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string functionNameFromDb = reader.GetString(0);
                            Action action = () => executor.ExecuteMethodByName(functionNameFromDb);
                            functions.Add(new ActionItem(functionNameFromDb, action));
                        }
                    }
                }
            }

            return functions;
        }

        //получение списка пулов приложений IIS
        public List<AppPoolInfo> GetListOfAppPools()
        {
            List<AppPoolInfo> appPools = new List<AppPoolInfo>();

            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    foreach (ApplicationPool appPool in serverManager.ApplicationPools)
                    {
                        AppPoolInfo appPoolInfo = new AppPoolInfo
                        {
                            Name = appPool.Name,
                            State = appPool.State.ToString(),
                            NETCLRVersion = appPool.ManagedRuntimeVersion,
                            ManagedPipelineMode = appPool.ManagedPipelineMode.ToString()
                        };

                        appPools.Add(appPoolInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при получении списка пулов приложений: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return appPools;
        }

        //получение списка сайтов IIS
        public List<SiteInfo> GetListOfSites()
        {
            List<SiteInfo> sites = new List<SiteInfo>();

            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    foreach (Site site in serverManager.Sites)
                    {
                        string bindings = string.Join(", ", site.Bindings.Select(binding => binding.BindingInformation));

                        SiteInfo siteInfo = new SiteInfo
                        {
                            Name = site.Name,
                            State = site.State.ToString(),
                            Bindings = bindings
                        };

                        sites.Add(siteInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при получении списка сайтов: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return sites;
        }

        //метод запуска сайта IIS
        public string StartSite(string siteName)
        {
            var result = "";
            using (ServerManager serverManager = new ServerManager())
            {
                Site siteToStart = serverManager.Sites[siteName];

                if (siteToStart != null)
                {
                    string siteToStartPort = siteToStart.Bindings[0].EndPoint.Port.ToString();

                    foreach (Site site in serverManager.Sites)
                    {
                        if (site.Name != siteName && site.State == ObjectState.Started)
                        {
                            string sitePort = site.Bindings[0].EndPoint.Port.ToString();
                            if (sitePort == siteToStartPort)
                            {
                                result = "Порт уже занят другим сайтом";
                                return result;
                            }
                        }
                    }

                    if (siteToStart.State == ObjectState.Stopped)
                    {
                        siteToStart.Start();
                        serverManager.CommitChanges();
                    }
                }
            }
            return result;
        }

        //метод остановки сайта IIS
        public void StopSite(string siteName)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                Site site = serverManager.Sites[siteName];

                if (site != null)
                {
                    if (site.State == ObjectState.Started)
                    {
                        site.Stop();
                        serverManager.CommitChanges();
                    }
                }
                else
                {
                    //MessageBox.Show($"Сайт с именем '{siteName}' не найден.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        //метод создания сайта IIS
        public void CreateWebsite(string siteName, string physicalPath, int port)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    Site site = serverManager.Sites.Add(siteName, "http", $"*:{port}:", physicalPath);
                    serverManager.CommitChanges();
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при добавлении сайта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //метод удаления сайта IIS
        public void DeleteWebsite(string siteName)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    Site site = serverManager.Sites[siteName];
                    if (site != null)
                    {
                        serverManager.Sites.Remove(site);
                        serverManager.CommitChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при удалении сайта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //метод редактирования сайта IIS
        public void ModifyWebsite(string currentSiteName, string newSiteName, string newPhysicalPath)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    Site site = serverManager.Sites[currentSiteName];

                    if (site != null)
                    {
                        site.Name = newSiteName;
                        site.Applications[0].VirtualDirectories[0].PhysicalPath = newPhysicalPath;
                        serverManager.CommitChanges();
                    }
                    else
                    {
                        //MessageBox.Show($"Сайт с именем '{currentSiteName}' не найден.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при изменении сайта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //метод создания пула IIS
        public void CreatePool(string poolName, ManagedPipelineMode mode, int memoryLimit, int intervalMinutes)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    if (!serverManager.ApplicationPools.Any(ap => ap.Name.Equals(poolName)))
                    {
                        ApplicationPool newAppPool = serverManager.ApplicationPools.Add(poolName);
                        newAppPool.ManagedPipelineMode = mode; //конвейер обработки HTTP-запросов: Integrated или Classic.
                        newAppPool.ManagedRuntimeVersion = "v4.0";
                        newAppPool.Recycling.PeriodicRestart.Memory = memoryLimit;
                        newAppPool.Recycling.PeriodicRestart.Time = TimeSpan.FromMinutes(intervalMinutes);
                        serverManager.CommitChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при создании пула: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //метод удаления пула IIS
        public void DeletePool(string poolName)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ApplicationPool appPoolToDelete = serverManager.ApplicationPools[poolName];
                    if (appPoolToDelete != null)
                    {
                        serverManager.ApplicationPools.Remove(appPoolToDelete);
                        serverManager.CommitChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при удалении пула: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //метод редактирования пула IIS
        public void ModifyPool(string currentPoolName, string newPoolName, ManagedPipelineMode mode, int memoryLimit, int intervalMinutes)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ApplicationPool appPool = serverManager.ApplicationPools[currentPoolName];

                    if (appPool != null)
                    {
                        if (!string.IsNullOrEmpty(newPoolName))
                        {
                            appPool.Name = newPoolName;
                        }
                        appPool.ManagedPipelineMode = mode;
                        appPool.ManagedRuntimeVersion = "v4.0";
                        appPool.Recycling.PeriodicRestart.Memory = memoryLimit;
                        appPool.Recycling.PeriodicRestart.Time = TimeSpan.FromMinutes(intervalMinutes);
                        serverManager.CommitChanges();
                    }
                    else
                    {
                        //MessageBox.Show($"Пул приложений '{currentPoolName}' не найден.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при изменении пула: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //ДЛЯ БУДУЩЕЙ РЕАЛИЗАЦИИ
        /*public void ConfigureCompressionSettings(bool enableStatic, bool enableDynamic)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                Configuration config = serverManager.GetApplicationHostConfiguration();

                ConfigurationSection staticCompressionSection = config.GetSection("system.webServer/httpCompression/staticCompression");
                staticCompressionSection["enabled"] = enableStatic;

                ConfigurationSection dynamicCompressionSection = config.GetSection("system.webServer/httpCompression/dynamicCompression");
                dynamicCompressionSection["enabled"] = enableDynamic;

                serverManager.CommitChanges();
            }
        }

        public void EnableServerLogging(bool isLoggingEnabled)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                Configuration config = serverManager.GetApplicationHostConfiguration();
                ConfigurationSection httpLoggingSection = config.GetSection("system.webServer/httpLogging");
                if (isLoggingEnabled) httpLoggingSection["dontLog"] = false;
                else httpLoggingSection["dontLog"] = true;
                serverManager.CommitChanges();
            }
        }

        public void ConfigureLogSettings(string logFilePath, string logFormat, bool isLoggingEnabled)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                Configuration config = serverManager.GetApplicationHostConfiguration();

                ConfigurationSection logFileSection = config.GetSection("system.applicationHost/log");
                logFileSection["directory"] = logFilePath;
                logFileSection["logFormat"] = logFormat;

                serverManager.CommitChanges();
                serverManager.Dispose();
            }
            EnableServerLogging(isLoggingEnabled);
        }*/

        //метод запуска пула IIS
        public void StartAppPool(string appPoolName)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ApplicationPool appPool = serverManager.ApplicationPools[appPoolName];

                    if (appPool != null)
                    {
                        if (appPool.State == ObjectState.Stopped)
                        {
                            appPool.Start();
                            serverManager.CommitChanges();
                            //MessageBox.Show($"Пул приложений '{appPoolName}' успешно запущен.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            //MessageBox.Show($"Пул приложений '{appPoolName}' уже запущен.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        //MessageBox.Show($"Пул приложений '{appPoolName}' не найден.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при запуске пула приложений: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //метод остановки пула IIS
        public void StopAppPool(string appPoolName)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ApplicationPool appPool = serverManager.ApplicationPools[appPoolName];

                    if (appPool != null)
                    {
                        if (appPool.State == ObjectState.Started)
                        {
                            appPool.Stop();
                            serverManager.CommitChanges();
                            //MessageBox.Show($"Пул приложений '{appPoolName}' успешно остановлен.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            //MessageBox.Show($"Пул приложений '{appPoolName}' уже остановлен.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        //MessageBox.Show($"Пул приложений '{appPoolName}' не найден.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при запуске пула приложений: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

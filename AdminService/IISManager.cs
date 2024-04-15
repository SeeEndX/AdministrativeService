using Microsoft.Web.Administration;
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
        private string cs = "server=localhost;user=root;database=admintooldb;password=1234;";
        FunctionExecutor executor;

        public class SiteInfo
        {
            public string Name { get; set; }
            public string State { get; set; }
            public string Bindings { get; set; }
        }

        public class AppPoolInfo
        {
            public string Name { get; set; }
            public string State { get; set; }
            public string NETCLRVersion { get; set; }
            public string ManagedPipelineMode { get; set; }
        }

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

        public void AddReport(string currentUser, string description)
        {
            using (MySqlConnection con = new MySqlConnection(cs))
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

        private int GetUserIdByUsername(string username)
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

        public void StartSite(string siteName)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                Site site = serverManager.Sites[siteName];

                if (site != null)
                {
                    if (site.State == ObjectState.Stopped)
                    {
                        site.Start();
                        serverManager.CommitChanges();
                    }
                }
            }
        }

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

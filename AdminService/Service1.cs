using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.Administration; // Для работы с IIS
using System.ServiceModel;
using MySql.Data.MySqlClient;
using System.Collections;
using System.Transactions;
using System.Collections.ObjectModel;
using System.CodeDom;


namespace AdminService
{
    public partial class AdministrativeService : ServiceBase
    {
        ServiceHost serviceHost;

        public AdministrativeService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            serviceHost = new ServiceHost(typeof(AdminService),
                new Uri("net.tcp://localhost:8000/AdminService"));
            serviceHost.AddServiceEndpoint(typeof(IAdminService),
                new NetTcpBinding(), "");
            serviceHost.Open();
        }

        protected override void OnStop()
        {
            serviceHost?.Close();
        }

        [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
        public class AdminService : ServiceBase, IAdminService
        {
            User user;
            private string cs = "server=localhost;user=root;database=admintooldb;password=1234;";
            public event EventHandler DataAdded;
            private IISManager iisManager = new IISManager();

            protected virtual void OnDataAdded()
            {
                DataAdded?.Invoke(this, EventArgs.Empty);
            }

            //логика с IIS Manager
            public List<IISManager.SiteInfo> GetListOfSites()
            {
                return iisManager.GetListOfSites();
            }

            public void AddReport(string currentUser, string description)
            {
                iisManager.AddReport(currentUser, description);
                OnDataAdded();
            }

            public List<IISManager.AppPoolInfo> GetListOfAppPools()
            {
                return iisManager.GetListOfAppPools();
            }

            public void StartSite(string siteName)
            {
                iisManager.StartSite(siteName);
            }

            public void StopSite(string siteName)
            {
                iisManager.StopSite(siteName);
            }

            public void CreateWebsite(string siteName, string physicalPath, int port)
            {
                iisManager.CreateWebsite(siteName, physicalPath, port);
            }

            public void DeleteWebsite(string siteName)
            {
                iisManager?.DeleteWebsite(siteName);
            }

            public void ModifyWebsite(string currentSiteName, string newSiteName, string newPhysicalPath)
            {
                iisManager.ModifyWebsite(currentSiteName, newSiteName, newPhysicalPath);
            }

            public void CreatePool(string poolName, ManagedPipelineMode mode, int memoryLimit, int intervalMinutes)
            {
                iisManager.CreatePool(poolName, mode, memoryLimit, intervalMinutes);
            }

            public void DeletePool(string poolName)
            {
                iisManager.DeletePool(poolName);
            }

            public void ModifyPool(string currentPoolName, string newPoolName, ManagedPipelineMode mode, int memoryLimit, int intervalMinutes)
            {
                iisManager.ModifyPool(currentPoolName, newPoolName, mode, memoryLimit, intervalMinutes);
            }

            public void StartAppPool(string appPoolName)
            {
                iisManager.StartAppPool(appPoolName);
            }

            public void StopAppPool(string appPoolName)
            {
                iisManager.StopAppPool(appPoolName);
            }


            //логика с IIS Manager

            public User Authenticate(string login, string password)
            {
                using (MySqlConnection con = new MySqlConnection(cs))
                {
                    con.Open();
                    string group = SqlQuery($"SELECT usergroup FROM Users WHERE login = '{login}' AND password = '{password}'", con);
                    if (group != null)
                    {
                        user = new User {Login=login, Pass=password, Group = group};
                    }
                    else
                    {
                        user = null;
                    }
                    con.Close();
                    return user;
                }
            }

            public (List<string>, string) GetReportsForUser(int user)
            {
                List<string> reports = new List<string>();
                string username = "";
                try
                {
                    using (MySqlConnection con = new MySqlConnection(cs))
                    {
                        con.Open();

                        string usernameQuery = "SELECT login FROM Users WHERE id = @UserId;";
                        using (MySqlCommand usernameCmd = new MySqlCommand(usernameQuery, con))
                        {
                            usernameCmd.Parameters.AddWithValue("@UserId", user);
                            username = (string)usernameCmd.ExecuteScalar();
                        }

                        string query = @"
                SELECT U.login, R.description, R.time
                FROM Reports R
                JOIN Users U ON R.user = U.id
                WHERE R.user = @UserId;";

                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", user);

                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string login = reader.GetString(0);
                                    string description = reader.GetString(1);
                                    string time = reader.GetString(2);
                                    string reportEntry = $"{description} в {time}\n";
                                    reports.Add(reportEntry);
                                    username = login;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //MessageBox.Show($"Ошибка при получении отчетов для пользователя: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return (reports, username);
            }

            private string SqlQuery(string cmdText, MySqlConnection con)
            {
                using (MySqlCommand cmd = new MySqlCommand(cmdText, con))
                {
                    object resultObj = cmd.ExecuteScalar();
                    string result = (resultObj != null) ? resultObj.ToString() : null;
                    return result;
                }
            }

            public DataView GetUsersData()
            {
                DataTable dataTable = new DataTable();

                var users = new List<User>();

                using (MySqlConnection connection = new MySqlConnection(cs))
                {
                    string cmdText = @"
            SELECT Users.id AS `id`, 
	            Users.login AS `Пользователь`, 
                GROUP_CONCAT(DISTINCT `Function`.name SEPARATOR ', ') AS `Доступные_функции`
            FROM Users
            LEFT JOIN Function_users ON Users.id = Function_users.user
            LEFT JOIN `Function` ON `Function`.id = Function_users.function
            WHERE Users.usergroup = 'Dev'
            GROUP BY Users.id;";

                    using (MySqlCommand cmd = new MySqlCommand(cmdText, connection))
                    {
                        try
                        {
                            connection.Open();
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                dataTable.Load(reader);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Ошибка при выполнении запроса: " + ex.Message);
                        }
                    }
                }

                return dataTable.DefaultView;
            }

            public ObservableCollection<User> GetUserData()
            {
                var users = new ObservableCollection<User>();

                using (MySqlConnection connection = new MySqlConnection(cs))
                {
                    string cmdText = @"
SELECT Users.id AS `id`, 
    Users.login AS `Пользователь`, 
    GROUP_CONCAT(DISTINCT `Function`.name SEPARATOR ', ') AS `Доступные функции`
FROM Users
LEFT JOIN Function_users ON Users.id = Function_users.user
LEFT JOIN `Function` ON `Function`.id = Function_users.function
WHERE Users.usergroup = 'Dev'
GROUP BY Users.id;";

                    using (MySqlCommand cmd = new MySqlCommand(cmdText, connection))
                    {
                        try
                        {
                            connection.Open();
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                var dt = new DataTable();
                                dt.Load(reader);
                                foreach (DataRow row in dt.Rows)
                                {
                                    users.Add(new User
                                    {
                                        Id = Convert.ToInt32(row["id"]),
                                        Login = Convert.ToString(row["Пользователь"]),
                                        Functions = Convert.ToString(row["Доступные функции"])
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Ошибка при выполнении запроса: " + ex.Message);
                        }
                    }
                }

                return users;
            }

            public int AddUser(string username, string password)
            {
                int userAdded = 0;
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(cs))
                    {
                        connection.Open();
                        string cmdText = "INSERT INTO Users (login, password, usergroup) VALUES (@Login, @Password, @Group)";
                        using (MySqlCommand command = new MySqlCommand(cmdText, connection))
                        {
                            command.Parameters.AddWithValue("@Login", username);
                            command.Parameters.AddWithValue("@Password", password);
                            command.Parameters.AddWithValue("@Group", "Dev");

                            userAdded = command.ExecuteNonQuery();
                        }
                        connection.Close();
                        OnDataAdded();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при добавлении пользователя: " + ex.Message);
                }
                return userAdded;
            }

            public int EditUser(string originalUsername, string newUsername, string newPassword)
            {
                int rowsAffected = 0;
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(cs))
                    {
                        connection.Open();
                        string updateQuery = @"
                UPDATE Users
                SET login = @NewUsername,
                    password = @NewPassword
                WHERE login = @OriginalUsername;";
                        using (MySqlCommand command = new MySqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@NewUsername", newUsername);
                            command.Parameters.AddWithValue("@NewPassword", newPassword);
                            command.Parameters.AddWithValue("@OriginalUsername", originalUsername);
                            rowsAffected = command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при редактировании пользователя: {ex.Message}");
                }
                return rowsAffected;
            }

            public void DeleteUser(int userId)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(cs))
                    {
                        connection.Open();
                        MySqlTransaction transaction = connection.BeginTransaction();

                        try
                        {
                            string deleteRelatedQuery = "DELETE FROM function_users WHERE user = @userId";
                            MySqlCommand deleteRelatedCommand = new MySqlCommand(deleteRelatedQuery, connection);
                            deleteRelatedCommand.Parameters.AddWithValue("@userId", userId);
                            deleteRelatedCommand.ExecuteNonQuery();

                            string deleteUserQuery = "DELETE FROM Users WHERE id = @userId";
                            MySqlCommand deleteUserCommand = new MySqlCommand(deleteUserQuery, connection);
                            deleteUserCommand.Parameters.AddWithValue("@userId", userId);
                            deleteUserCommand.ExecuteNonQuery();

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }

                        connection.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при удалении пользователей: " + ex.Message);
                }
            }

            public int GetSelectedUserId(string username)
            {
                int userId = -1;

                string query = "SELECT id FROM Users WHERE login = @username";
                try
                {
                    using (MySqlConnection con = new MySqlConnection(cs))
                    {
                        con.Open();

                        using (MySqlCommand command = new MySqlCommand(query, con))
                        {
                            command.Parameters.Add(new MySqlParameter("@username", username));

                            object result = command.ExecuteScalar();
                            if (result != null && int.TryParse(result.ToString(), out userId))
                            {
                                return userId;
                            }
                        }
                        con.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка - ",ex.Message);
                }
                
                return userId;
            }

            public void UpdatePass(int userId, string newPassword)
            {
                try
                {
                    using (MySqlConnection con = new MySqlConnection(cs))
                    {
                        con.Open();
                        string query = "UPDATE Users SET password = @Password WHERE id = @UserId;";
                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@Password", newPassword);
                            cmd.ExecuteNonQuery();
                        }
                        con.Close();
                        OnDataAdded();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка - ", ex.Message);
                }
            }

            public bool IsUserExists(string login)
            {
                int userCount = 0;
                try
                {
                    using (MySqlConnection con = new MySqlConnection(cs))
                    {
                        con.Open();
                        string cmdText = "SELECT COUNT(*) FROM Users WHERE login = @Login";
                        using (MySqlCommand cmd = new MySqlCommand(cmdText, con))
                        {
                            cmd.Parameters.AddWithValue("@Login", login);
                            userCount = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        con.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка - ", ex.Message);
                }
                return userCount > 0;
            }

            public List<string> GetAllFunctionNames()
            {
                List<string> allFunctionNames = new List<string>();

                using (MySqlConnection connection = new MySqlConnection(cs))
                {
                    string query = "SELECT name FROM `Function`";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        connection.Open();
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                allFunctionNames.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                return allFunctionNames;
            }

            public List<string> GetAssignedFunctionNames(string username)
            {
                List<string> assignedFunctionNames = new List<string>();

                using (MySqlConnection connection = new MySqlConnection(cs))
                {
                    string query = @"SELECT f.name 
                             FROM Function_users fu
                             INNER JOIN Users u ON fu.user = u.id
                             INNER JOIN `Function` f ON fu.`function` = f.id
                             WHERE u.login = @Username";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Username", username);
                        connection.Open();
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                assignedFunctionNames.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                return assignedFunctionNames;
            }

            public void SaveAssignedFunctions(string username, List<string> selectedFunctionNames)
            {
                using (MySqlConnection connection = new MySqlConnection(cs))
                {
                    connection.Open();
                    int userId = GetUserIdByUsername(username, connection);

                    string deleteQuery = "DELETE FROM Function_users WHERE user = @UserId";
                    using (MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.AddWithValue("@UserId", userId);
                        deleteCommand.ExecuteNonQuery();
                    }

                    string insertQuery = "INSERT INTO Function_users (user, `function`) VALUES (@UserId, @FunctionId)";
                    foreach (string functionName in selectedFunctionNames)
                    {
                        int functionId = GetFunctionIdByName(functionName, connection);
                        if (functionId > 0)
                        {
                            using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@UserId", userId);
                                insertCommand.Parameters.AddWithValue("@FunctionId", functionId);
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }

            private int GetUserIdByUsername(string username, MySqlConnection connection)
            {
                string query = "SELECT id FROM Users WHERE login = @Username";
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username);
                    object result = command.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out int userId))
                    {
                        return userId;
                    }
                }
                return -1;
            }

            private int GetFunctionIdByName(string functionName, MySqlConnection connection)
            {
                string query = "SELECT id FROM `Function` WHERE name = @FunctionName";
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FunctionName", functionName);
                    object result = command.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out int functionId))
                    {
                        return functionId;
                    }
                }
                return -1;
            }
        }
    }
}

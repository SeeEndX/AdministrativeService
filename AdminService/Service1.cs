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

            protected virtual void OnDataAdded()
            {
                DataAdded?.Invoke(this, EventArgs.Empty);
            }

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
            private string SqlQuery(string cmdText, MySqlConnection con)
            {
                using (MySqlCommand cmd = new MySqlCommand(cmdText, con))
                {
                    object resultObj = cmd.ExecuteScalar();
                    string result = (resultObj != null) ? resultObj.ToString() : null;
                    return result;
                }
            }

            public DataTable GetUsersData()
            {
                DataTable dataTable = new DataTable();

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
                                dataTable.Load(reader);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Ошибка при выполнении запроса: " + ex.Message);
                        }
                    }
                }

                return dataTable;
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
                        return userAdded;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при добавлении пользователя: " + ex.Message);
                    return 0;
                }
            }

            public void EditUser(string oldUsername, string newUsername)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(cs))
                    {
                        connection.Open();
                        string query = "UPDATE Users SET username = @NewUsername WHERE username = @OldUsername";
                        MySqlCommand command = new MySqlCommand(query, connection);
                        command.Parameters.AddWithValue("@NewUsername", newUsername);
                        command.Parameters.AddWithValue("@OldUsername", oldUsername);
                        command.ExecuteNonQuery();
                        connection.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при редактировании пользователя: " + ex.Message);
                }
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

            public List<string> GetFunctions()
            {
                List<string> allFunctions = new List<string>();

                try
                {
                    using (MySqlConnection con = new MySqlConnection(cs))
                    {
                        con.Open();
                        string query = "SELECT name FROM Function;";
                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string functionName = reader.GetString(0);
                                    allFunctions.Add(functionName);
                                }
                            }
                        }
                        con.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка - ", ex.Message);
                }

                return allFunctions;
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

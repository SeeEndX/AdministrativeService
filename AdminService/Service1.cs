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
            SELECT Users.id AS `id`, Users.login AS `Пользователь`, GROUP_CONCAT(`Function`.name, ', ') AS `Доступные функции`
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
                        string query = "DELETE FROM Users WHERE username IN (@userId)";
                        MySqlCommand command = new MySqlCommand(query, connection);
                        command.Parameters.AddWithValue("@userId", string.Join(",", userId));
                        command.ExecuteNonQuery();
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
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка - ", ex.Message);
                }
            }
        }
    }
}

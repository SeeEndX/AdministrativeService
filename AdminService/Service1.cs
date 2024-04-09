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


namespace AdminService
{
    public partial class Service1 : ServiceBase
    {
        ServiceHost serviceHost;

        public Service1()
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
            private string cs = "server=localhost;user=root;database=admintooldb;password=1234";

            public User Authenticate(string login, string password)
            {
                using (MySqlConnection con = new MySqlConnection(cs))
                {
                    con.Open();
                    string group = SqlQuery("SELECT usergroup FROM Users WHERE login = @Login AND password = @Password", con, login, password);
                    if (group != null)
                    {
                        return new User {Login=login, Pass=password, Group = group};
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            private string SqlQuery(string cmdText, MySqlConnection con, string login, string password)
            {
                using (MySqlCommand cmd = new MySqlCommand(cmdText, con))
                {
                    cmd.Parameters.AddWithValue("@Login", login);
                    cmd.Parameters.AddWithValue("@Password", password);
                    object resultObj = cmd.ExecuteScalar();
                    string result = (resultObj != null) ? resultObj.ToString() : null;
                    return result;
                }
            }
        }
    }
}

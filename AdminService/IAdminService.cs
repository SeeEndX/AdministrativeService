using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Data;


namespace AdminService
{
    [ServiceContract]
    public interface IAdminService
    {
        [OperationContract]
        User Authenticate(string login, string password);

        [OperationContract]
        DataTable GetUsersData();

        [OperationContract]
        int AddUser(string username, string password);

        [OperationContract]
        void EditUser(string oldUsername, string newUsername);

        [OperationContract]
        void DeleteUser(int userId);

        [OperationContract]
        int GetSelectedUserId(string newUsername);

        [OperationContract]
        void UpdatePass(int userId, string newPassword);

        [OperationContract]
        List<string> GetFunctions(int userId, string newPassword);
    }

    [DataContract]
    public class User
    {
        [DataMember]
        public string Login { get; set; }

        [DataMember]
        public string Pass { get; set; }

        [DataMember]
        public string Group { get; set; }
    }
}
